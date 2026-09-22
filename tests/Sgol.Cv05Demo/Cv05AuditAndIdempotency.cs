using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Sgol.Configuration.Contracts;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Generation.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Generation;

namespace Sgol.Cv05Demo;

internal sealed partial class Cv05ScenarioEngine
{
    private async Task RunAuditAndIdempotencyAsync(CancellationToken token)
    {
        await RunAsync("S06", "AUDIT", VerifyTraceAsync, token);
        await RunAsync("S07", "AUDIT", VerifyAuditScopeAsync, token);
        await RunAsync("S08", "AUDIT", VerifyAuditDeleteAsync, token);
        await RunAsync("S09", "IDEMPOTENCY", VerifyGenerationReplayAsync, token);
        await RunAsync("S10", "IDEMPOTENCY", VerifyGenerationConflictAsync, token);
        await RunAsync("S11", "IDEMPOTENCY", VerifyAuthorizationAndStaleVersionAsync, token);
        await RunAsync("S12", "IDEMPOTENCY", VerifyConcurrentGenerationAsync, token);
    }

    private static string AuditRange() =>
        $"from={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture))}" +
        $"&to={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(1).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture))}";

    private async Task VerifyTraceAsync(CancellationToken token)
    {
        var check = "status";
        try
        {
            var path = $"/api/v1/audit-events?{AuditRange()}&traceObligationId={floorReplaced.Id:D}&limit=100";
            using var response = await direction.Client.GetAsync(path, token);
            Require(response.StatusCode == HttpStatusCode.OK);
            using var document = await HostedAuthenticationClient.ReadJsonAsync(response, token);
            check = "rows";
            var rows = document.RootElement.GetProperty("data").EnumerateArray().ToArray();
            Require(rows.Length >= 4);
            check = "completeness";
            var completeness = document.RootElement.GetProperty("meta").GetProperty("completeness");
            foreach (var stage in new[] { "configuration", "assignment", "evidence", "validation" })
            {
                if (!completeness.GetProperty(stage).GetBoolean())
                {
                    var incomplete = new DemoFailureException("AUDIT", "NONE", "CV05_SCENARIO_FAILED");
                    incomplete.Data["Check"] = stage;
                    throw incomplete;
                }
            }
            foreach (var row in rows)
            {
                check = "actor";
                var actor = row.GetProperty("actor");
                var actorType = actor.GetProperty("type").GetString();
                var userId = actor.GetProperty("userId");
                Require(actorType == "SYSTEM" && userId.ValueKind == JsonValueKind.Null ||
                    actorType is "APP_USER" or "USER" or "HUMAN" && userId.ValueKind == JsonValueKind.String &&
                    userId.GetGuid() != Guid.Empty);
                check = "time";
                Require(row.GetProperty("occurredAt").GetDateTimeOffset().Offset == TimeSpan.Zero);
                check = "minimization-password";
                Require(!row.GetRawText().Contains("passwordHash", StringComparison.OrdinalIgnoreCase));
                check = "minimization-url";
                foreach (var property in row.EnumerateObject())
                {
                    check = "minimization-url-" + property.Name;
                    Require(!property.Value.GetRawText().Contains("signedUrl", StringComparison.OrdinalIgnoreCase));
                }
            }
            check = "detail";
            var eventId = rows[0].GetProperty("id").GetGuid();
            using var detail = await direction.Client.GetAsync($"/api/v1/audit-events/{eventId:D}", token);
            Require(detail.StatusCode == HttpStatusCode.OK);
            using var detailDocument = await HostedAuthenticationClient.ReadJsonAsync(detail, token);
            Require(detailDocument.RootElement.GetProperty("data").GetProperty("id").GetGuid() == eventId);
        }
        catch (Exception exception)
        {
            var failure = exception as DemoFailureException ??
                new DemoFailureException("AUDIT", "NONE", "CV05_SCENARIO_FAILED");
            if (!failure.Data.Contains("Check")) failure.Data["Check"] = check;
            throw failure;
        }
    }

    private async Task VerifyAuditScopeAsync(CancellationToken token)
    {
        await using var context = infrastructure.CreateContext();
        var before = await context.AuditEvents.AsNoTracking().CountAsync(token);
        var path = $"/api/v1/audit-events?{AuditRange()}&limit=1";
        using var first = await direction.Client.GetAsync(path, token);
        Require(first.StatusCode == HttpStatusCode.OK);
        using var page = await HostedAuthenticationClient.ReadJsonAsync(first, token);
        var cursor = page.RootElement.GetProperty("meta").GetProperty("nextCursor").GetString();
        Require(!string.IsNullOrWhiteSpace(cursor));
        using var second = await direction.Client.GetAsync(path + "&cursor=" + Uri.EscapeDataString(cursor!), token);
        Require(second.StatusCode == HttpStatusCode.OK);
        using var otherActor = await floorA.Client.GetAsync(path + "&cursor=" + Uri.EscapeDataString(cursor!), token);
        Require(otherActor.StatusCode == HttpStatusCode.BadRequest);
        using var hiddenTrace = await floorA.Client.GetAsync(
            $"/api/v1/audit-events?{AuditRange()}&traceObligationId={floorReplaced.Id:D}", token);
        Require(hiddenTrace.StatusCode == HttpStatusCode.NotFound);
        using var ownTrace = await floorB.Client.GetAsync(
            $"/api/v1/audit-events?{AuditRange()}&traceObligationId={floorReplaced.Id:D}", token);
        Require(ownTrace.StatusCode == HttpStatusCode.OK);
        Require(await context.AuditEvents.AsNoTracking().CountAsync(token) == before);
    }

    private async Task VerifyAuditDeleteAsync(CancellationToken token)
    {
        var check = "target";
        try
        {
            await using var context = infrastructure.CreateContext();
            var target = await context.AuditEvents.AsNoTracking().Select(item => item.Id).FirstAsync(token);
            var before = await context.AuditEvents.AsNoTracking().CountAsync(token);
            using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/audit-events/{target:D}");
            using var denied = await direction.Client.SendAsync(request, token);
            check = "status";
            Require(denied.StatusCode == HttpStatusCode.MethodNotAllowed);
            check = "code";
            Require(await HostedAuthenticationClient.ProblemCodeAsync(denied, token) == "AUDIT_NOT_DELETABLE");
            check = "count";
            Require(await context.AuditEvents.AsNoTracking().CountAsync(token) == before + 1);
            check = "security-event";
            Require(await context.AuditEvents.AsNoTracking().CountAsync(item =>
                item.Action == "AUDIT_EVENT_DELETE_ATTEMPTED" && item.ActorUserId == direction.Account.UserId, token) == 1);
            check = "preserved";
            Require(await context.AuditEvents.AsNoTracking().AnyAsync(item => item.Id == target, token));
            using var anonymous = infrastructure.CreateClient();
            using var anonymousDenied = await anonymous.DeleteAsync($"/api/v1/audit-events/{target:D}", token);
            check = "anonymous";
            Require(anonymousDenied.StatusCode == HttpStatusCode.Unauthorized);
            check = "anonymous-no-effect";
            Require(await context.AuditEvents.AsNoTracking().CountAsync(token) == before + 1);
        }
        catch (Exception exception)
        {
            var failure = exception as DemoFailureException ??
                new DemoFailureException("AUDIT", "NONE", "CV05_SCENARIO_FAILED");
            if (!failure.Data.Contains("Check")) failure.Data["Check"] = check;
            throw failure;
        }
    }

    private Guid generationRuleId;
    private Guid generationKey;
    private Guid generationId;
    private object GenerationBody(string origin) => new
    {
        ruleVersionId = generationRuleId,
        branchId = BranchScope.LorettaId,
        periodId = seed.PeriodId,
        originType = ActivationOriginSchemas.ManualReference,
        originReference = origin,
    };

    private async Task PrepareGenerationAsync(CancellationToken token)
    {
        await using var context = infrastructure.CreateContext();
        var definition = Sgol.Configuration.Contracts.TaskDefinitionCatalog.Require("TAR-0008");
        generationRuleId = await context.ActivationRuleVersions.AsNoTracking()
            .Where(item => item.TaskDefinitionId == definition.Id)
            .Select(item => item.Id).SingleAsync(token);
        var rule = await context.ActivationRuleVersions.AsNoTracking()
            .SingleAsync(item => item.Id == generationRuleId, token);
        var existing = await context.EligibilityPolicyVersions.AsNoTracking()
            .AnyAsync(item => item.TaskDefinitionVersionId == rule.TaskDefinitionVersionId &&
                item.Status == VersionStatuses.Current, token);
        if (!existing)
        {
            var releaseId = await context.ConfigurationReleases.AsNoTracking()
                .Where(item => item.Status == VersionStatuses.Current)
                .Select(item => item.Id).SingleAsync(token);
            var policy = new EligibilityPolicyVersion(
                Guid.CreateVersion7(), definition.Id, rule.TaskDefinitionVersionId,
                releaseId, null, 1, CanonicalRole.Subcoordination, true, null);
            var plan = VersioningRules.PlanPublication(
                policy.ToVersionRecord(), null, [], policy.RowVersion,
                DateTimeOffset.UtcNow.AddMinutes(-2), "CV05 elegibilidad sintética");
            policy.ApplyPublished(plan.Published);
            context.EligibilityPolicyVersions.Add(policy);
            await context.SaveChangesAsync(token);
        }
    }

    private async Task VerifyGenerationReplayAsync(CancellationToken token)
    {
        var check = "prepare";
        try
        {
            await PrepareGenerationAsync(token);
            generationKey = Guid.CreateVersion7();
            var body = GenerationBody("CV05-IDEMPOTENCY-ORIGINAL");
            using var first = await HostedAuthenticationClient.PostAsync(subcoordination.Client,
                "/api/v1/generation-requests", body, subcoordination.Csrf, token, generationKey);
            check = "first-status-" + (int)first.StatusCode;
            Require(first.StatusCode == HttpStatusCode.Created);
            using var firstJson = await HostedAuthenticationClient.ReadJsonAsync(first, token);
            check = "first-id";
            generationId = firstJson.RootElement.GetProperty("data").GetProperty("generationRequestId").GetGuid();
            check = "first-result";
            Require(firstJson.RootElement.GetProperty("data").GetProperty("result").GetString() == GenerationRequestResults.Accepted);
            using var replay = await HostedAuthenticationClient.PostAsync(subcoordination.Client,
                "/api/v1/generation-requests", body, subcoordination.Csrf, token, generationKey);
            check = "replay-status";
            Require(replay.StatusCode == first.StatusCode && replay.Headers.Location == first.Headers.Location);
            using var replayJson = await HostedAuthenticationClient.ReadJsonAsync(replay, token);
            check = "replay-id";
            Require(replayJson.RootElement.GetProperty("data").GetProperty("generationRequestId").GetGuid() == generationId);
            check = "replay-result";
            Require(replayJson.RootElement.GetProperty("data").GetProperty("result").GetString() == GenerationRequestResults.Recovered);
            await using var context = infrastructure.CreateContext();
            var clock = new SystemClock();
            check = "materialization";
            var materialized = await new EfWorkObligationMaterializer(context, new AuditTransaction(context),
                clock, new Uuid7Generator(clock)).MaterializeAsync(new(generationId, Guid.CreateVersion7()), token);
            Require(materialized.ObligationId != Guid.Empty);
            check = "generation-count";
            Require(await context.GenerationRequests.AsNoTracking().CountAsync(item => item.Id == generationId, token) == 1);
            check = "obligation-count";
            Require(await context.WorkObligations.AsNoTracking().CountAsync(item =>
                item.GenerationRequestId == generationId, token) == 1);
            check = "audit-count";
            Require(await context.AuditEvents.AsNoTracking().CountAsync(item =>
                item.Action == "GENERATION_REQUEST_ACCEPTED" && item.ResourceId == generationId, token) == 1);
        }
        catch (Exception exception)
        {
            var failure = exception as DemoFailureException ??
                new DemoFailureException("IDEMPOTENCY", "NONE", "CV05_SCENARIO_FAILED");
            if (!failure.Data.Contains("Check")) failure.Data["Check"] = check;
            throw failure;
        }
    }

    private async Task VerifyGenerationConflictAsync(CancellationToken token)
    {
        await using var context = infrastructure.CreateContext();
        var before = await context.GenerationRequests.AsNoTracking().CountAsync(token);
        using var conflict = await HostedAuthenticationClient.PostAsync(subcoordination.Client,
            "/api/v1/generation-requests", GenerationBody("CV05-IDEMPOTENCY-CHANGED"),
            subcoordination.Csrf, token, generationKey);
        Require(conflict.StatusCode == HttpStatusCode.Conflict);
        Require(await HostedAuthenticationClient.ProblemCodeAsync(conflict, token) == "IDEMPOTENCY_CONFLICT");
        Require(await context.GenerationRequests.AsNoTracking().CountAsync(token) == before);
        Require(await context.AuditEvents.AsNoTracking().AnyAsync(item =>
            item.Action == "IDEMPOTENCY_CONFLICT_REJECTED" && item.ActorUserId == subcoordination.Account.UserId, token));
    }

    private async Task VerifyAuthorizationAndStaleVersionAsync(CancellationToken token)
    {
        var check = "unauthorized";
        try
        {
            using var unauthorized = await HostedAuthenticationClient.PostAsync(floorA.Client,
                "/api/v1/generation-requests", GenerationBody("CV05-IDEMPOTENCY-ORIGINAL"), floorA.Csrf, token, generationKey);
            check = "unauthorized-" + (int)unauthorized.StatusCode;
            Require(unauthorized.StatusCode == HttpStatusCode.Forbidden);
            using var stale = await HostedAuthenticationClient.PostAsync(subcoordination.Client,
                $"/api/v1/validation-decisions/{currentDecisionId:D}/replacements",
                new
                {
                    result = Sgol.Validation.Contracts.ValidationResults.Fulfilled,
                    foundation = "Evidencia CV05 vigente.",
                    reason = "Sustitución sintética repetida CV05."
                },
                subcoordination.Csrf, token, Guid.CreateVersion7(), initialDecisionEtag);
            check = "stale-" + (int)stale.StatusCode;
            Require(stale.StatusCode == HttpStatusCode.PreconditionFailed);
            await using var context = infrastructure.CreateContext();
            check = "decision-count";
            Require(await context.ValidationDecisionVersions.AsNoTracking().CountAsync(item =>
                item.RequirementId == context.ValidationRequirements.Where(requirement =>
                    requirement.ObligationId == floorReplaced.Id).Select(requirement => requirement.Id).Single(), token) == 2);
        }
        catch (Exception exception)
        {
            var failure = exception as DemoFailureException ??
                new DemoFailureException("IDEMPOTENCY", "NONE", "CV05_SCENARIO_FAILED");
            if (!failure.Data.Contains("Check")) failure.Data["Check"] = check;
            throw failure;
        }
    }

    private async Task VerifyConcurrentGenerationAsync(CancellationToken token)
    {
        var key = Guid.CreateVersion7();
        var body = GenerationBody("CV05-IDEMPOTENCY-CONCURRENT");
        var calls = Enumerable.Range(0, 2).Select(_ => HostedAuthenticationClient.PostAsync(
            subcoordination.Client, "/api/v1/generation-requests", body, subcoordination.Csrf, token, key));
        var responses = await Task.WhenAll(calls);
        try
        {
            Require(responses.All(item => item.StatusCode == HttpStatusCode.Created));
            using var first = await HostedAuthenticationClient.ReadJsonAsync(responses[0], token);
            using var second = await HostedAuthenticationClient.ReadJsonAsync(responses[1], token);
            var id = first.RootElement.GetProperty("data").GetProperty("generationRequestId").GetGuid();
            Require(second.RootElement.GetProperty("data").GetProperty("generationRequestId").GetGuid() == id);
            await using var context = infrastructure.CreateContext();
            Require(await context.GenerationRequests.AsNoTracking().CountAsync(item => item.Id == id, token) == 1);
            Require(await context.AuditEvents.AsNoTracking().CountAsync(item =>
                item.Action == "GENERATION_REQUEST_ACCEPTED" && item.ResourceId == id, token) == 1);
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
        }
    }
}
