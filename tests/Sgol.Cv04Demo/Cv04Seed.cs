using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.Assignment.Contracts;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Configuration.Contracts;
using Sgol.Evidence.Contracts;
using Sgol.Execution.Contracts;
using Sgol.Generation.Contracts;
using Sgol.JobInfrastructure;
using Sgol.Notifications.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence.Evidence;
using Sgol.Web.Infrastructure.Persistence.Execution;
using Sgol.Web.Infrastructure.Persistence.Generation;
using Sgol.Web.Infrastructure.Persistence.Validation;
using Sgol.Web.Infrastructure.Persistence.Versioning;
using Sgol.Web.Infrastructure.Evidence;

namespace Sgol.Cv04Demo;

internal sealed record DemoObligation(Guid Id, Guid ResponsibleUserId, Guid ResponsiblePersonId, string RoleCode);

internal sealed class Cv04Seed(Cv04Infrastructure infrastructure)
{
    private DateTimeOffset now;
    private int recurringOriginIndex;

    public int IsoYear { get; private set; }
    public int IsoWeek { get; private set; }

    public async Task SeedBaseConfigurationAsync(Guid directionUserId, CancellationToken cancellationToken)
    {
        now = DateTimeOffset.UtcNow;
        await using var context = infrastructure.CreateContext();
        var clock = new FixedClock(now);
        var generator = new Uuid7Generator(clock);
        var audit = new AuditTransaction(context);
        var releaseService = new EfConfigurationReleaseService(
            context, audit, new VersioningTransaction(context, audit), clock, generator);
        var taskService = new EfTaskDefinitionService(context, audit, releaseService, clock, generator);
        var evidenceService = new EfEvidencePolicyService(context, audit, clock, generator);

        try
        {
            using var empty = JsonDocument.Parse("{}");
            var taskRelease = await releaseService.CreateDraftAsync(NewRelease(directionUserId), cancellationToken);
            foreach (var task in TaskDefinitionCatalog.All)
            {
                await taskService.CreateVersionAsync(new(
                    directionUserId,
                    Guid.CreateVersion7(),
                    Guid.CreateVersion7(),
                    task.TaskCode,
                    taskRelease.Id,
                    1,
                    empty.RootElement), cancellationToken);
            }
            await releaseService.PublishAsync(new(
                directionUserId,
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                taskRelease.Id,
                taskRelease.RowVersion,
                now.AddSeconds(1),
                "TECH-E2E-CV-04 definiciones sintéticas"), cancellationToken);
        }
        catch
        {
            throw new DemoFailureException("SEED", "NONE", "CV04_TASK_CONFIGURATION_FAILED");
        }

        try
        {
            var evidenceRelease = await releaseService.CreateDraftAsync(NewRelease(directionUserId), cancellationToken);
            foreach (var pair in EvidencePolicyCatalog.All)
            {
                await evidenceService.PutAsync(new(
                    directionUserId,
                    Guid.CreateVersion7(),
                    Guid.CreateVersion7(),
                    pair.Key,
                    evidenceRelease.Id,
                    pair.Value.Select(item =>
                        new EvidenceRequirementInput(item.Code, item.Kind, item.ConditionCode)).ToArray(),
                    null), cancellationToken);
            }
            await releaseService.PublishAsync(new(
                directionUserId,
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                evidenceRelease.Id,
                evidenceRelease.RowVersion,
                now.AddSeconds(2),
                "TECH-E2E-CV-04 evidencia sintética"), cancellationToken);
        }
        catch
        {
            throw new DemoFailureException("SEED", "NONE", "CV04_EVIDENCE_CONFIGURATION_FAILED");
        }

        try
        {
            var current = ISOWeek.GetWeekOfYear(DateTime.UtcNow);
            IsoYear = ISOWeek.GetYear(DateTime.UtcNow);
            IsoWeek = current;
            var week = WeekContract.Calculate(IsoYear, IsoWeek);
            if (!await context.WeekPeriods.AnyAsync(item => item.IsoYear == IsoYear && item.IsoWeek == IsoWeek, cancellationToken))
            {
                context.WeekPeriods.Add(new WeekPeriod(
                    Guid.CreateVersion7(), BranchScope.LorettaId, IsoYear, IsoWeek,
                    week.StartsOn, week.EndsOn, WeekContract.Current));
                await context.SaveChangesAsync(cancellationToken);
            }
        }
        catch
        {
            throw new DemoFailureException("SEED", "NONE", "CV04_WEEK_CONFIGURATION_FAILED");
        }
    }

    public async Task SetTextualPositionAsync(Guid personId, string positionCode, CancellationToken cancellationToken)
    {
        await using var context = infrastructure.CreateContext();
        var employment = await context.EmploymentVersions.SingleAsync(
            item => item.PersonId == personId && item.ValidTo == null, cancellationToken);
        context.EmploymentVersions.Add(employment.CreateSuccessor(
            Guid.CreateVersion7(), EmploymentStatus.Active, positionCode, employment.ShiftText,
            DateTimeOffset.UtcNow));
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<DemoObligation> CreateConcludedAsync(
        DemoAccount responsible,
        string taskCode,
        string origin,
        CancellationToken cancellationToken)
    {
        await using var context = infrastructure.CreateContext();
        TaskDefinitionSeed task;
        TaskDefinitionVersion taskVersion;
        EvidencePolicyVersion evidencePolicy;
        ValidationPolicyVersion validationPolicy;
        WeekPeriod period;
        Guid configurationReleaseId;
        task = TaskDefinitionCatalog.Require(taskCode);
        try
        {
            taskVersion = await context.TaskDefinitionVersions.AsNoTracking().SingleAsync(
                item => item.TaskDefinitionId == task.Id && item.Status == VersionStatuses.Current,
                cancellationToken);
        }
        catch
        {
            throw new DemoFailureException("SEED", "NONE", "CV04_TASK_CURRENT_MISSING");
        }
        try
        {
            evidencePolicy = await context.EvidencePolicyVersions.AsNoTracking().SingleAsync(
                item => item.TaskDefinitionId == task.Id && item.Status == VersionStatuses.Current,
                cancellationToken);
        }
        catch
        {
            throw new DemoFailureException("SEED", "NONE", "CV04_EVIDENCE_CURRENT_MISSING");
        }
        try
        {
            validationPolicy = await context.ValidationPolicyVersions.AsNoTracking().SingleAsync(
                item => item.TaskDefinitionId == task.Id && item.Status == VersionStatuses.Current,
                cancellationToken);
        }
        catch
        {
            throw new DemoFailureException("SEED", "NONE", "CV04_VALIDATION_CURRENT_MISSING");
        }
        try
        {
            period = await context.WeekPeriods.AsNoTracking().SingleAsync(
                item => item.IsoYear == IsoYear && item.IsoWeek == IsoWeek,
                cancellationToken);
            configurationReleaseId = await context.ConfigurationReleases.AsNoTracking()
                .Where(item => item.Status == VersionStatuses.Current)
                .Select(item => item.Id)
                .SingleAsync(cancellationToken);
        }
        catch
        {
            throw new DemoFailureException("SEED", "NONE", "CV04_WEEK_CURRENT_MISSING");
        }

        Guid requestId;
        try
        {
            var ruleId = await context.ActivationRuleVersions.AsNoTracking()
                .Where(item => item.TaskDefinitionId == task.Id && item.Status == VersionStatuses.Current)
                .Select(item => item.Id)
                .SingleOrDefaultAsync(cancellationToken);
            if (ruleId == Guid.Empty)
            {
                var recurring = taskCode == "TAR-0005";
                using var schedule = JsonDocument.Parse(recurring
                    ? """{"kind":"WORKING_DAY_WINDOWS","localTimes":["12:00","17:00"],"timeZone":"America/Mexico_City","workingDaysOnly":true}"""
                    : "null");
                var rule = new ActivationRuleVersion(
                    Guid.CreateVersion7(), task.Id, taskVersion.Id, configurationReleaseId, null, 1,
                    recurring ? ActivationModes.Recurring : ActivationModes.Manual,
                    schedule.RootElement,
                    recurring ? ActivationOriginSchemas.WorkingDayWindow : ActivationOriginSchemas.ManualReference);
                var plan = VersioningRules.PlanPublication(
                    rule.ToVersionRecord(), null, [], rule.RowVersion, now.AddSeconds(3),
                    "TECH-E2E-CV-04 activación sintética");
                rule.ApplyPublished(plan.Published);
                ruleId = rule.Id;
                context.ActivationRuleVersions.Add(rule);
            }

            requestId = Guid.CreateVersion7();
            var requestRecurring = taskCode == "TAR-0005";
            var originReference = origin;
            if (requestRecurring)
            {
                var week = WeekContract.Calculate(IsoYear, IsoWeek);
                var sequence = recurringOriginIndex++;
                var localDate = week.StartsOn.AddDays(sequence / 2);
                var localTime = sequence % 2 == 0 ? "12:00" : "17:00";
                originReference = $"{BranchScope.LorettaCode}|{localDate:yyyy-MM-dd}|{localTime}";
            }
            var request = new GenerationRequest(
                requestId,
                Guid.CreateVersion7(),
                Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(origin))),
                ruleId,
                BranchScope.LorettaId,
                period.Id,
                requestRecurring ? ActivationOriginSchemas.WorkingDayWindow : ActivationOriginSchemas.ManualReference,
                originReference,
                requestRecurring ? null : responsible.UserId,
                now.AddSeconds(4));
            context.GenerationRequests.Add(request);
            await context.SaveChangesAsync(cancellationToken);
            context.ChangeTracker.Clear();
        }
        catch
        {
            throw new DemoFailureException("SEED", "NONE", "CV04_OBLIGATION_CONFIGURATION_FAILED");
        }

        var clock = new FixedClock(now.AddSeconds(5));
        WorkObligationDetails materialized;
        try
        {
            materialized = await new EfWorkObligationMaterializer(
                context, new AuditTransaction(context), clock, new Uuid7Generator(clock))
                .MaterializeAsync(new(requestId, Guid.CreateVersion7()), cancellationToken);
        }
        catch
        {
            throw new DemoFailureException("SEED", "NONE", "CV04_MATERIALIZATION_FAILED");
        }

        try
        {
            using var explanation = JsonDocument.Parse("{}");
            var assignment = new AssignmentVersion(
                Guid.CreateVersion7(), materialized.ObligationId, responsible.PersonId,
                AssignmentVersionStatuses.Current, AssignmentTypes.Automatic, explanation, now.AddSeconds(6));
            context.AssignmentVersions.Add(assignment);
            context.InternalNotices.Add(new InternalNotice(
                Guid.CreateVersion7(), responsible.UserId, assignment.Id, now.AddSeconds(6)));
            await context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            throw new DemoFailureException("SEED", "NONE", "CV04_ASSIGNMENT_FAILED");
        }

        try
        {
            await AddStructuredEvidenceAsync(
                context, responsible.UserId, materialized.ObligationId, cancellationToken);
        }
        catch (DemoFailureException)
        {
            throw;
        }
        catch
        {
            throw new DemoFailureException("SEED", "NONE", "CV04_EVIDENCE_SEED_FAILED");
        }

        var conclusionClock = new FixedClock(now.AddSeconds(8));
        try
        {
            await new EfObligationConclusionService(
                context,
                new EfEvidenceConclusionReviewService(context, new Uuid7Generator(conclusionClock)),
                new EfValidationRequirementWriter(context, new Uuid7Generator(conclusionClock)),
                conclusionClock,
                new Uuid7Generator(conclusionClock))
                .ConcludeAsync(new(
                    responsible.UserId,
                    Guid.CreateVersion7(),
                    Guid.CreateVersion7(),
                    materialized.ObligationId,
                    1), cancellationToken);
        }
        catch
        {
            throw new DemoFailureException("SEED", "NONE", "CV04_CONCLUSION_FAILED");
        }

        var stored = await context.WorkObligations.AsNoTracking().SingleAsync(
            item => item.Id == materialized.ObligationId, cancellationToken);
        if (stored.ExecutionStatus != WorkObligationStatuses.Concluded ||
            stored.ValidationPolicyVersionId != validationPolicy.Id)
        {
            throw new DemoFailureException("SEED", "NONE", "CV04_DATABASE_CONTRACT_FAILED");
        }

        return new(stored.Id, responsible.UserId, responsible.PersonId, responsible.RoleCode);
    }

    private static async Task AddStructuredEvidenceAsync(
        SgolDbContext context,
        Guid actor,
        Guid obligationId,
        CancellationToken cancellationToken)
    {
        var requirements = await (
            from obligation in context.WorkObligations.AsNoTracking()
            join requirement in context.EvidenceRequirementVersions.AsNoTracking()
                on obligation.EvidencePolicyVersionId equals requirement.PolicyVersionId
            where obligation.Id == obligationId
            select requirement)
            .OrderBy(item => item.Ordinal)
            .ToListAsync(cancellationToken);
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var generator = new Uuid7Generator(clock);
        var service = new EfEvidenceContributionService(
            context,
            new AuditTransaction(context),
            new UnusedPrivateObjectStorage(),
            new UnusedObjectKeyFactory(),
            new UnusedOutboxWriter(),
            clock,
            generator);
        foreach (var requirement in requirements)
        {
            if (requirement.Kind is not (
                EvidenceRequirementKinds.StructuredData or
                EvidenceRequirementKinds.DigitalRecord or
                EvidenceRequirementKinds.StructuredChecklist))
            {
                throw new DemoFailureException("SEED", "NONE", "CV04_PRECONDITION_FAILED");
            }

            await service.ContributeAsync(new(
                actor,
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                obligationId,
                requirement.RequirementCode,
                null,
                StructuredPayload(requirement.RequirementCode)), cancellationToken);
        }
        context.ChangeTracker.Clear();
    }

    private static JsonDocument StructuredPayload(string code) => JsonDocument.Parse(code switch
    {
        "CALCULO_AVANCE" => """
            {"schemaVersion":1,"expectedTarget":100,"actualSales":100,"sourceReference":"CV04-SYNTHETIC"}
            """,
        "ACCION_O_CONFORMIDAD" => """
            {"schemaVersion":1,"outcome":"CONFORMIDAD","actionDescription":null,"responsiblePersonId":null,"startsAt":null}
            """,
        "LIBERACION" => """
            {"schemaVersion":1,"releasedAt":"2026-09-19T12:00:00Z","releaseReference":"CV04-RELEASE"}
            """,
        "MERCANCIA" => """
            {"schemaVersion":1,"merchandiseReference":"CV04-MERCHANDISE"}
            """,
        "FECHA_HORA" => """
            {"schemaVersion":1,"occurredAt":"2026-09-19T12:00:00Z"}
            """,
        "RETORNO_EXHIBICION" => """
            {"schemaVersion":1,"returnedAt":"2026-09-19T12:15:00Z","returnReference":"CV04-RETURN"}
            """,
        _ => throw new DemoFailureException("SEED", "NONE", "CV04_PRECONDITION_FAILED"),
    });

    private static CreateConfigurationReleaseCommand NewRelease(Guid actor) =>
        new(actor, Guid.CreateVersion7(), Guid.CreateVersion7());

    private sealed class FixedClock(DateTimeOffset value) : IClock
    {
        public DateTimeOffset UtcNow { get; } = value;
    }

    private sealed class UnusedObjectKeyFactory : IEvidenceObjectKeyFactory
    {
        public EvidenceObjectKey Create() => throw new DemoFailureException(
            "SEED", "NONE", "CV04_EVIDENCE_SEED_FAILED");
    }

    private sealed class UnusedOutboxWriter : IOutboxWriter
    {
        public OutboxEvent Enqueue(
            string eventType,
            Guid? aggregateId,
            JsonElement data,
            Guid correlationId,
            DateTimeOffset? availableAt = null) => throw new DemoFailureException(
                "SEED", "NONE", "CV04_EVIDENCE_SEED_FAILED");
    }

    private sealed class UnusedPrivateObjectStorage : IPrivateObjectStorage
    {
        public Task<EvidenceUploadAuthorization> CreateQuarantineUploadAuthorizationAsync(
            EvidenceObjectMetadata metadata, DateTimeOffset expiresAt, CancellationToken cancellationToken) =>
            throw Failure();
        public Task PutQuarantineAsync(EvidenceObjectMetadata metadata, Stream content, CancellationToken cancellationToken) =>
            throw Failure();
        public Task<EvidenceObjectMetadata> GetMetadataAsync(
            EvidenceStorageArea area, EvidenceObjectKey key, CancellationToken cancellationToken) => throw Failure();
        public Task<Stream> OpenReadAsync(
            EvidenceStorageArea area, EvidenceObjectKey key, CancellationToken cancellationToken) => throw Failure();
        public Task PromoteToCleanAsync(EvidenceObjectMetadata expected, CancellationToken cancellationToken) => throw Failure();
        public Task DeleteAsync(EvidenceStorageArea area, EvidenceObjectKey key, CancellationToken cancellationToken) => throw Failure();
        public Task CheckAvailabilityAsync(CancellationToken cancellationToken) => throw Failure();

        private static DemoFailureException Failure() => new(
            "SEED", "NONE", "CV04_EVIDENCE_SEED_FAILED");
    }
}
