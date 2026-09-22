using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.Identity.Contracts;
using Sgol.Validation.Contracts;

namespace Sgol.Cv05Demo;

internal sealed partial class Cv05ScenarioEngine(Cv05Infrastructure infrastructure)
{
    private static readonly string[] FingerprintCounters = ["pending", "concluded", "validated", "nonCompliant"];
    private readonly List<PhaseEvidence> evidence = [];
    private readonly Cv05Seed seed = new(infrastructure);
    private HostedSession direction = null!;
    private HostedSession administration = null!;
    private HostedSession subcoordination = null!;
    private HostedSession floorA = null!;
    private HostedSession floorB = null!;
    private Cv05Obligation floorPending = null!;
    private Cv05Obligation floorReplaced = null!;
    private Cv05Obligation floorNonCompliant = null!;
    private Cv05Obligation subPending = null!;
    private Cv05Obligation unassigned = null!;
    private Guid currentDecisionId;
    private string initialDecisionEtag = null!;

    public IReadOnlyList<PhaseEvidence> CurrentEvidence => evidence;

    public async Task<CycleResult> ExecuteAsync(int cycle, CancellationToken token)
    {
        await ExecutePreRecoveryAsync(token);
        await RunRecoveryAsync(token);
        await RunAsync("S20", "MFA", VerifyHostedAuthenticationFailuresAsync, token);
        var fingerprint = await FingerprintAsync(token);
        return new CycleResult(cycle, evidence.ToArray(), fingerprint);
    }

    internal async Task ExecutePreRecoveryAsync(CancellationToken token)
    {
        await PrepareSeedAsync(token);
        await RunAsync("S01", "INDICATORS", VerifyOperationalIndicatorsAsync, token);
        await RunAsync("S02", "INDICATORS", VerifyNoInflationAsync, token);
        await RunAsync("S03", "INDICATORS", VerifyIndicatorHierarchyAsync, token);
        await RunAsync("S04", "DIRECTION", VerifyDirectionOverviewAsync, token);
        await RunAsync("S05", "DIRECTION", VerifyDirectionBoundaryAsync, token);
        await RunAuditAndIdempotencyAsync(token);
    }

    internal async Task PrepareSeedAsync(CancellationToken token)
    {
        var stage = "AUTH";
        try
        {
            await AuthenticateAsync(token);
            stage = "CONFIGURATION";
            await seed.PrepareConfigurationAsync(direction.Account.UserId, token);
            stage = "PENDING_A";
            floorPending = await seed.CreatePendingAsync(floorA.Account, "TAR-0007", "CV05-FLOOR-PENDING", token);
            stage = "PENDING_B";
            floorReplaced = await seed.CreatePendingAsync(floorB.Account, "TAR-0007", "CV05-FLOOR-REPLACED", token);
            stage = "PENDING_C";
            floorNonCompliant = await seed.CreatePendingAsync(floorA.Account, "TAR-0007", "CV05-FLOOR-NONCOMPLIANT", token);
            stage = "PENDING_SUB";
            subPending = await seed.CreatePendingAsync(subcoordination.Account, "TAR-0008", "CV05-SUB-PENDING", token);
            stage = "PENDING_UNASSIGNED";
            unassigned = await seed.CreatePendingAsync(null, "TAR-0007", "CV05-UNASSIGNED", token);
            stage = "CONCLUDE_B";
            await seed.ConcludeTar0007Async(floorReplaced, floorB, token);
            stage = "CONCLUDE_C";
            await seed.ConcludeTar0007Async(floorNonCompliant, floorA, token);
            stage = "VALIDATION";
            await EstablishDecisionPreconditionsAsync(token);
            evidence.Add(PhaseEvidence.Passed("SEED", "NONE", 0, new Dictionary<string, int>
            {
                ["accounts"] = 5,
                ["obligations"] = 5,
            }));
        }
        catch (Exception exception)
        {
            var failure = exception as DemoFailureException ??
                new DemoFailureException("SEED", "NONE", "CV05_DATABASE_CONTRACT_FAILED");
            failure.Data["Stage"] = stage;
            failure.Data["Category"] = exception.GetType().Name;
            if (exception is DbUpdateException { InnerException: Npgsql.PostgresException databaseError })
            {
                failure.Data["SqlState"] = databaseError.SqlState;
                failure.Data["Constraint"] = databaseError.ConstraintName;
            }
            throw failure;
        }
    }

    private async Task AuthenticateAsync(CancellationToken token)
    {
        var watch = Stopwatch.StartNew();
        var account = new DemoAccount(infrastructure.DirectionUserId, infrastructure.DirectionPersonId,
            infrastructure.DirectionUserName, CanonicalRole.Direction,
            infrastructure.DirectionTemporaryPassword, infrastructure.DirectionNewPassword, "DIR-CV05");
        direction = await HostedAuthenticationClient.CompleteFirstAccessAsync(infrastructure.CreateClient(), account, token);
        administration = await ProvisionAsync("ADMIN-CV05", CanonicalRole.Administration, token);
        subcoordination = await ProvisionAsync("SUB-CV05", CanonicalRole.Subcoordination, token);
        floorA = await ProvisionAsync("PISO-CV05-A", CanonicalRole.SalesFloor, token);
        floorB = await ProvisionAsync("PISO-CV05-B", CanonicalRole.SalesFloor, token);
        watch.Stop();
        foreach (var phase in new[] { "CSRF", "LOGIN", "PASSWORD_CHANGE", "MFA" })
        {
            evidence.Add(PhaseEvidence.Passed(phase, "NONE", watch.ElapsedMilliseconds,
                new Dictionary<string, int> { ["accounts"] = 5 }));
        }
    }

    private async Task<HostedSession> ProvisionAsync(string code, string role, CancellationToken token)
    {
        var account = await HostedAuthenticationClient.ProvisionAccountAsync(direction, code, role, token);
        return await HostedAuthenticationClient.CompleteFirstAccessAsync(infrastructure.CreateClient(), account, token);
    }

    private async Task EstablishDecisionPreconditionsAsync(CancellationToken token)
    {
        var initial = await IssueAsync(subcoordination, floorReplaced.Id, ValidationResults.NotFulfilled, token);
        initialDecisionEtag = initial.Etag;
        using (var replacement = await HostedAuthenticationClient.PostAsync(subcoordination.Client,
            $"/api/v1/validation-decisions/{initial.DecisionId:D}/replacements",
            new
            {
                result = ValidationResults.Fulfilled,
                foundation = "Evidencia CV05 vigente.",
                reason = "Sustitución sintética motivada CV05."
            }, subcoordination.Csrf,
            token, Guid.CreateVersion7(), initial.Etag))
        {
            Require(replacement.StatusCode == HttpStatusCode.Created);
            using var document = await HostedAuthenticationClient.ReadJsonAsync(replacement, token);
            currentDecisionId = document.RootElement.GetProperty("data").GetProperty("decision")
                .GetProperty("decisionVersionId").GetGuid();
        }
        await IssueAsync(subcoordination, floorNonCompliant.Id, ValidationResults.NotFulfilled, token);
    }

    private async Task<(Guid DecisionId, string Etag)> IssueAsync(
        HostedSession actor, Guid obligationId, string result, CancellationToken token)
    {
        await using var context = infrastructure.CreateContext();
        var rowVersion = await context.ValidationRequirements.AsNoTracking().Where(item => item.ObligationId == obligationId)
            .Select(item => item.RowVersion).SingleAsync(token);
        using var response = await HostedAuthenticationClient.PostAsync(actor.Client,
            $"/api/v1/obligations/{obligationId:D}/validation-decisions",
            new { result, foundation = "Evidencia CV05 vigente.", escalationReason = (string?)null },
            actor.Csrf, token, Guid.CreateVersion7(), $"\"{rowVersion}\"");
        Require(response.StatusCode == HttpStatusCode.Created);
        using var document = await HostedAuthenticationClient.ReadJsonAsync(response, token);
        var id = document.RootElement.GetProperty("data").GetProperty("decision")
            .GetProperty("decisionVersionId").GetGuid();
        return (id, Etag(response));
    }

    private async Task VerifyOperationalIndicatorsAsync(CancellationToken token)
    {
        using var snapshot = await ReadAsync(subcoordination, "indicators", token);
        RequireCount(snapshot, "pending", 2, 4);
        RequireCount(snapshot, "concluded", 2, 4);
        RequireCount(snapshot, "validated", 2, 4);
        RequireCount(snapshot, "nonCompliant", 1, 4);
        Require(snapshot.RootElement.GetProperty("data").GetProperty("activeLoadByPerson")
            .GetProperty("denominator").GetInt32() == 2);
    }

    private async Task VerifyNoInflationAsync(CancellationToken token)
    {
        using var snapshot = await ReadAsync(direction, "direction/overview", token);
        RequireCount(snapshot, "validated", 2, 5);
        RequireCount(snapshot, "nonCompliant", 1, 5);
        var data = snapshot.RootElement.GetProperty("data");
        Require(!data.TryGetProperty("amount", out _) && !data.TryGetProperty("incentive", out _));
        await using var context = infrastructure.CreateContext();
        var requirementId = await context.ValidationRequirements.AsNoTracking()
            .Where(item => item.ObligationId == floorReplaced.Id).Select(item => item.Id).SingleAsync(token);
        Require(await context.ValidationDecisionVersions.AsNoTracking()
            .CountAsync(item => item.RequirementId == requirementId, token) == 2);
    }

    private async Task VerifyIndicatorHierarchyAsync(CancellationToken token)
    {
        using var floor = await ReadAsync(floorA, "indicators", token);
        RequireCount(floor, "pending", 1, 2);
        RequireCount(floor, "concluded", 1, 2);
        using var admin = await ReadAsync(administration, "indicators", token);
        RequireCount(admin, "pending", 2, 4);
        using var page = await ReadAsync(subcoordination, "indicators", token, "&limit=1");
        RequireCount(page, "pending", 2, 4);
        Require(page.RootElement.GetProperty("meta").GetProperty("count").GetInt32() == 1);
        using var outside = await ReadAsync(floorA, "indicators", token,
            $"&responsiblePersonId={subcoordination.Account.PersonId:D}");
        RequireCount(outside, "pending", 0, 0);
    }

    private async Task VerifyDirectionOverviewAsync(CancellationToken token)
    {
        using var overview = await ReadAsync(direction, "direction/overview", token);
        RequireCount(overview, "pending", 3, 5);
        RequireCount(overview, "concluded", 2, 5);
        Require(overview.RootElement.GetProperty("data").GetProperty("activeLoadByPerson")
            .GetProperty("denominator").GetInt32() == 3);
        await using var context = infrastructure.CreateContext();
        Require(!await context.AssignmentVersions.AsNoTracking()
            .AnyAsync(item => item.ObligationId == unassigned.Id, token));
    }

    private async Task VerifyDirectionBoundaryAsync(CancellationToken token)
    {
        var path = $"/api/v1/direction/overview?isoYear={seed.IsoYear}&isoWeek={seed.IsoWeek}";
        foreach (var actor in new[] { administration, subcoordination, floorA })
        {
            using var denied = await actor.Client.GetAsync(path, token);
            Require(denied.StatusCode == HttpStatusCode.Forbidden);
        }
        using var accepted = await direction.Client.GetAsync(path, token);
        Require(accepted.StatusCode == HttpStatusCode.OK);
        var body = await accepted.Content.ReadAsStringAsync(token);
        Require(!body.Contains("payroll", StringComparison.OrdinalIgnoreCase));
        Require(!body.Contains("incentive", StringComparison.OrdinalIgnoreCase));
        Require(!body.Contains("integrationHealth", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<JsonDocument> ReadAsync(HostedSession actor, string route, CancellationToken token,
        string extra = "")
    {
        using var response = await actor.Client.GetAsync(
            $"/api/v1/{route}?isoYear={seed.IsoYear}&isoWeek={seed.IsoWeek}{extra}", token);
        Require(response.StatusCode == HttpStatusCode.OK);
        return await HostedAuthenticationClient.ReadJsonAsync(response, token);
    }

    private static void RequireCount(JsonDocument document, string name, int count, int denominator)
    {
        var value = document.RootElement.GetProperty("data").GetProperty(name);
        var observed = value.GetProperty("count").GetInt32();
        var baseCount = value.GetProperty("denominator").GetInt32();
        if (observed != count || baseCount != denominator)
        {
            var failure = new DemoFailureException("INDICATORS", "NONE", "CV05_SCENARIO_FAILED");
            failure.Data["Check"] = name;
            failure.Data["Observed"] = observed;
            failure.Data["Denominator"] = baseCount;
            throw failure;
        }
    }

    private async Task RunAsync(string id, string phase, Func<CancellationToken, Task> test, CancellationToken token)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            await test(token);
            evidence.Add(PhaseEvidence.Passed(phase, id, watch.ElapsedMilliseconds));
        }
        catch (DemoFailureException exception)
        {
            var failure = new DemoFailureException(
                DemoContract.Phases.Contains(exception.Phase, StringComparer.Ordinal) ? exception.Phase : phase,
                id, exception.Code, exception.Exit);
            foreach (System.Collections.DictionaryEntry entry in exception.Data)
                failure.Data[entry.Key] = entry.Value;
            throw failure;
        }
        catch
        {
            throw new DemoFailureException(phase, id, "CV05_SCENARIO_FAILED");
        }
    }

    private async Task<string> FingerprintAsync(CancellationToken token)
    {
        using var overview = await ReadAsync(direction, "direction/overview", token);
        var data = overview.RootElement.GetProperty("data");
        var values = string.Join('|', FingerprintCounters
            .Select(name => data.GetProperty(name).GetProperty("count").GetInt32().ToString(
                System.Globalization.CultureInfo.InvariantCulture)));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(values)));
    }

    private static string Etag(HttpResponseMessage response) =>
        response.Headers.ETag?.ToString() ?? throw new DemoFailureException("IDEMPOTENCY", "NONE", "CV05_HTTP_CONTRACT_FAILED");

    private static void Require(bool condition)
    {
        if (!condition)
        {
            throw new DemoFailureException("REPORT", "NONE", "CV05_SCENARIO_FAILED");
        }
    }

}
