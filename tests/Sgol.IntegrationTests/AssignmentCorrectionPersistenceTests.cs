using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.Assignment.Contracts;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Configuration.Contracts;
using Sgol.Generation.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Assignment;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Testcontainers.PostgreSql;
using Xunit;

namespace Sgol.IntegrationTests;

public sealed class AssignmentCorrectionPersistenceTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 4, 18, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly EligibilityDate = new(2026, 9, 4);
    private static readonly string[] ExplanationProperties =
    [
        "schemaVersion", "operation", "obligationId", "supersededAssignmentId",
        "previousResponsiblePersonId", "newResponsiblePersonId", "actorUserId", "correctedAt",
        "reasonField", "eligibilityEvaluationId", "eligibilityPolicyVersionId",
        "actorEmploymentVersionId", "actorRoleAssignmentVersionId", "candidateEmploymentVersionId",
        "candidateRoleAssignmentVersionId", "candidateAvailabilityVersionId", "actorRoleCode",
        "targetRoleCode", "hierarchyResult",
    ];
    private readonly PostgreSqlContainer _postgres = PostgreSqlPersistenceTests.CreateContainerForTests();

    public Task InitializeAsync() => _postgres.StartAsync();
    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task AdministrationCorrectsSubcoordinationAndPersistsExactChainExplanationAuditAndNoEffects()
    {
        var seed = await ResetAsync();
        await using var context = CreateContext();
        var actor = AddUser(context, "CORR-ADM", CanonicalRole.Administration, "Gerencia textual irrelevante");
        var previous = AddUser(context, "CORR-OLD", CanonicalRole.Subcoordination);
        var candidate = AddUser(context, "CORR-NEW", CanonicalRole.Subcoordination, "Puesto cualquiera");
        await context.SaveChangesAsync();
        var obligationId = await AddObligationAsync(context, seed, "correction-success");
        var originalId = AddAutomatic(context, obligationId, previous.Person.Id);
        var evaluation = AddEvaluation(context, obligationId, seed.PolicyId, previous.Person, candidate.Person);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var before = await CountsAsync(context);
        var key = Guid.CreateVersion7();

        var result = await Service(context).CorrectAsync(new(
            actor.User.Id, key, Guid.CreateVersion7(), obligationId, candidate.Person.Id,
            evaluation.Id, "  Cobertura   operativa autorizada ", 1));

        Assert.Equal(AssignmentCorrectionResults.Created, result.Result);
        Assert.Equal(2, result.RowVersion);
        Assert.Equal("Cobertura operativa autorizada", result.Reason);
        var history = await context.AssignmentVersions.AsNoTracking()
            .Where(item => item.ObligationId == obligationId).OrderBy(item => item.AssignedAt).ToListAsync();
        Assert.Equal(2, history.Count);
        Assert.Equal(AssignmentVersionStatuses.Superseded, history[0].Status);
        Assert.Equal(AssignmentVersionStatuses.Current, history[1].Status);
        Assert.Equal(AssignmentTypes.Correction, history[1].AssignmentType);
        Assert.Equal(originalId, history[1].SupersedesId);
        Assert.Equal(actor.User.Id, history[1].AssignedBy);

        var explanation = history[1].Explanation.RootElement;
        Assert.Equal(ExplanationProperties.Order(StringComparer.Ordinal),
            explanation.EnumerateObject().Select(item => item.Name).Order(StringComparer.Ordinal));
        Assert.Equal("ASSIGNMENT_CORRECTION", explanation.GetProperty("operation").GetString());
        Assert.Equal(CanonicalRole.Administration, explanation.GetProperty("actorRoleCode").GetString());
        Assert.Equal(CanonicalRole.Subcoordination, explanation.GetProperty("targetRoleCode").GetString());
        Assert.False(explanation.TryGetProperty("reason", out _));
        Assert.False(explanation.TryGetProperty("positionText", out _));

        var obligation = await context.WorkObligations.AsNoTracking().SingleAsync(item => item.Id == obligationId);
        Assert.Equal(WorkObligationStatuses.Pending, obligation.ExecutionStatus);
        Assert.Equal(2, obligation.RowVersion);
        var audit = await context.AuditEvents.AsNoTracking()
            .SingleAsync(item => item.RequestId == key.ToString("D"));
        Assert.Equal("ASSIGNMENT_CORRECTED", audit.Action);
        Assert.Equal("USER", audit.ActorType);
        Assert.Equal(actor.User.Id, audit.ActorUserId);
        Assert.Equal("Cobertura operativa autorizada", audit.Reason);
        Assert.Equal(before with
        {
            Assignments = before.Assignments + 1,
            Idempotency = before.Idempotency + 1,
            Audits = before.Audits + 1,
        }, await CountsAsync(context));
    }

    [Fact]
    public async Task DirectionCanCorrectACurrentCorrectionAndTheChainCanReturnToAHistoricalPerson()
    {
        var seed = await ResetAsync();
        await using var context = CreateContext();
        var administration = AddUser(context, "CORR-ADM", CanonicalRole.Administration);
        var direction = AddUser(context, "CORR-DIR", CanonicalRole.Direction);
        var first = AddUser(context, "CORR-001", CanonicalRole.Subcoordination);
        var second = AddUser(context, "CORR-002", CanonicalRole.Subcoordination);
        await context.SaveChangesAsync();
        var obligationId = await AddObligationAsync(context, seed, "correction-chain");
        var originalId = AddAutomatic(context, obligationId, first.Person.Id);
        var evaluation = AddEvaluation(context, obligationId, seed.PolicyId, first.Person, second.Person);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var service = Service(context);
        var firstCorrection = await service.CorrectAsync(new(
            administration.User.Id, Guid.CreateVersion7(), Guid.CreateVersion7(), obligationId,
            second.Person.Id, evaluation.Id, "Primer ajuste autorizado", 1));
        var secondCorrection = await service.CorrectAsync(new(
            direction.User.Id, Guid.CreateVersion7(), Guid.CreateVersion7(), obligationId,
            first.Person.Id, evaluation.Id, "Segundo ajuste autorizado", 2));

        Assert.Equal(3, secondCorrection.RowVersion);
        var chain = await context.AssignmentVersions.AsNoTracking()
            .Where(item => item.ObligationId == obligationId).ToListAsync();
        Assert.Equal(3, chain.Count);
        Assert.Equal(originalId, chain.Single(item => item.Id == firstCorrection.AssignmentId).SupersedesId);
        Assert.Equal(firstCorrection.AssignmentId,
            chain.Single(item => item.Id == secondCorrection.AssignmentId).SupersedesId);
        Assert.Single(chain, item => item.Status == AssignmentVersionStatuses.Current);
        Assert.Equal(first.Person.Id, chain.Single(item => item.Status == AssignmentVersionStatuses.Current).PersonId);
    }

    [Fact]
    public async Task PeerIneligibleStaleVersionAndTextualPositionAreRejectedWithoutAssignmentEffects()
    {
        var seed = await ResetAsync();
        await using var context = CreateContext();
        var peer = AddUser(context, "CORR-PEER", CanonicalRole.Subcoordination, "Dirección");
        var previous = AddUser(context, "CORR-OLD", CanonicalRole.Subcoordination);
        var candidate = AddUser(context, "CORR-NEW", CanonicalRole.Subcoordination);
        await context.SaveChangesAsync();
        var obligationId = await AddObligationAsync(context, seed, "correction-negative");
        AddAutomatic(context, obligationId, previous.Person.Id);
        var evaluation = AddEvaluation(context, obligationId, seed.PolicyId, previous.Person, candidate.Person);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var service = Service(context);

        await Assert.ThrowsAsync<AssignmentCorrectionAccessDeniedException>(() => service.CorrectAsync(new(
            peer.User.Id, Guid.CreateVersion7(), Guid.CreateVersion7(), obligationId, candidate.Person.Id,
            evaluation.Id, "Puesto no concede autoridad", 1)));

        var administration = AddUser(context, "CORR-ADM", CanonicalRole.Administration);
        await context.SaveChangesAsync();
        var availability = await context.AvailabilityDayVersions
            .SingleAsync(item => item.PersonId == candidate.Person.Id && item.Status == AvailabilityVersionStatus.Current);
        availability.CreateSuccessor(Guid.CreateVersion7(), false, administration.User.Id);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        await Assert.ThrowsAsync<AssignmentCorrectionResponsibleIneligibleException>(() => service.CorrectAsync(new(
            administration.User.Id, Guid.CreateVersion7(), Guid.CreateVersion7(), obligationId, candidate.Person.Id,
            evaluation.Id, "Candidato perdió disponibilidad", 1)));

        await Assert.ThrowsAsync<AssignmentCorrectionVersionConflictException>(() => service.CorrectAsync(new(
            administration.User.Id, Guid.CreateVersion7(), Guid.CreateVersion7(), obligationId, previous.Person.Id,
            evaluation.Id, "Versión anterior rechazada", 9)));
        Assert.Single(await context.AssignmentVersions.AsNoTracking()
            .Where(item => item.ObligationId == obligationId).ToListAsync());
        Assert.Equal(1, (await context.WorkObligations.AsNoTracking().SingleAsync(item => item.Id == obligationId)).RowVersion);
    }

    [Fact]
    public async Task MissingRoleInactiveAccountMissingAssignmentEvaluationStatesAndNoChangeAreRejected()
    {
        var seed = await ResetAsync();
        await using var context = CreateContext();
        var administration = AddUser(context, "CORR-ADM", CanonicalRole.Administration);
        var inactive = AddUser(context, "CORR-INACTIVE", CanonicalRole.Administration);
        inactive.User.Status = AccountStatus.Inactive;
        var withoutRole = AddUser(context, "CORR-NOROLE", CanonicalRole.Administration);
        withoutRole.Role.Status = RoleAssignmentStatus.Superseded;
        withoutRole.Role.ValidTo = Now.AddMinutes(-1);
        var previous = AddUser(context, "CORR-OLD", CanonicalRole.Subcoordination);
        var candidate = AddUser(context, "CORR-NEW", CanonicalRole.Subcoordination);
        await context.SaveChangesAsync();
        var obligationId = await AddObligationAsync(context, seed, "correction-guards");
        AddAutomatic(context, obligationId, previous.Person.Id);
        var oldEvaluation = AddEvaluation(
            context, obligationId, seed.PolicyId, Now.AddMinutes(-2), previous.Person, candidate.Person);
        var latestEvaluation = AddEvaluation(
            context, obligationId, seed.PolicyId, Now.AddMinutes(-1), previous.Person, candidate.Person);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var service = Service(context);

        await Assert.ThrowsAsync<AssignmentCorrectionAccessDeniedException>(() => service.CorrectAsync(new(
            inactive.User.Id, Guid.CreateVersion7(), Guid.CreateVersion7(), obligationId, candidate.Person.Id,
            latestEvaluation.Id, "Cuenta inactiva rechazada", 1)));
        await Assert.ThrowsAsync<AssignmentCorrectionAccessDeniedException>(() => service.CorrectAsync(new(
            withoutRole.User.Id, Guid.CreateVersion7(), Guid.CreateVersion7(), obligationId, candidate.Person.Id,
            latestEvaluation.Id, "Rol no vigente rechazado", 1)));
        await Assert.ThrowsAsync<AssignmentCorrectionEvaluationNotFoundException>(() => service.CorrectAsync(new(
            administration.User.Id, Guid.CreateVersion7(), Guid.CreateVersion7(), obligationId, candidate.Person.Id,
            Guid.CreateVersion7(), "Evaluación ausente rechazada", 1)));
        await Assert.ThrowsAsync<AssignmentCorrectionEvaluationStaleException>(() => service.CorrectAsync(new(
            administration.User.Id, Guid.CreateVersion7(), Guid.CreateVersion7(), obligationId, candidate.Person.Id,
            oldEvaluation.Id, "Evaluación anterior rechazada", 1)));
        await Assert.ThrowsAsync<AssignmentCorrectionNoChangeException>(() => service.CorrectAsync(new(
            administration.User.Id, Guid.CreateVersion7(), Guid.CreateVersion7(), obligationId, previous.Person.Id,
            latestEvaluation.Id, "Responsable actual sin cambio", 1)));

        var noAssignment = await AddObligationAsync(context, seed, "correction-no-assignment");
        AddEvaluation(context, noAssignment, seed.PolicyId, candidate.Person);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var noAssignmentEvaluationId = await context.EligibilityEvaluations
            .Where(item => item.ObligationId == noAssignment).Select(item => item.Id).SingleAsync();
        await Assert.ThrowsAsync<AssignmentCorrectionCurrentAssignmentNotFoundException>(() => service.CorrectAsync(new(
            administration.User.Id, Guid.CreateVersion7(), Guid.CreateVersion7(), noAssignment, candidate.Person.Id,
            noAssignmentEvaluationId, "Asignación vigente ausente", 1)));

        var concluded = await AddObligationAsync(context, seed, "correction-concluded");
        AddAutomatic(context, concluded, previous.Person.Id);
        var concludedEvaluation = AddEvaluation(context, concluded, seed.PolicyId, candidate.Person);
        await context.SaveChangesAsync();
        await ObligationConclusionTestData.ConcludeAsync(context, concluded, Now);
        await Assert.ThrowsAsync<AssignmentCorrectionObligationNotCorrectableException>(() => service.CorrectAsync(new(
            administration.User.Id, Guid.CreateVersion7(), Guid.CreateVersion7(), concluded, candidate.Person.Id,
            concludedEvaluation.Id, "Obligación concluida rechazada", 1)));
        Assert.Single(await context.AssignmentVersions.AsNoTracking()
            .Where(item => item.ObligationId == obligationId && item.Status == AssignmentVersionStatuses.Current)
            .ToListAsync());
    }

    [Fact]
    public async Task IdenticalRetryRecoversDifferentContentConflictsAndAuditFailureRollsBack()
    {
        var seed = await ResetAsync();
        await using var context = CreateContext();
        var actor = AddUser(context, "CORR-ADM", CanonicalRole.Administration);
        var previous = AddUser(context, "CORR-OLD", CanonicalRole.Subcoordination);
        var candidate = AddUser(context, "CORR-NEW", CanonicalRole.Subcoordination);
        await context.SaveChangesAsync();
        var obligationId = await AddObligationAsync(context, seed, "correction-replay");
        AddAutomatic(context, obligationId, previous.Person.Id);
        var evaluation = AddEvaluation(context, obligationId, seed.PolicyId, previous.Person, candidate.Person);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var key = Guid.CreateVersion7();
        var command = new CorrectAssignmentCommand(
            actor.User.Id, key, Guid.CreateVersion7(), obligationId, candidate.Person.Id,
            evaluation.Id, "Motivo de recuperación", 1);
        var service = Service(context);

        var created = await service.CorrectAsync(command);
        var replay = await service.CorrectAsync(command with { CorrelationId = Guid.CreateVersion7() });
        Assert.Equal(AssignmentCorrectionResults.Recovered, replay.Result);
        Assert.Equal(created.AssignmentId, replay.AssignmentId);
        Assert.Equal(created.RowVersion, replay.RowVersion);
        await Assert.ThrowsAsync<AssignmentCorrectionIdempotencyConflictException>(() =>
            service.CorrectAsync(command with { Reason = "Contenido conflictivo distinto" }));
        Assert.Equal(2, await context.AssignmentVersions.CountAsync(item => item.ObligationId == obligationId));

        var rollbackObligation = await AddObligationAsync(context, seed, "correction-rollback");
        AddAutomatic(context, rollbackObligation, previous.Person.Id);
        var rollbackEvaluation = AddEvaluation(context, rollbackObligation, seed.PolicyId, previous.Person, candidate.Person);
        var duplicateAuditId = Guid.CreateVersion7();
        context.AuditEvents.Add(new AuditEvent
        {
            Id = duplicateAuditId,
            OccurredAt = Now.AddMinutes(-5),
            ActorType = "SYSTEM",
            Action = "SYNTHETIC",
            ResourceType = "WORK_OBLIGATION",
            ResourceId = rollbackObligation,
            BranchId = BranchScope.LorettaId,
            CorrelationId = Guid.CreateVersion7(),
            Outcome = "SUCCESS",
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var rolledBackAssignmentId = Guid.CreateVersion7();
        var rollbackKey = Guid.CreateVersion7();
        var failingService = Service(context, new SequenceUuidGenerator(rolledBackAssignmentId, duplicateAuditId));
        await Assert.ThrowsAsync<DbUpdateException>(() => failingService.CorrectAsync(new(
            actor.User.Id, rollbackKey, Guid.CreateVersion7(), rollbackObligation, candidate.Person.Id,
            rollbackEvaluation.Id, "Falla atómica de auditoría", 1)));
        Assert.False(await context.AssignmentVersions.AnyAsync(item => item.Id == rolledBackAssignmentId));
        Assert.False(await context.IdempotencyRecords.AnyAsync(item => item.Key == rollbackKey));
        Assert.Equal(AssignmentVersionStatuses.Current,
            (await context.AssignmentVersions.SingleAsync(item => item.ObligationId == rollbackObligation)).Status);
    }

    [Fact]
    public async Task ConcurrentCorrectionsCannotCreateTwoCurrentSuccessors()
    {
        var seed = await ResetAsync();
        Guid obligationId;
        Guid evaluationId;
        UserSeed actor;
        UserSeed previous;
        UserSeed first;
        UserSeed second;
        await using (var arrange = CreateContext())
        {
            actor = AddUser(arrange, "CORR-DIR", CanonicalRole.Direction);
            previous = AddUser(arrange, "CORR-OLD", CanonicalRole.Subcoordination);
            first = AddUser(arrange, "CORR-001", CanonicalRole.Subcoordination);
            second = AddUser(arrange, "CORR-002", CanonicalRole.Subcoordination);
            await arrange.SaveChangesAsync();
            obligationId = await AddObligationAsync(arrange, seed, "correction-concurrent");
            AddAutomatic(arrange, obligationId, previous.Person.Id);
            evaluationId = AddEvaluation(arrange, obligationId, seed.PolicyId, first.Person, second.Person).Id;
            await arrange.SaveChangesAsync();
        }

        var commands = new[]
        {
            new CorrectAssignmentCommand(actor.User.Id, Guid.CreateVersion7(), Guid.CreateVersion7(), obligationId,
                first.Person.Id, evaluationId, "Primera corrección concurrente", 1),
            new CorrectAssignmentCommand(actor.User.Id, Guid.CreateVersion7(), Guid.CreateVersion7(), obligationId,
                second.Person.Id, evaluationId, "Segunda corrección concurrente", 1),
        };
        var outcomes = await Task.WhenAll(commands.Select(async command =>
        {
            try
            {
                await using var context = CreateContext();
                return (await Service(context).CorrectAsync(command)).Result;
            }
            catch (AssignmentCorrectionVersionConflictException)
            {
                return AssignmentCorrectionErrors.VersionConflict;
            }
        }));

        Assert.Contains(AssignmentCorrectionResults.Created, outcomes);
        Assert.Contains(AssignmentCorrectionErrors.VersionConflict, outcomes);
        await using var verify = CreateContext();
        Assert.Single(await verify.AssignmentVersions.AsNoTracking()
            .Where(item => item.ObligationId == obligationId && item.Status == AssignmentVersionStatuses.Current)
            .ToListAsync());
    }

    private async Task<Seed> ResetAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
        var system = AddUser(context, "CORR-SYSTEM", CanonicalRole.Direction);
        var releaseId = Guid.CreateVersion7();
        var release = new ConfigurationRelease(releaseId, BranchScope.LorettaId);
        release.ApplyPublished(Published(releaseId), 1, system.User.Id, Now.AddDays(-30));
        var task = TaskDefinitionCatalog.Require("TAR-0008");
        var taskVersionId = Guid.CreateVersion7();
        using var payload = JsonDocument.Parse("{}");
        var taskVersion = new TaskDefinitionVersion(taskVersionId, task.Id, 1, 1, payload, releaseId);
        taskVersion.ApplyPublished(Published(taskVersionId), true);
        var policyId = Guid.CreateVersion7();
        var policy = new EligibilityPolicyVersion(
            policyId, task.Id, taskVersionId, releaseId, null, 1,
            CanonicalRole.Subcoordination, true, null);
        policy.ApplyPublished(Published(policyId));
        var ruleId = Guid.CreateVersion7();
        using var schedule = JsonDocument.Parse("null");
        var rule = new ActivationRuleVersion(
            ruleId, task.Id, taskVersionId, releaseId, null, 1,
            ActivationModes.Manual, schedule.RootElement, ActivationOriginSchemas.ManualReference);
        rule.ApplyPublished(Published(ruleId));
        var week = WeekContract.Calculate(2026, 36);
        var period = new WeekPeriod(Guid.CreateVersion7(), BranchScope.LorettaId, 2026, 36,
            week.StartsOn, week.EndsOn, WeekContract.Current);
        context.ConfigurationReleases.Add(release);
        context.TaskDefinitionVersions.Add(taskVersion);
        context.EligibilityPolicyVersions.Add(policy);
        context.ActivationRuleVersions.Add(rule);
        context.WeekPeriods.Add(period);
        await context.SaveChangesAsync();
        return new Seed(system.User.Id, taskVersionId, policyId, ruleId, period.Id);
    }

    private static UserSeed AddUser(SgolDbContext context, string code, string role, string? position = null)
    {
        var person = new Person
        {
            Id = Guid.CreateVersion7(),
            StableCode = code,
            DisplayName = $"Persona {code}",
            CreatedAt = Now.AddDays(-30),
        };
        var employment = new EmploymentVersion(
            Guid.CreateVersion7(), person.Id, BranchScope.LorettaId, EmploymentStatus.Active,
            Now.AddDays(-30), positionText: position);
        var user = new AppUser
        {
            Id = Guid.CreateVersion7(),
            PersonId = person.Id,
            Status = AccountStatus.Active,
            MustChangePassword = false,
            MfaEnrolledAt = Now.AddDays(-30),
            SecurityStamp = $"synthetic-{code}",
        };
        var assignment = new RoleAssignmentVersion
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            BranchId = BranchScope.LorettaId,
            RoleCode = role,
            Status = RoleAssignmentStatus.Active,
            ValidFrom = Now.AddDays(-30),
        };
        var availability = new AvailabilityDayVersion(
            Guid.CreateVersion7(), person.Id, BranchScope.LorettaId, EligibilityDate, true, user.Id);
        context.People.Add(person);
        context.EmploymentVersions.Add(employment);
        context.AppUsers.Add(user);
        context.RoleAssignmentVersions.Add(assignment);
        context.AvailabilityDayVersions.Add(availability);
        return new UserSeed(person, user, employment, assignment, availability);
    }

    private static async Task<Guid> AddObligationAsync(SgolDbContext context, Seed seed, string origin)
    {
        var request = new GenerationRequest(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Hash(origin), seed.RuleId, BranchScope.LorettaId,
            seed.PeriodId, ActivationOriginSchemas.ManualReference, origin, seed.SystemUserId, Now.AddDays(-1));
        context.GenerationRequests.Add(request);
        await context.SaveChangesAsync();
        var obligation = new WorkObligation(
            Guid.CreateVersion7(), seed.TaskVersionId, BranchScope.LorettaId, seed.PeriodId, request.Id, origin);
        context.WorkObligations.Add(obligation);
        await context.SaveChangesAsync();
        request.LinkObligation(obligation.Id);
        await context.SaveChangesAsync();
        return obligation.Id;
    }

    private static Guid AddAutomatic(SgolDbContext context, Guid obligationId, Guid personId)
    {
        var id = Guid.CreateVersion7();
        context.AssignmentVersions.Add(new AssignmentVersion(
            id, obligationId, personId, AssignmentVersionStatuses.Current, AssignmentTypes.Automatic,
            JsonDocument.Parse("{}"), Now.AddMinutes(-10)));
        return id;
    }

    private static EligibilityEvaluation AddEvaluation(
        SgolDbContext context, Guid obligationId, Guid policyId, params Person[] people)
        => AddEvaluation(context, obligationId, policyId, Now.AddMinutes(-1), people);

    private static EligibilityEvaluation AddEvaluation(
        SgolDbContext context,
        Guid obligationId,
        Guid policyId,
        DateTimeOffset evaluatedAt,
        params Person[] people)
    {
        var evaluation = new EligibilityEvaluation(
            Guid.CreateVersion7(), Guid.CreateVersion7(), obligationId, evaluatedAt, EligibilityDate,
            EligibilityDateSources.ManualRequest, policyId, JsonDocument.Parse("{}"),
            EligibilityResults.EligibleCandidates);
        context.EligibilityEvaluations.Add(evaluation);
        foreach (var person in people)
        {
            context.EligibilityCandidates.Add(new EligibilityCandidate(
                evaluation.Id, person.Id, person.StableCode, true, JsonDocument.Parse("[]")));
        }

        return evaluation;
    }

    private static async Task<Counts> CountsAsync(SgolDbContext context) => new(
        await context.WorkObligations.CountAsync(), await context.AssignmentVersions.CountAsync(),
        await context.EligibilityEvaluations.CountAsync(), await context.EligibilityCandidates.CountAsync(),
        await context.People.CountAsync(), await context.EmploymentVersions.CountAsync(),
        await context.AppUsers.CountAsync(), await context.RoleAssignmentVersions.CountAsync(),
        await context.AvailabilityDayVersions.CountAsync(), await context.IdempotencyRecords.CountAsync(),
        await context.AuditEvents.CountAsync());

    private static EfAssignmentCorrectionService Service(SgolDbContext context, IUuidGenerator? generator = null) =>
        new(context, new AuditTransaction(context), new FixedClock(Now), generator ?? new TestUuidGenerator());

    private SgolDbContext CreateContext() => new(new DbContextOptionsBuilder<SgolDbContext>()
        .UseNpgsql(_postgres.GetConnectionString()).Options);

    private static VersionRecord Published(Guid id) => new(
        id, VersionStatuses.Current, Now.AddDays(-30), null, "Configuración sintética", null, 2);
    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed record UserSeed(
        Person Person,
        AppUser User,
        EmploymentVersion Employment,
        RoleAssignmentVersion Role,
        AvailabilityDayVersion Availability);
    private sealed record Seed(Guid SystemUserId, Guid TaskVersionId, Guid PolicyId, Guid RuleId, Guid PeriodId);
    private sealed record Counts(
        int Obligations, int Assignments, int Evaluations, int Candidates, int People, int Employments,
        int AppUsers, int Roles, int Availability, int Idempotency, int Audits);
    private sealed class FixedClock(DateTimeOffset now) : IClock { public DateTimeOffset UtcNow { get; } = now; }
    private sealed class TestUuidGenerator : IUuidGenerator { public Guid NewUuid() => Guid.CreateVersion7(); }
    private sealed class SequenceUuidGenerator(params Guid[] values) : IUuidGenerator
    {
        private readonly Queue<Guid> _values = new(values);
        public Guid NewUuid() => _values.Dequeue();
    }
}
