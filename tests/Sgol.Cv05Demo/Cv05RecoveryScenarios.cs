using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sgol.Continuity.Contracts;

namespace Sgol.Cv05Demo;

internal sealed partial class Cv05ScenarioEngine
{
    private Cv05RecoveryRuntime recovery = null!;
    private Guid matchedReconciliationId;
    private string matchedEtag = null!;

    internal async Task ExecuteRecoveryFocusedAsync(CancellationToken token)
    {
        await PrepareSeedAsync(token);
        await RunRecoveryAsync(token);
    }

    private async Task RunRecoveryAsync(CancellationToken token)
    {
        recovery = new Cv05RecoveryRuntime(infrastructure, direction);
        await RunAsync("S13", "BACKUP", VerifyMatchedRecoveryAsync, token);
        await RunAsync("S14", "RECONCILIATION", VerifyApprovalAsync, token);
        await RunAsync("S15", "RECONCILIATION", VerifyMissingEvidenceAsync, token);
        await RunAsync("S16", "RECONCILIATION", VerifyMissingAuditAsync, token);
        await RunAsync("S17", "RECONCILIATION", VerifyCorruptManifestAsync, token);
        await RunAsync("S18", "RECONCILIATION", VerifyRecoveryAuthorizationAsync, token);
        await RunAsync("S19", "RECONCILIATION", VerifyRecoveryConcurrencyAsync, token);
    }

    private async Task VerifyMatchedRecoveryAsync(CancellationToken token)
    {
        await recovery.PrepareAsync(token);
        var requested = await recovery.RequestAsync("CV05 recuperación sintética positiva", token);
        matchedReconciliationId = requested.Id;
        await recovery.CaptureAsync(requested.Id, token);
        var restored = await recovery.RestoreAsync(requested.Id, token);
        var (exit, details) = await recovery.ReconcileAsync(requested.Id, restored, [0], token);
        using (details)
        {
            Require(exit == 0);
            var data = details.RootElement.GetProperty("data");
            Require(data.GetProperty("status").GetString() == RecoveryReconciliationStatuses.Matched);
            Require(data.GetProperty("differenceCount").GetInt32() == 0);
            Require(data.GetProperty("referenceRootSha256").GetString() ==
                data.GetProperty("actualRootSha256").GetString());
            Require(data.GetProperty("observedRpoSeconds").GetInt64() <= 3600);
            Require(data.GetProperty("observedRtoSeconds").GetInt64() <= 14400);
        }
        using var current = await direction.Client.GetAsync(
            $"/api/v1/continuity/reconciliations/{requested.Id:D}", token);
        Require(current.StatusCode == HttpStatusCode.OK);
        matchedEtag = Etag(current);
    }

    private async Task VerifyApprovalAsync(CancellationToken token)
    {
        var key = Guid.CreateVersion7();
        const string pathPrefix = "/api/v1/continuity/reconciliations/";
        var path = $"{pathPrefix}{matchedReconciliationId:D}/approval";
        var body = new { reason = "CV05 aprobación sintética tras conciliación" };
        using var approved = await HostedAuthenticationClient.PostAsync(direction.Client, path, body,
            direction.Csrf, token, key, matchedEtag);
        Require(approved.StatusCode == HttpStatusCode.OK);
        using var firstBody = await HostedAuthenticationClient.ReadJsonAsync(approved, token);
        Require(firstBody.RootElement.GetProperty("data").GetProperty("status").GetString() ==
            RecoveryReconciliationStatuses.Approved);
        using var replay = await HostedAuthenticationClient.PostAsync(direction.Client, path, body,
            direction.Csrf, token, key, matchedEtag);
        Require(replay.StatusCode == HttpStatusCode.OK && Etag(replay) == Etag(approved));
        using var replayBody = await HostedAuthenticationClient.ReadJsonAsync(replay, token);
        Require(replayBody.RootElement.GetProperty("data").GetProperty("status").GetString() ==
            RecoveryReconciliationStatuses.Approved);
        using var stale = await HostedAuthenticationClient.PostAsync(direction.Client, path, body,
            direction.Csrf, token, Guid.CreateVersion7(), matchedEtag);
        Require(stale.StatusCode == HttpStatusCode.PreconditionFailed);
        Require(await HostedAuthenticationClient.ProblemCodeAsync(stale, token) == "VERSION_CONFLICT");
        await using var context = infrastructure.CreateContext();
        Require(await context.RecoveryReconciliationEvents.AsNoTracking().CountAsync(item =>
            item.ReconciliationId == matchedReconciliationId &&
            item.EventType == RecoveryReconciliationEvents.Approved, token) == 1);
    }

    private async Task VerifyMissingEvidenceAsync(CancellationToken token)
    {
        var requested = await recovery.RequestAsync("CV05 evidencia relacional faltante", token);
        await recovery.CaptureAsync(requested.Id, token);
        var restored = await recovery.RestoreAsync(requested.Id, token);
        await recovery.MutateRestoreAsync("evidence_version", token);
        var (exit, details) = await recovery.ReconcileAsync(requested.Id, restored, [2, 1], token);
        using (details)
        {
            Require(exit is 1 or 2);
            var data = details.RootElement.GetProperty("data");
            var status = data.GetProperty("status").GetString();
            Require(status is RecoveryReconciliationStatuses.Different or RecoveryReconciliationStatuses.Failed);
            if (status == RecoveryReconciliationStatuses.Different)
                Require(data.GetProperty("differences").EnumerateArray().Any(item =>
                    item.GetProperty("kind").GetString() == RecoveryDifferenceKinds.EvidenceMissing));
            Require(data.GetProperty("approvedAt").ValueKind == JsonValueKind.Null);
        }
        await EnsureNotApprovableAsync(requested.Id, token);
    }

    private async Task VerifyMissingAuditAsync(CancellationToken token)
    {
        var check = "request";
        try
        {
            var requested = await recovery.RequestAsync("CV05 auditoría restaurada faltante", token);
            check = "capture";
            await recovery.CaptureAsync(requested.Id, token);
            check = "restore";
            var restored = await recovery.RestoreAsync(requested.Id, token);
            check = "mutate";
            await recovery.MutateRestoreAsync("audit_event", token);
            check = "reconcile";
            var (exit, details) = await recovery.ReconcileAsync(requested.Id, restored, [2, 1], token);
            using (details)
            {
                check = "exit";
                Require(exit is 1 or 2);
                var status = details.RootElement.GetProperty("data").GetProperty("status").GetString();
                check = "status";
                Require(status is RecoveryReconciliationStatuses.Different or RecoveryReconciliationStatuses.Failed);
                if (status == RecoveryReconciliationStatuses.Different)
                {
                    check = "audit-missing-kind";
                    Require(details.RootElement.GetProperty("data").GetProperty("differences").EnumerateArray()
                        .Any(item => item.GetProperty("kind").GetString() == RecoveryDifferenceKinds.AuditMissing));
                }
            }
            await using var origin = infrastructure.CreateContext();
            check = "origin-preserved";
            Require(await origin.AuditEvents.AsNoTracking().AnyAsync(token));
            check = "approval-blocked";
            await EnsureNotApprovableAsync(requested.Id, token);
        }
        catch (Exception exception)
        {
            var failure = exception as DemoFailureException ??
                new DemoFailureException("RECONCILIATION", "NONE", "CV05_SCENARIO_FAILED");
            if (!failure.Data.Contains("Check")) failure.Data["Check"] = check;
            failure.Data["Category"] = exception is Npgsql.PostgresException databaseError
                ? "POSTGRES_" + databaseError.SqlState : exception.GetType().Name;
            throw failure;
        }
    }

    private async Task VerifyCorruptManifestAsync(CancellationToken token)
    {
        var requested = await recovery.RequestAsync("CV05 manifiesto sintético corrupto", token);
        await recovery.CaptureAsync(requested.Id, token);
        var restored = await recovery.RestoreAsync(requested.Id, token);
        await recovery.CorruptReferenceAsync(requested.Id, token);
        var (exit, details) = await recovery.ReconcileAsync(requested.Id, restored, [1], token);
        using (details)
        {
            Require(exit == 1);
            Require(details.RootElement.GetProperty("data").GetProperty("status").GetString() ==
                RecoveryReconciliationStatuses.Failed);
        }
        await using var context = infrastructure.CreateContext();
        Require(await context.RecoveryReconciliationEvents.AsNoTracking().AnyAsync(item =>
            item.ReconciliationId == requested.Id && item.ErrorCode == "REFERENCE_CORRUPT", token));
        await EnsureNotApprovableAsync(requested.Id, token);
    }

    private async Task VerifyRecoveryAuthorizationAsync(CancellationToken token)
    {
        var path = $"/api/v1/continuity/reconciliations/{matchedReconciliationId:D}";
        await using var context = infrastructure.CreateContext();
        var events = await context.RecoveryReconciliationEvents.AsNoTracking()
            .CountAsync(item => item.ReconciliationId == matchedReconciliationId, token);
        var views = await context.AuditEvents.AsNoTracking().CountAsync(item =>
            item.Action == RecoveryReconciliationEvents.Viewed && item.ResourceId == matchedReconciliationId, token);
        using var allowed = await direction.Client.GetAsync(path, token);
        Require(allowed.StatusCode == HttpStatusCode.OK);
        using var data = await HostedAuthenticationClient.ReadJsonAsync(allowed, token);
        Require(data.RootElement.GetProperty("data").GetProperty("status").GetString() ==
            RecoveryReconciliationStatuses.Approved);
        foreach (var actor in new[] { administration, subcoordination, floorA })
        {
            using var denied = await actor.Client.GetAsync(path, token);
            Require(denied.StatusCode == HttpStatusCode.Forbidden);
        }
        using var anonymous = infrastructure.CreateClient();
        using var noCookie = await anonymous.GetAsync(path, token);
        Require(noCookie.StatusCode == HttpStatusCode.Unauthorized);
        Require(await context.RecoveryReconciliationEvents.AsNoTracking()
            .CountAsync(item => item.ReconciliationId == matchedReconciliationId, token) == events);
        Require(await context.AuditEvents.AsNoTracking().CountAsync(item =>
            item.Action == RecoveryReconciliationEvents.Viewed && item.ResourceId == matchedReconciliationId, token)
            == views + 1);
    }

    private async Task VerifyRecoveryConcurrencyAsync(CancellationToken token)
    {
        var requested = await recovery.RequestAsync("CV05 concurrencia de reconciliación", token);
        await recovery.CaptureAsync(requested.Id, token);
        await recovery.CompleteReferenceAgainAsync(requested.Id, token);
        var restored = await recovery.RestoreAsync(requested.Id, token);
        var parallel = await Task.WhenAll(
            recovery.ExecuteReconcileCommandAsync(requested.Id, restored, [0, 1], token),
            recovery.ExecuteReconcileCommandAsync(requested.Id, restored, [0, 1], token));
        Require(parallel.Contains(0));
        Require(await recovery.ExecuteReconcileCommandAsync(requested.Id, restored, [0], token) == 0);
        await using var context = infrastructure.CreateContext();
        Require(await context.RecoveryReconciliationEvents.AsNoTracking().CountAsync(item =>
            item.ReconciliationId == requested.Id &&
            item.EventType == RecoveryReconciliationEvents.Completed, token) == 1);
        using var result = await direction.Client.GetAsync(
            $"/api/v1/continuity/reconciliations/{requested.Id:D}", token);
        Require(result.StatusCode == HttpStatusCode.OK);
        using var body = await HostedAuthenticationClient.ReadJsonAsync(result, token);
        Require(body.RootElement.GetProperty("data").GetProperty("status").GetString() ==
            RecoveryReconciliationStatuses.Matched);
    }

    private async Task EnsureNotApprovableAsync(Guid id, CancellationToken token)
    {
        using var state = await direction.Client.GetAsync($"/api/v1/continuity/reconciliations/{id:D}", token);
        Require(state.StatusCode == HttpStatusCode.OK);
        using var denied = await HostedAuthenticationClient.PostAsync(direction.Client,
            $"/api/v1/continuity/reconciliations/{id:D}/approval",
            new { reason = "CV05 rechazo de resultado no conciliado" }, direction.Csrf, token,
            Guid.CreateVersion7(), Etag(state));
        Require(denied.StatusCode == HttpStatusCode.Conflict);
        Require(await HostedAuthenticationClient.ProblemCodeAsync(denied, token) == "RECONCILIACION_NO_APROBABLE");
    }
}
