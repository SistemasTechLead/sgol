using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sgol.Assignment.Contracts;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.Configuration.Contracts;
using Sgol.Generation.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;

namespace Sgol.Web.Infrastructure.Persistence.Assignment;

public sealed class EfEligibilityEvaluationService(
    SgolDbContext dbContext,
    AuditTransaction auditTransaction,
    IClock clock,
    IUuidGenerator uuidGenerator) : IEligibilityEvaluationService
{
    private const string IdempotencyScope = "eligibility:evaluate";
    private const string IdempotencyPrimaryKey = "PK_idempotency_record";

    public async Task<EligibilityEvaluationDetails> EvaluateAsync(
        EvaluateEligibilityCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!EligibilityDateSources.IsDefined(command.EligibilityDateSource))
        {
            throw new EligibilityDateInvalidException();
        }

        var requestHash = Hash(
            command.ObligationId.ToString("D", CultureInfo.InvariantCulture),
            command.EligibilityDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            command.EligibilityDateSource);
        var replay = await FindReplayAsync(command.EvaluationRequestId, requestHash, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        Guid evaluationId = default;
        try
        {
            await auditTransaction.ExecuteAsync(
                IsolationLevel.RepeatableRead,
                async token =>
                {
                    var now = clock.UtcNow;
                    var obligation = await dbContext.WorkObligations
                        .AsNoTracking()
                        .SingleOrDefaultAsync(item => item.Id == command.ObligationId, token)
                        ?? throw new EligibilityEvaluationNotFoundException();
                    if (!string.Equals(obligation.ExecutionStatus, WorkObligationStatuses.Pending, StringComparison.Ordinal))
                    {
                        throw new EligibilityObligationNotPendingException();
                    }

                    var generationRequest = await dbContext.GenerationRequests
                        .AsNoTracking()
                        .SingleAsync(item => item.Id == obligation.GenerationRequestId, token);
                    ValidateDate(command, generationRequest.RequestedAt);

                    var matchingPolicies = await dbContext.EligibilityPolicyVersions
                        .AsNoTracking()
                        .Where(policy =>
                            policy.TaskDefinitionVersionId == obligation.TaskDefinitionVersionId &&
                            policy.EffectiveFrom != null &&
                            policy.EffectiveFrom <= generationRequest.RequestedAt &&
                            (policy.EffectiveTo == null || generationRequest.RequestedAt < policy.EffectiveTo))
                        .ToListAsync(token);
                    if (matchingPolicies.Count != 1)
                    {
                        throw new EligibilityPolicyConfigurationException();
                    }

                    var policy = matchingPolicies[0];
                    var people = await dbContext.People.AsNoTracking()
                        .OrderBy(person => person.StableCode)
                        .ThenBy(person => person.Id)
                        .ToListAsync(token);
                    var personIds = people.Select(person => person.Id).ToArray();
                    var employments = await dbContext.EmploymentVersions.AsNoTracking()
                        .Where(employment =>
                            personIds.Contains(employment.PersonId) &&
                            employment.ValidFrom <= now &&
                            (employment.ValidTo == null || now < employment.ValidTo))
                        .ToListAsync(token);
                    var users = await dbContext.AppUsers.AsNoTracking()
                        .Where(user => personIds.Contains(user.PersonId))
                        .ToListAsync(token);
                    var userIds = users.Select(user => user.Id).ToArray();
                    var roles = await dbContext.RoleAssignmentVersions.AsNoTracking()
                        .Where(role =>
                            userIds.Contains(role.UserId) &&
                            role.ValidFrom <= now &&
                            (role.ValidTo == null || now < role.ValidTo))
                        .ToListAsync(token);
                    var availabilities = await dbContext.AvailabilityDayVersions.AsNoTracking()
                        .Where(availability =>
                            personIds.Contains(availability.PersonId) &&
                            availability.BranchId == obligation.BranchId &&
                            availability.LocalDate == command.EligibilityDate &&
                            availability.Status == AvailabilityVersionStatus.Current)
                        .ToListAsync(token);

                    var inputs = people.Select(person => EvaluatePerson(
                        person,
                        obligation.BranchId,
                        policy,
                        employments,
                        users,
                        roles,
                        availabilities)).ToArray();
                    var result = inputs.Any(input => input.IsEligible)
                        ? EligibilityResults.EligibleCandidates
                        : EligibilityResults.NoEligibleCandidate;
                    evaluationId = uuidGenerator.NewUuid();
                    var inputSnapshot = JsonSerializer.SerializeToDocument(new
                    {
                        schemaVersion = 1,
                        obligationId = obligation.Id,
                        obligationTaskDefinitionVersionId = obligation.TaskDefinitionVersionId,
                        obligationBranchId = obligation.BranchId,
                        generationRequestId = generationRequest.Id,
                        generationRequestedAt = generationRequest.RequestedAt,
                        evaluatedAt = now,
                        eligibilityDate = command.EligibilityDate,
                        eligibilityDateSource = command.EligibilityDateSource,
                        policyVersionId = policy.Id,
                        requiredRole = policy.RequiredRole,
                        requiredShift = policy.RequiredShift,
                        candidates = inputs,
                    }, JsonSerializerOptions.Web);
                    var evaluation = new EligibilityEvaluation(
                        evaluationId,
                        command.EvaluationRequestId,
                        obligation.Id,
                        now,
                        command.EligibilityDate,
                        command.EligibilityDateSource,
                        policy.Id,
                        inputSnapshot,
                        result);
                    dbContext.EligibilityEvaluations.Add(evaluation);
                    dbContext.EligibilityCandidates.AddRange(inputs.Select(input => new EligibilityCandidate(
                        evaluationId,
                        input.PersonId,
                        input.StableCode,
                        input.IsEligible,
                        JsonSerializer.SerializeToDocument(input.Reasons))));
                    dbContext.IdempotencyRecords.Add(new IdempotencyRecord
                    {
                        Scope = IdempotencyScope,
                        Key = command.EvaluationRequestId,
                        RequestHash = requestHash,
                        Status = "COMPLETED",
                        ResourceType = "ELIGIBILITY_EVALUATION",
                        ResourceId = evaluationId,
                        ResponseCode = StatusCodes.Status201Created,
                        CreatedAt = now,
                        ExpiresAt = DateTimeOffset.MaxValue,
                    });

                    return new AuditEvent
                    {
                        Id = uuidGenerator.NewUuid(),
                        OccurredAt = now,
                        ActorType = "SYSTEM",
                        Action = "ELIGIBILITY_EVALUATED",
                        ResourceType = "ELIGIBILITY_EVALUATION",
                        ResourceId = evaluationId,
                        BranchId = obligation.BranchId,
                        CorrelationId = command.CorrelationId,
                        RequestId = command.EvaluationRequestId.ToString("D", CultureInfo.InvariantCulture),
                        AfterData = JsonSerializer.SerializeToDocument(new
                        {
                            schemaVersion = 1,
                            evaluationId,
                            obligationId = obligation.Id,
                            policyVersionId = policy.Id,
                            command.EligibilityDate,
                            command.EligibilityDateSource,
                            result,
                            eligibleCount = inputs.Count(input => input.IsEligible),
                            excludedCount = inputs.Count(input => !input.IsEligible),
                        }, JsonSerializerOptions.Web),
                        Outcome = result,
                    };
                },
                cancellationToken);
        }
        catch (DbUpdateException exception) when (GetConstraintName(exception) is
            EligibilityEvaluationConfiguration.RequestIndex or IdempotencyPrimaryKey)
        {
            dbContext.ChangeTracker.Clear();
            return await FindReplayAsync(command.EvaluationRequestId, requestHash, cancellationToken)
                ?? throw new EligibilityEvaluationIdempotencyConflictException();
        }
        catch
        {
            dbContext.ChangeTracker.Clear();
            throw;
        }

        var created = await LoadDetailsAsync(evaluationId, replayed: false, cancellationToken);
        dbContext.ChangeTracker.Clear();
        return created;
    }

    public async Task<EligibilityEvaluationDetails> GetLatestAsync(
        Guid actorUserId,
        Guid correlationId,
        Guid obligationId,
        CancellationToken cancellationToken = default)
    {
        _ = correlationId;
        var actorRole = await GetActorRoleAsync(actorUserId, cancellationToken)
            ?? throw new EligibilityEvaluationAccessDeniedException();
        var visible = await (
            from evaluation in dbContext.EligibilityEvaluations.AsNoTracking()
            join obligation in dbContext.WorkObligations.AsNoTracking()
                on evaluation.ObligationId equals obligation.Id
            join policy in dbContext.EligibilityPolicyVersions.AsNoTracking()
                on evaluation.PolicyVersionId equals policy.Id
            where obligation.Id == obligationId && obligation.BranchId == BranchScope.LorettaId
            orderby evaluation.EvaluatedAt descending, evaluation.Id descending
            select new { evaluation.Id, policy.RequiredRole })
            .FirstOrDefaultAsync(cancellationToken);
        if (visible is null || !RoleHierarchy.CanAccessLevel(actorRole, visible.RequiredRole))
        {
            throw new EligibilityEvaluationNotFoundException();
        }

        var result = await LoadDetailsAsync(visible.Id, replayed: false, cancellationToken);
        dbContext.ChangeTracker.Clear();
        return result;
    }

    private async Task<EligibilityEvaluationDetails?> FindReplayAsync(
        Guid requestId,
        string requestHash,
        CancellationToken cancellationToken)
    {
        var record = await dbContext.IdempotencyRecords.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Scope == IdempotencyScope && item.Key == requestId, cancellationToken);
        if (record is null)
        {
            return null;
        }

        if (!string.Equals(record.RequestHash, requestHash, StringComparison.Ordinal))
        {
            throw new EligibilityEvaluationIdempotencyConflictException();
        }

        return await LoadDetailsAsync(record.ResourceId, replayed: true, cancellationToken);
    }

    private async Task<EligibilityEvaluationDetails> LoadDetailsAsync(
        Guid evaluationId,
        bool replayed,
        CancellationToken cancellationToken)
    {
        var evaluation = await dbContext.EligibilityEvaluations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == evaluationId, cancellationToken)
            ?? throw new EligibilityEvaluationNotFoundException();
        var policy = await dbContext.EligibilityPolicyVersions.AsNoTracking()
            .SingleAsync(item => item.Id == evaluation.PolicyVersionId, cancellationToken);
        var storedCandidates = await dbContext.EligibilityCandidates.AsNoTracking()
            .Where(candidate => candidate.EvaluationId == evaluationId)
            .OrderBy(candidate => candidate.StableCode)
            .ThenBy(candidate => candidate.PersonId)
            .ToListAsync(cancellationToken);
        var snapshotCandidates = evaluation.InputSnapshot.RootElement.GetProperty("candidates")
            .EnumerateArray()
            .ToDictionary(item => item.GetProperty("personId").GetGuid());
        var candidates = storedCandidates.Select(candidate =>
        {
            var input = snapshotCandidates[candidate.PersonId];
            return new EligibilityCandidateExplanation(
                candidate.PersonId,
                candidate.StableCode,
                candidate.IsEligible,
                candidate.Reasons.RootElement.EnumerateArray().Select(reason => reason.GetString()!).ToArray(),
                NullableGuid(input, "employmentVersionId"),
                NullableString(input, "employmentStatus"),
                NullableGuid(input, "employmentBranchId"),
                NullableString(input, "positionText"),
                NullableString(input, "shiftText"),
                NullableGuid(input, "roleAssignmentVersionId"),
                NullableString(input, "activeRoleCode"),
                NullableGuid(input, "availabilityVersionId"),
                NullableBoolean(input, "isAvailable"),
                candidate.ActiveLoad,
                candidate.LastAutoAssignmentAt,
                candidate.Rank);
        }).ToArray();

        return new EligibilityEvaluationDetails(
            evaluation.Id,
            evaluation.EvaluationRequestId,
            evaluation.ObligationId,
            evaluation.EvaluatedAt,
            evaluation.EligibilityDate,
            evaluation.EligibilityDateSource,
            evaluation.PolicyVersionId,
            policy.RequiredRole,
            policy.RequiredShift,
            evaluation.Result,
            evaluation.WinnerPersonId,
            candidates,
            replayed);
    }

    private static CandidateInput EvaluatePerson(
        Person person,
        Guid branchId,
        EligibilityPolicyVersion policy,
        IReadOnlyList<EmploymentVersion> employments,
        IReadOnlyList<AppUser> users,
        IReadOnlyList<RoleAssignmentVersion> roles,
        IReadOnlyList<AvailabilityDayVersion> availabilities)
    {
        var employment = employments
            .Where(item => item.PersonId == person.Id)
            .OrderByDescending(item => item.BranchId == branchId)
            .ThenByDescending(item => item.ValidFrom)
            .FirstOrDefault();
        var user = users.SingleOrDefault(item => item.PersonId == person.Id);
        var role = user is { Status: BootstrapContract.ActiveAccountStatus }
            ? roles.SingleOrDefault(item => item.UserId == user.Id && item.BranchId == branchId)
            : null;
        var availability = availabilities.SingleOrDefault(item => item.PersonId == person.Id);
        var reasons = EligibilityEvaluator.Explain(
            new EligibilityPersonInput(
                employment is not null,
                employment?.Status == EmploymentStatus.Active,
                employment?.BranchId == branchId,
                role is not null,
                role?.RoleCode,
                availability?.IsAvailable,
                employment?.ShiftText),
            policy.RequiredRole,
            policy.RequiredShift);

        return new CandidateInput(
            person.Id,
            person.StableCode,
            reasons.Count == 0,
            reasons,
            employment?.Id,
            employment?.Status,
            employment?.BranchId,
            employment?.PositionText,
            employment?.ShiftText,
            role?.Id,
            role?.RoleCode,
            availability?.Id,
            availability?.IsAvailable);
    }

    private static void ValidateDate(EvaluateEligibilityCommand command, DateTimeOffset requestedAt)
    {
        if (command.EligibilityDateSource != EligibilityDateSources.ManualRequest)
        {
            return;
        }

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(BranchScope.TimeZone);
        var expected = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(requestedAt, timeZone).DateTime);
        if (command.EligibilityDate != expected)
        {
            throw new EligibilityDateInvalidException();
        }
    }

    private async Task<string?> GetActorRoleAsync(Guid actorUserId, CancellationToken cancellationToken) =>
        await (
            from user in dbContext.AppUsers.AsNoTracking()
            join role in dbContext.RoleAssignmentVersions.AsNoTracking() on user.Id equals role.UserId
            join employment in dbContext.EmploymentVersions.AsNoTracking() on user.PersonId equals employment.PersonId
            where user.Id == actorUserId &&
                user.Status == BootstrapContract.ActiveAccountStatus &&
                role.BranchId == BranchScope.LorettaId &&
                role.Status == RoleAssignmentStatus.Active &&
                role.ValidTo == null &&
                employment.BranchId == BranchScope.LorettaId &&
                employment.Status == EmploymentStatus.Active &&
                employment.ValidTo == null
            select role.RoleCode)
        .SingleOrDefaultAsync(cancellationToken);

    private static Guid? NullableGuid(JsonElement element, string propertyName) =>
        element.GetProperty(propertyName) is { ValueKind: JsonValueKind.String } value ? value.GetGuid() : null;

    private static string? NullableString(JsonElement element, string propertyName) =>
        element.GetProperty(propertyName) is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;

    private static bool? NullableBoolean(JsonElement element, string propertyName) =>
        element.GetProperty(propertyName) is { ValueKind: JsonValueKind.True } ? true :
        element.GetProperty(propertyName) is { ValueKind: JsonValueKind.False } ? false : null;

    private static string Hash(params string[] parts) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', parts))));

    private static string? GetConstraintName(DbUpdateException exception) =>
        (exception.InnerException as PostgresException)?.ConstraintName;

    private sealed record CandidateInput(
        Guid PersonId,
        string StableCode,
        bool IsEligible,
        IReadOnlyList<string> Reasons,
        Guid? EmploymentVersionId,
        string? EmploymentStatus,
        Guid? EmploymentBranchId,
        string? PositionText,
        string? ShiftText,
        Guid? RoleAssignmentVersionId,
        string? ActiveRoleCode,
        Guid? AvailabilityVersionId,
        bool? IsAvailable);
}
