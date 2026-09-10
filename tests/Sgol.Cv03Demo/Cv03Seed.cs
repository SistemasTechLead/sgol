using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Sgol.Assignment.Contracts;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Configuration.Contracts;
using Sgol.Evidence.Contracts;
using Sgol.Generation.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Notifications.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;

namespace Sgol.Cv03Demo;

internal sealed record SeededActor(Guid PersonId, Guid UserId, string Code, string RoleCode);
internal sealed record SeededActors(SeededActor Direction, SeededActor Administration, SeededActor Subcoordination,
    SeededActor ResponsibleA, SeededActor ResponsibleB, SeededActor Outside);
internal sealed record SeededObligation(Guid Id, Guid PolicyId, string TaskCode, Guid AssignmentId, Guid PeriodId);
internal sealed record Cv03SeedData(SeededActors Actors, IReadOnlyDictionary<string, SeededObligation> Obligations,
    Guid PolicyReleaseId);

internal static class Cv03Seed
{
    public static async Task<Cv03SeedData> CreateAsync(Cv03Infrastructure infrastructure, CancellationToken cancellationToken)
    {
        var stage = "CV03_SEED_ACTORS_FAILED";
        try
        {
            var actors = await CreateActorsAsync(infrastructure, cancellationToken);
            stage = "CV03_SEED_CONFIGURATION_FAILED";
            var policyReleaseId = await PublishConfigurationAsync(infrastructure, actors.Direction.UserId, cancellationToken);
            infrastructure.Clock.Set(Cv03Timeline.Now.AddMinutes(3));
            var obligations = new Dictionary<string, SeededObligation>(StringComparer.Ordinal);
            async Task Add(string key, SeededActor actor, string taskCode, int week, DateTimeOffset? dueAt = null)
            {
                stage = $"CV03_SEED_{key}_FAILED";
                obligations[key] = await CreateObligationAsync(
                    infrastructure, actor, taskCode, key, 2026, week, dueAt, cancellationToken);
            }

            await Add("OWN", actors.ResponsibleA, "TAR-0005", 37);
            await Add("FOREIGN", actors.ResponsibleB, "TAR-0005", 37);
            await Add("STRUCTURED", actors.ResponsibleA, "TAR-0005", 37);
            await Add("BINARY", actors.ResponsibleA, "TAR-0018", 37);
            await Add("COMPLETE", actors.ResponsibleA, "TAR-0018", 37);
            await Add("RACE", actors.ResponsibleA, "TAR-0018", 37);
            await Add("INCOMPLETE", actors.ResponsibleA, "TAR-0018", 37);
            await Add("TAR0092_TRUE", actors.ResponsibleA, "TAR-0092", 37);
            await Add("TAR0092_FALSE", actors.ResponsibleA, "TAR-0092", 37);
            await Add("FUTURE", actors.ResponsibleA, "TAR-0005", 38);
            await Add("AVAILABLE", actors.ResponsibleA, "TAR-0005", 37, Cv03Timeline.After);
            await Add("OVERDUE", actors.ResponsibleA, "TAR-0005", 37, Cv03Timeline.Before);

            stage = "CV03_SEED_EVIDENCE_FAILED";
            await AddCompleteEvidenceAsync(infrastructure, obligations["COMPLETE"], actors.ResponsibleA.UserId, cancellationToken);
            await AddCompleteEvidenceAsync(infrastructure, obligations["RACE"], actors.ResponsibleA.UserId, cancellationToken);
            await AddTar0092EvidenceAsync(infrastructure, obligations["TAR0092_TRUE"], actors.ResponsibleA.UserId, true, true, cancellationToken);
            await AddTar0092EvidenceAsync(infrastructure, obligations["TAR0092_FALSE"], actors.ResponsibleA.UserId, false, false, cancellationToken);
            return new(actors, obligations, policyReleaseId);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgres)
        {
            throw new DemoSafetyException(MapConstraint(postgres.ConstraintName, stage));
        }
        catch (DemoSafetyException)
        {
            throw;
        }
        catch
        {
            throw new DemoSafetyException(stage);
        }
    }

    private static async Task<SeededActors> CreateActorsAsync(Cv03Infrastructure infrastructure, CancellationToken cancellationToken)
    {
        await using var scope = infrastructure.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        var direction = AddActor(context, infrastructure, "DIR-CV03", CanonicalRole.Direction);
        var administration = AddActor(context, infrastructure, "ADMIN-CV03", CanonicalRole.Administration);
        var subcoordination = AddActor(context, infrastructure, "SUB-CV03", CanonicalRole.Subcoordination);
        var responsibleA = AddActor(context, infrastructure, "RESP-A-CV03", CanonicalRole.SalesFloor);
        var responsibleB = AddActor(context, infrastructure, "RESP-B-CV03", CanonicalRole.SalesFloor);
        var outside = AddActor(context, infrastructure, "OUTSIDE-CV03", CanonicalRole.SalesFloor);
        await context.SaveChangesAsync(cancellationToken);
        return new(direction, administration, subcoordination, responsibleA, responsibleB, outside);
    }

    private static SeededActor AddActor(SgolDbContext context, Cv03Infrastructure infrastructure, string code, string role)
    {
        var personId = infrastructure.Uuids.NewUuid();
        var userId = infrastructure.Uuids.NewUuid();
        context.People.Add(new Person { Id = personId, StableCode = code, DisplayName = "Persona sintética CV-03", CreatedAt = Cv03Timeline.Before });
        context.EmploymentVersions.Add(new EmploymentVersion(infrastructure.Uuids.NewUuid(), personId, BranchScope.LorettaId,
            EmploymentStatus.Active, Cv03Timeline.Before));
        context.AppUsers.Add(new AppUser
        {
            Id = userId,
            PersonId = personId,
            Status = AccountStatus.Active,
            MustChangePassword = false,
            MfaEnrolledAt = Cv03Timeline.Before,
            SecurityStamp = "cv03-" + code,
        });
        context.IdentityCredentials.Add(new IdentityCredential
        {
            UserId = userId,
            UserName = code.ToLowerInvariant(),
            NormalizedUserName = code,
            PasswordHash = "synthetic-hash-not-a-secret",
        });
        context.RoleAssignmentVersions.Add(new RoleAssignmentVersion
        {
            Id = infrastructure.Uuids.NewUuid(),
            UserId = userId,
            BranchId = BranchScope.LorettaId,
            RoleCode = role,
            Status = RoleAssignmentStatus.Active,
            ValidFrom = Cv03Timeline.Before,
        });
        return new(personId, userId, code, role);
    }

    private static async Task<Guid> PublishConfigurationAsync(Cv03Infrastructure infrastructure, Guid actor, CancellationToken cancellationToken)
    {
        await using var scope = infrastructure.Services.CreateAsyncScope();
        var releaseService = scope.ServiceProvider.GetRequiredService<IConfigurationReleaseService>();
        var taskService = scope.ServiceProvider.GetRequiredService<ITaskDefinitionService>();
        var policyService = scope.ServiceProvider.GetRequiredService<IEvidencePolicyService>();
        var definitions = await releaseService.CreateDraftAsync(new(actor, infrastructure.Uuids.NewUuid(), infrastructure.Uuids.NewUuid()), cancellationToken);
        using var empty = JsonDocument.Parse("{}");
        foreach (var task in TaskDefinitionCatalog.All)
            await taskService.CreateVersionAsync(new(actor, infrastructure.Uuids.NewUuid(), infrastructure.Uuids.NewUuid(), task.TaskCode,
                definitions.Id, 1, empty.RootElement), cancellationToken);
        await releaseService.PublishAsync(new(actor, infrastructure.Uuids.NewUuid(), infrastructure.Uuids.NewUuid(), definitions.Id,
            definitions.RowVersion, Cv03Timeline.Now.AddMinutes(1), "CV-03 synthetic definitions"), cancellationToken);

        var policies = await releaseService.CreateDraftAsync(new(actor, infrastructure.Uuids.NewUuid(), infrastructure.Uuids.NewUuid()), cancellationToken);
        foreach (var pair in EvidencePolicyCatalog.All)
            await policyService.PutAsync(new(actor, infrastructure.Uuids.NewUuid(), infrastructure.Uuids.NewUuid(), pair.Key, policies.Id,
                pair.Value.Select(item => new EvidenceRequirementInput(item.Code, item.Kind, item.ConditionCode)).ToArray(), null), cancellationToken);
        await releaseService.PublishAsync(new(actor, infrastructure.Uuids.NewUuid(), infrastructure.Uuids.NewUuid(), policies.Id,
            policies.RowVersion, Cv03Timeline.Now.AddMinutes(2), "CV-03 synthetic evidence policies"), cancellationToken);
        return policies.Id;
    }

    private static async Task<SeededObligation> CreateObligationAsync(Cv03Infrastructure infrastructure, SeededActor responsible,
        string taskCode, string reference, int isoYear, int isoWeek, DateTimeOffset? dueAt, CancellationToken cancellationToken)
    {
        var stage = "CV03_SEED_OBLIGATION_LOOKUP_FAILED";
        try
        {
            await using var scope = infrastructure.Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
            stage = "CV03_SEED_OBLIGATION_TASK_FAILED";
            var task = TaskDefinitionCatalog.Require(taskCode);
            var taskVersion = await context.TaskDefinitionVersions.AsNoTracking().SingleAsync(item =>
                item.TaskDefinitionId == task.Id && item.Status == VersionStatuses.Current, cancellationToken);
            stage = "CV03_SEED_OBLIGATION_POLICY_FAILED";
            var policy = await context.EvidencePolicyVersions.AsNoTracking().SingleAsync(item =>
                item.TaskDefinitionId == task.Id && item.Status == VersionStatuses.Current, cancellationToken);
            stage = "CV03_SEED_OBLIGATION_RULE_FAILED";
            var activation = ActivationPolicyCatalog.Require(taskCode);
            var currentRules = await context.ActivationRuleVersions.AsNoTracking()
                .Where(item => item.TaskDefinitionId == task.Id && item.Status == VersionStatuses.Current)
                .Take(2)
                .ToListAsync(cancellationToken);
            if (currentRules.Count > 1)
                throw new DemoSafetyException("CV03_SEED_RULE_UNIQUE_FAILED");

            var rule = currentRules.SingleOrDefault();
            if (rule is not null &&
                (rule.TaskDefinitionVersionId != taskVersion.Id || rule.Mode != activation.Mode ||
                 rule.OriginKeySchema != activation.OriginKeySchema))
            {
                throw new DemoSafetyException("CV03_SEED_OBLIGATION_RULE_FAILED");
            }

            if (rule is null)
            {
                using var schedule = activation.Mode == ActivationModes.Recurring
                    ? JsonDocument.Parse("""{"kind":"WORKING_DAY_WINDOWS","workingDaysOnly":true,"localTimes":["12:00","17:00"],"timeZone":"America/Mexico_City"}""")
                    : JsonDocument.Parse("null");
                rule = new ActivationRuleVersion(infrastructure.Uuids.NewUuid(), task.Id, taskVersion.Id, policy.ReleaseId, null, 1,
                    activation.Mode, schedule.RootElement, activation.OriginKeySchema);
                var rulePlan = VersioningRules.PlanPublication(rule.ToVersionRecord(), null, [], rule.RowVersion,
                    Cv03Timeline.Now.AddMinutes(2), "CV-03 synthetic activation");
                rule.ApplyPublished(rulePlan.Published);
                context.ActivationRuleVersions.Add(rule);
            }
            stage = "CV03_SEED_OBLIGATION_PERIOD_FAILED";
            var week = WeekContract.Calculate(isoYear, isoWeek);
            var period = await context.WeekPeriods.SingleOrDefaultAsync(item => item.BranchId == BranchScope.LorettaId &&
                item.IsoYear == isoYear && item.IsoWeek == isoWeek, cancellationToken);
            if (period is null)
            {
                period = new WeekPeriod(infrastructure.Uuids.NewUuid(), BranchScope.LorettaId, isoYear, isoWeek,
                    week.StartsOn, week.EndsOn, WeekContract.Current);
                context.WeekPeriods.Add(period);
            }
            stage = "CV03_SEED_OBLIGATION_RULE_PERSIST_FAILED";
            await context.SaveChangesAsync(cancellationToken);
            context.ChangeTracker.Clear();
            stage = "CV03_SEED_OBLIGATION_REQUEST_FAILED";
            var originReference = activation.OriginKeySchema == ActivationOriginSchemas.WorkingDayWindow
                ? RecurringOriginReference(reference)
                : reference;
            var requestedBy = activation.OriginKeySchema == ActivationOriginSchemas.WorkingDayWindow
                ? (Guid?)null
                : responsible.UserId;
            var request = new GenerationRequest(infrastructure.Uuids.NewUuid(), infrastructure.Uuids.NewUuid(),
                Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(originReference))), rule.Id,
                BranchScope.LorettaId, period.Id, activation.OriginKeySchema, originReference,
                requestedBy, Cv03Timeline.Now.AddMinutes(3));
            context.GenerationRequests.Add(request);
            await context.SaveChangesAsync(cancellationToken);
            context.ChangeTracker.Clear();
            stage = "CV03_SEED_OBLIGATION_MATERIALIZE_FAILED";
            var materializer = scope.ServiceProvider.GetRequiredService<IWorkObligationMaterializer>();
            var obligation = await materializer.MaterializeAsync(new(request.Id, infrastructure.Uuids.NewUuid()), cancellationToken);
            stage = "CV03_SEED_OBLIGATION_ASSIGNMENT_FAILED";
            using var explanation = JsonDocument.Parse("{}");
            var assignment = new AssignmentVersion(infrastructure.Uuids.NewUuid(), obligation.ObligationId, responsible.PersonId,
                AssignmentVersionStatuses.Current, AssignmentTypes.Automatic, explanation, Cv03Timeline.Now.AddMinutes(4));
            context.AssignmentVersions.Add(assignment);
            await scope.ServiceProvider.GetRequiredService<IInternalNoticeWriter>()
                .AddAssignmentNoticeAsync(assignment.Id, responsible.PersonId, Cv03Timeline.Now.AddMinutes(4), cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            if (dueAt.HasValue)
                await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE work_obligation SET due_at = {dueAt.Value} WHERE id = {obligation.ObligationId}", cancellationToken);
            return new(obligation.ObligationId, policy.Id, taskCode, assignment.Id, period.Id);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgres)
        {
            throw new DemoSafetyException(MapConstraint(postgres.ConstraintName, stage));
        }
        catch (DemoSafetyException)
        {
            throw;
        }
        catch
        {
            throw new DemoSafetyException(stage);
        }
    }

    private static string MapConstraint(string? constraintName, string stage)
    {
        if (constraintName?.StartsWith("CK_activation_rule_version", StringComparison.OrdinalIgnoreCase) == true)
            return "CV03_SEED_RULE_CHECK_FAILED";
        if (constraintName?.StartsWith("IX_activation_rule_version", StringComparison.OrdinalIgnoreCase) == true)
            return "CV03_SEED_RULE_UNIQUE_FAILED";
        if (constraintName?.StartsWith("FK_activation_rule_version", StringComparison.OrdinalIgnoreCase) == true)
            return "CV03_SEED_RULE_FK_FAILED";
        if (constraintName?.Contains("week_period", StringComparison.OrdinalIgnoreCase) == true)
            return "CV03_SEED_PERIOD_CONSTRAINT_FAILED";
        return stage;
    }

    private static string RecurringOriginReference(string reference) => reference switch
    {
        "OWN" => "LOR-001|2026-09-07|12:00",
        "FOREIGN" => "LOR-001|2026-09-07|17:00",
        "STRUCTURED" => "LOR-001|2026-09-08|12:00",
        "AVAILABLE" => "LOR-001|2026-09-08|17:00",
        "OVERDUE" => "LOR-001|2026-09-09|12:00",
        "FUTURE" => "LOR-001|2026-09-14|12:00",
        _ => throw new DemoSafetyException("CV03_SEED_OBLIGATION_RULE_FAILED"),
    };

    private static async Task AddCompleteEvidenceAsync(Cv03Infrastructure infrastructure, SeededObligation obligation,
        Guid actor, CancellationToken cancellationToken)
    {
        await using var scope = infrastructure.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        var requirements = await context.EvidenceRequirementVersions.AsNoTracking().Where(item => item.PolicyVersionId == obligation.PolicyId)
            .OrderBy(item => item.Ordinal).ToListAsync(cancellationToken);
        foreach (var requirement in requirements)
        {
            var item = new EvidenceItem(infrastructure.Uuids.NewUuid(), obligation.Id, obligation.PolicyId, requirement.Id,
                requirement.RequirementCode, requirement.Kind, Cv03Timeline.Now.AddMinutes(5));
            context.EvidenceItems.Add(item);
            if (requirement.RequirementCode == "CHECKLIST_COMPLETO")
            {
                context.EvidenceVersions.Add(new EvidenceVersion(infrastructure.Uuids.NewUuid(), item.Id, 1,
                    Payload(requirement.RequirementCode, false), actor, Cv03Timeline.Now.AddMinutes(5)));
            }
            else
            {
                var file = CleanFile(infrastructure, actor, obligation, requirement, item.Id);
                context.FileObjects.Add(file);
                context.EvidenceVersions.Add(new EvidenceVersion(infrastructure.Uuids.NewUuid(), item.Id, 1, file.Id, actor, Cv03Timeline.Now.AddMinutes(8)));
            }
        }
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task AddTar0092EvidenceAsync(Cv03Infrastructure infrastructure, SeededObligation obligation,
        Guid actor, bool difference, bool includePhoto, CancellationToken cancellationToken)
    {
        await using var scope = infrastructure.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        var requirements = await context.EvidenceRequirementVersions.AsNoTracking().Where(item => item.PolicyVersionId == obligation.PolicyId)
            .OrderBy(item => item.Ordinal).ToListAsync(cancellationToken);
        foreach (var requirement in requirements.Where(item => includePhoto || item.RequirementCode != "FOTO_DIFERENCIA_DANO"))
        {
            var item = new EvidenceItem(infrastructure.Uuids.NewUuid(), obligation.Id, obligation.PolicyId, requirement.Id,
                requirement.RequirementCode, requirement.Kind, Cv03Timeline.Now.AddMinutes(5));
            context.EvidenceItems.Add(item);
            if (requirement.RequirementCode == "F_ENT_001")
                context.EvidenceVersions.Add(new EvidenceVersion(infrastructure.Uuids.NewUuid(), item.Id, 1,
                    Payload(requirement.RequirementCode, difference), actor, Cv03Timeline.Now.AddMinutes(5)));
            else
            {
                var file = CleanFile(infrastructure, actor, obligation, requirement, item.Id);
                context.FileObjects.Add(file);
                context.EvidenceVersions.Add(new EvidenceVersion(infrastructure.Uuids.NewUuid(), item.Id, 1, file.Id, actor, Cv03Timeline.Now.AddMinutes(8)));
            }
        }
        await context.SaveChangesAsync(cancellationToken);
    }

    internal static JsonDocument Payload(string code, bool flag) => code switch
    {
        "CALCULO_AVANCE" => JsonDocument.Parse("{\"schemaVersion\":1,\"expectedTarget\":100,\"actualSales\":90,\"sourceReference\":\"CV03\"}"),
        "ACCION_O_CONFORMIDAD" => JsonDocument.Parse("{\"schemaVersion\":1,\"outcome\":\"CONFORMIDAD\",\"actionDescription\":null,\"responsiblePersonId\":null,\"startsAt\":null}"),
        "CHECKLIST_COMPLETO" => JsonDocument.Parse("{\"schemaVersion\":1,\"productCorrect\":true,\"zoneAndFamilyCorrect\":true,\"stableFormation\":true,\"labelsVisible\":true,\"alignmentConsistent\":true,\"occupancyJustified\":true,\"clean\":true,\"intact\":true,\"signageCorrect\":true,\"matchesPlanogramOrList\":true}"),
        "F_ENT_001" => JsonDocument.Parse($"{{\"schemaVersion\":1,\"formCode\":\"F-ENT-001\",\"formReference\":\"CV03\",\"completedAt\":\"2026-09-09T19:00:00Z\",\"hasDifference\":{flag.ToString().ToLowerInvariant()},\"hasDamage\":false}}"),
        _ => throw new InvalidOperationException("CV03_STRUCTURED_PAYLOAD_NOT_DEFINED"),
    };

    private static FileObject CleanFile(Cv03Infrastructure infrastructure, Guid actor, SeededObligation obligation,
        EvidenceRequirementVersion requirement, Guid itemId)
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(infrastructure.Uuids.NewUuid().ToByteArray()));
        var photo = requirement.Kind == EvidenceRequirementKinds.Photograph;
        var subtype = requirement.RequirementCode == "DOCUMENTO_RECEPCION" ? "NOTA" : null;
        var file = new FileObject(infrastructure.Uuids.NewUuid(), BranchScope.LorettaId, obligation.Id, obligation.PolicyId,
            requirement.Id, requirement.RequirementCode, requirement.Kind, subtype, $"v1/{hash[..2]}/{hash[2..4]}/{hash}",
            photo ? "synthetic.png" : "synthetic.pdf", photo ? "image/png" : "application/pdf", 128, hash, actor, Cv03Timeline.Now.AddMinutes(5));
        file.ConfirmUpload(Cv03Timeline.Now.AddMinutes(6));
        file.MarkClean(photo ? "image/png" : "application/pdf", "synthetic", Cv03Timeline.Now.AddMinutes(7));
        file.Link(itemId, Cv03Timeline.Now.AddMinutes(8));
        return file;
    }
}
