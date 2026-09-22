using System.Globalization;
using System.Net;
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
using Sgol.Notifications.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Assignment;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence.Evidence;
using Sgol.Web.Infrastructure.Persistence.Execution;
using Sgol.Web.Infrastructure.Persistence.Generation;
using Sgol.Web.Infrastructure.Persistence.Notifications;
using Sgol.Web.Infrastructure.Persistence.Validation;
using Sgol.Web.Infrastructure.Persistence.Versioning;

namespace Sgol.Cv05Demo;

internal sealed record Cv05Obligation(Guid Id, Guid? ResponsibleUserId, Guid? ResponsiblePersonId, string TaskCode);

internal sealed class Cv05Seed(Cv05Infrastructure infrastructure)
{
    private DateTimeOffset baseline;
    private Guid periodId;

    public int IsoYear { get; private set; }
    public int IsoWeek { get; private set; }
    public Guid PeriodId => periodId;

    public async Task PrepareConfigurationAsync(Guid directionUserId, CancellationToken token)
    {
        var current = DateTimeOffset.UtcNow;
        baseline = current;
        var clock = new FixedClock(baseline);
        var ids = new Uuid7Generator(clock);
        await using var context = infrastructure.CreateContext();
        var audit = new AuditTransaction(context);
        var releases = new EfConfigurationReleaseService(context, audit, new VersioningTransaction(context, audit), clock, ids);
        var definitions = new EfTaskDefinitionService(context, audit, releases, clock, ids);
        var evidence = new EfEvidencePolicyService(context, audit, clock, ids);
        var validation = new EfValidationPolicyService(context, audit, clock, ids);

        using var empty = JsonDocument.Parse("{}");
        var definitionsRelease = await releases.CreateDraftAsync(NewRelease(directionUserId), token);
        foreach (var definition in TaskDefinitionCatalog.All)
        {
            await definitions.CreateVersionAsync(new(directionUserId, Guid.CreateVersion7(), Guid.CreateVersion7(),
                definition.TaskCode, definitionsRelease.Id, 1, empty.RootElement), token);
        }
        await releases.PublishAsync(new(directionUserId, Guid.CreateVersion7(), Guid.CreateVersion7(),
            definitionsRelease.Id, definitionsRelease.RowVersion, baseline.AddMinutes(-5), "CV05 definiciones sintéticas"), token);

        var evidenceRelease = await releases.CreateDraftAsync(NewRelease(directionUserId), token);
        foreach (var policy in EvidencePolicyCatalog.All)
        {
            await evidence.PutAsync(new(directionUserId, Guid.CreateVersion7(), Guid.CreateVersion7(),
                policy.Key, evidenceRelease.Id, policy.Value.Select(item =>
                    new EvidenceRequirementInput(item.Code, item.Kind, item.ConditionCode)).ToArray(), null), token);
        }
        await releases.PublishAsync(new(directionUserId, Guid.CreateVersion7(), Guid.CreateVersion7(),
            evidenceRelease.Id, evidenceRelease.RowVersion, baseline.AddMinutes(-4), "CV05 evidencia sintética"), token);

        var validationRelease = await releases.CreateDraftAsync(NewRelease(directionUserId), token);
        foreach (var policy in ValidationPolicyCatalog.All)
        {
            await validation.PutAsync(new(directionUserId, Guid.CreateVersion7(), Guid.CreateVersion7(),
                policy.Key, validationRelease.Id, true, policy.Value.ExecutorRole,
                ValidationPolicyValues.ImmediateSuperior, policy.Value.ValidatorRole,
                ValidationPolicyValues.AllowedResults, null), token);
        }
        await releases.PublishAsync(new(directionUserId, Guid.CreateVersion7(), Guid.CreateVersion7(),
            validationRelease.Id, validationRelease.RowVersion, baseline.AddMinutes(-3), "CV05 validación sintética"), token);

        var localDate = TimeZoneInfo.ConvertTime(current,
            TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City")).DateTime;
        IsoYear = ISOWeek.GetYear(localDate);
        IsoWeek = ISOWeek.GetWeekOfYear(localDate);
        var week = WeekContract.Calculate(IsoYear, IsoWeek);
        periodId = Guid.CreateVersion7();
        context.WeekPeriods.Add(new WeekPeriod(periodId, BranchScope.LorettaId, IsoYear, IsoWeek,
            week.StartsOn, week.EndsOn, WeekContract.Current));
        await context.SaveChangesAsync(token);
    }

    public async Task<Cv05Obligation> CreatePendingAsync(
        DemoAccount? responsible, string taskCode, string origin, CancellationToken token)
    {
        await using var context = infrastructure.CreateContext();
        var definition = TaskDefinitionCatalog.Require(taskCode);
        var taskVersion = await context.TaskDefinitionVersions.AsNoTracking().SingleAsync(item =>
            item.TaskDefinitionId == definition.Id && item.Status == VersionStatuses.Current, token);
        var evidencePolicy = await context.EvidencePolicyVersions.AsNoTracking().SingleAsync(item =>
            item.TaskDefinitionId == definition.Id && item.Status == VersionStatuses.Current, token);
        var validationPolicy = await context.ValidationPolicyVersions.AsNoTracking().SingleAsync(item =>
            item.TaskDefinitionId == definition.Id && item.Status == VersionStatuses.Current, token);
        var currentRelease = await context.ConfigurationReleases.AsNoTracking()
            .Where(item => item.Status == VersionStatuses.Current)
            .Select(item => item.Id).SingleAsync(token);

        var ruleId = await context.ActivationRuleVersions.AsNoTracking()
            .Where(item => item.TaskDefinitionId == definition.Id && item.Status == VersionStatuses.Current)
            .Select(item => item.Id).SingleOrDefaultAsync(token);
        if (ruleId == Guid.Empty)
        {
            using var noSchedule = JsonDocument.Parse("null");
            var rule = new ActivationRuleVersion(Guid.CreateVersion7(), definition.Id, taskVersion.Id,
                currentRelease, null, 1, ActivationModes.Manual, noSchedule.RootElement,
                ActivationOriginSchemas.ManualReference);
            var plan = VersioningRules.PlanPublication(rule.ToVersionRecord(), null, [], rule.RowVersion,
                baseline.AddMinutes(-2), "CV05 activación sintética");
            rule.ApplyPublished(plan.Published);
            context.ActivationRuleVersions.Add(rule);
            ruleId = rule.Id;
        }

        var requestId = Guid.CreateVersion7();
        var request = new GenerationRequest(requestId, Guid.CreateVersion7(),
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(origin))), ruleId,
            BranchScope.LorettaId, periodId, ActivationOriginSchemas.ManualReference, origin,
            responsible?.UserId ?? infrastructure.DirectionUserId, baseline);
        context.GenerationRequests.Add(request);
        await context.SaveChangesAsync(token);
        context.ChangeTracker.Clear();

        var materializeClock = new FixedClock(baseline);
        var materialized = await new EfWorkObligationMaterializer(context, new AuditTransaction(context),
            materializeClock, new Uuid7Generator(materializeClock))
            .MaterializeAsync(new(requestId, Guid.CreateVersion7()), token);

        if (responsible is not null && origin == "CV05-FLOOR-REPLACED")
        {
            var policy = new EligibilityPolicyVersion(Guid.CreateVersion7(), definition.Id, taskVersion.Id,
                currentRelease, null, 1, responsible.RoleCode, true, null);
            var policyPlan = VersioningRules.PlanPublication(policy.ToVersionRecord(), null, [],
                policy.RowVersion, baseline.AddMinutes(-2), "CV05 elegibilidad sintética");
            policy.ApplyPublished(policyPlan.Published);
            var evaluation = new EligibilityEvaluation(Guid.CreateVersion7(), Guid.CreateVersion7(),
                materialized.ObligationId, baseline,
                DateOnly.FromDateTime(baseline.UtcDateTime), EligibilityDateSources.ManualRequest,
                policy.Id, JsonDocument.Parse("{}"), EligibilityResults.EligibleCandidates);
            context.EligibilityPolicyVersions.Add(policy);
            context.EligibilityEvaluations.Add(evaluation);
            context.EligibilityCandidates.Add(new EligibilityCandidate(evaluation.Id, responsible.PersonId,
                responsible.StableCode, true, JsonDocument.Parse("[]")));
            await context.SaveChangesAsync(token);
            context.ChangeTracker.Clear();
            var assignmentClock = new FixedClock(baseline);
            var ids = new Uuid7Generator(assignmentClock);
            var result = await new EfAutomaticAssignmentService(context, new AuditTransaction(context),
                assignmentClock, ids, new EfInternalNoticeWriter(context, ids))
                .AssignAsync(new(Guid.CreateVersion7(), materialized.ObligationId, evaluation.Id,
                    Guid.CreateVersion7()), token);
            if (result.Result != AutomaticAssignmentResults.Created || result.WinnerPersonId != responsible.PersonId)
                throw new DemoFailureException("SEED", "NONE", "CV05_DATABASE_CONTRACT_FAILED");
        }
        else if (responsible is not null)
        {
            using var explanation = JsonDocument.Parse("{}");
            var assignment = new AssignmentVersion(Guid.CreateVersion7(), materialized.ObligationId,
                responsible.PersonId, AssignmentVersionStatuses.Current, AssignmentTypes.Automatic,
                explanation, baseline);
            context.AssignmentVersions.Add(assignment);
            context.InternalNotices.Add(new InternalNotice(Guid.CreateVersion7(), responsible.UserId,
                assignment.Id, baseline));
            await context.SaveChangesAsync(token);
        }
        var persisted = await context.WorkObligations.AsNoTracking().SingleAsync(item =>
            item.Id == materialized.ObligationId, token);
        if (persisted.EvidencePolicyVersionId != evidencePolicy.Id ||
            persisted.ValidationPolicyVersionId != validationPolicy.Id ||
            persisted.ExecutionStatus != WorkObligationStatuses.Pending)
        {
            throw new DemoFailureException("SEED", "NONE", "CV05_DATABASE_CONTRACT_FAILED");
        }
        return new(persisted.Id, responsible?.UserId, responsible?.PersonId, taskCode);
    }

    public async Task ConcludeTar0007Async(Cv05Obligation obligation, HostedSession actor, CancellationToken token)
    {
        if (obligation.TaskCode != "TAR-0007" || obligation.ResponsibleUserId != actor.Account.UserId)
        {
            throw new DemoFailureException("SEED", "NONE", "CV05_PRECONDITION_FAILED");
        }
        foreach (var requirementCode in new[] { "LIBERACION", "MERCANCIA", "FECHA_HORA", "RETORNO_EXHIBICION" })
        {
            var payload = requirementCode switch
            {
                "LIBERACION" => """{"schemaVersion":1,"releasedAt":"2026-09-22T12:00:00Z","releaseReference":"CV05-LIBERACION"}""",
                "MERCANCIA" => """{"schemaVersion":1,"merchandiseReference":"CV05-MERCANCIA"}""",
                "FECHA_HORA" => """{"schemaVersion":1,"occurredAt":"2026-09-22T12:00:00Z"}""",
                "RETORNO_EXHIBICION" => """{"schemaVersion":1,"returnedAt":"2026-09-22T12:15:00Z","returnReference":"CV05-RETORNO"}""",
                _ => throw new DemoFailureException("SEED", "NONE", "CV05_PRECONDITION_FAILED"),
            };
            using var document = JsonDocument.Parse(payload);
            using var response = await HostedAuthenticationClient.PostAsync(actor.Client,
                $"/api/v1/obligations/{obligation.Id:D}/evidence",
                new { requirementCode, fileId = (Guid?)null, structuredPayload = document.RootElement },
                actor.Csrf, token, Guid.CreateVersion7());
            if (response.StatusCode != HttpStatusCode.Created)
                throw new DemoFailureException("SEED", "NONE", "CV05_HTTP_CONTRACT_FAILED");
        }
        using var conclusionRequest = new HttpRequestMessage(HttpMethod.Post,
            $"/api/v1/obligations/{obligation.Id:D}/conclusion");
        conclusionRequest.Headers.Add("Idempotency-Key", Guid.CreateVersion7().ToString("D"));
        conclusionRequest.Headers.Add("X-CSRF-TOKEN", actor.Csrf);
        conclusionRequest.Headers.TryAddWithoutValidation("If-Match", "\"1\"");
        using var conclusion = await actor.Client.SendAsync(conclusionRequest, token);
        if (conclusion.StatusCode != HttpStatusCode.OK)
            throw new DemoFailureException("SEED", "NONE", "CV05_HTTP_CONTRACT_FAILED");
        await using var context = infrastructure.CreateContext();
        if (await context.WorkObligations.AsNoTracking().Where(item => item.Id == obligation.Id)
            .Select(item => item.ExecutionStatus).SingleAsync(token) != WorkObligationStatuses.Concluded)
        {
            throw new DemoFailureException("SEED", "NONE", "CV05_DATABASE_CONTRACT_FAILED");
        }
    }

    private static CreateConfigurationReleaseCommand NewRelease(Guid actor) =>
        new(actor, Guid.CreateVersion7(), Guid.CreateVersion7());

    private sealed class FixedClock(DateTimeOffset value) : IClock
    {
        public DateTimeOffset UtcNow { get; } = value;
    }
}
