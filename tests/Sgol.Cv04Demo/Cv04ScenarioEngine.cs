using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.Configuration.Contracts;
using Sgol.Generation.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Validation.Contracts;

namespace Sgol.Cv04Demo;

internal sealed class Cv04ScenarioEngine(Cv04Infrastructure infrastructure)
{
    private readonly List<PhaseEvidence> evidence = [];
    private readonly Cv04Seed seed = new(infrastructure);
    private HostedSession direction = null!;
    private HostedSession administrationA = null!;
    private HostedSession administrationB = null!;
    private HostedSession subcoordinationA = null!;
    private HostedSession subcoordinationB = null!;
    private HostedSession floorA = null!;
    private HostedSession floorB = null!;

    public IReadOnlyList<PhaseEvidence> CurrentEvidence => evidence;

    public async Task<CycleResult> ExecuteAsync(int cycle, CancellationToken cancellationToken)
    {
        try
        {
            await AuthenticateActorsAsync(cancellationToken);
        }
        catch (DemoFailureException)
        {
            throw;
        }
        catch
        {
            throw new DemoFailureException("LOGIN", "NONE", "CV04_HTTP_CONTRACT_FAILED");
        }

        try
        {
            await seed.SeedBaseConfigurationAsync(direction.Account.UserId, cancellationToken);
        }
        catch (DemoFailureException)
        {
            throw;
        }
        catch
        {
            throw new DemoFailureException("SEED", "NONE", "CV04_DATABASE_CONTRACT_FAILED");
        }
        evidence.Add(PhaseEvidence.Passed("SEED", "NONE", 0, new Dictionary<string, int> { ["accounts"] = 7 }));

        await RunAsync("S01", "POLICY", PublishValidationPoliciesAsync, cancellationToken);
        await seed.SetTextualPositionAsync(floorB.Account.PersonId, CanonicalRole.Direction, cancellationToken);

        Obligations obligations;
        try
        {
            obligations = await CreateObligationsAsync(cancellationToken);
        }
        catch (DemoFailureException)
        {
            throw;
        }
        catch
        {
            throw new DemoFailureException("SEED", "NONE", "CV04_DATABASE_CONTRACT_FAILED");
        }
        await RunAsync("S02", "AUTHORIZATION", token => VerifyDeniedAuthorityAsync(obligations, token), cancellationToken);
        var issued = await RunDecisionScenariosAsync(obligations, cancellationToken);
        await RunAsync("S11", "SUPERVISION", token => VerifyAdministrationScopeAsync(obligations, token), cancellationToken);
        await RunAsync("S12", "SUPERVISION", token => VerifySubcoordinationScopeAsync(obligations, token), cancellationToken);
        await RunAsync("S13", "SUPERVISION", VerifyFloorHasNoSupervisionAsync, cancellationToken);
        await RunAsync("S14", "SUPERVISION", token => VerifyFiltersDoNotExpandAsync(obligations, token), cancellationToken);
        await RunAsync("S15", "AUTHORIZATION", token => VerifyAntiIdorAsync(obligations, issued, token), cancellationToken);
        await RunAsync("S16", "SUPERVISION", token => VerifyPendingAsync(obligations, token), cancellationToken);
        await RunAsync("S17", "AUDIT", token => VerifyAuditAndRejectedNoEffectAsync(obligations, token), cancellationToken);
        await RunAsync("S18", "VALIDATION", token => VerifyConcurrentIssueAsync(obligations, token), cancellationToken);
        await RunAsync("S19", "REPLACEMENT", token => VerifyConcurrentReplacementAsync(obligations, token), cancellationToken);
        await RunAsync("S20", "AUTHORIZATION", token => VerifyMissingCookieAsync(obligations, token), cancellationToken);
        await RunAsync("S21", "CSRF", token => VerifyMissingCsrfAsync(obligations, token), cancellationToken);
        await RunAsync("S22", "AUTHORIZATION", token => VerifyMfaAndInvalidationAsync(obligations, token), cancellationToken);

        var fingerprint = await FunctionalFingerprintAsync(cancellationToken);
        return new CycleResult(cycle, evidence.ToArray(), fingerprint);
    }

    private async Task AuthenticateActorsAsync(CancellationToken cancellationToken)
    {
        var watch = Stopwatch.StartNew();
        var directionAccount = new DemoAccount(
            infrastructure.DirectionUserId,
            infrastructure.DirectionPersonId,
            infrastructure.DirectionUserName,
            CanonicalRole.Direction,
            infrastructure.DirectionTemporaryPassword,
            infrastructure.DirectionNewPassword,
            "DIR-CV04");
        direction = await HostedAuthenticationClient.CompleteFirstAccessAsync(
            infrastructure.CreateClient(), directionAccount, cancellationToken);

        administrationA = await ProvisionAndAuthenticateAsync("ADMIN-CV04-A", CanonicalRole.Administration, cancellationToken);
        administrationB = await ProvisionAndAuthenticateAsync("ADMIN-CV04-B", CanonicalRole.Administration, cancellationToken);
        subcoordinationA = await ProvisionAndAuthenticateAsync("SUB-CV04-A", CanonicalRole.Subcoordination, cancellationToken);
        subcoordinationB = await ProvisionAndAuthenticateAsync("SUB-CV04-B", CanonicalRole.Subcoordination, cancellationToken);
        floorA = await ProvisionAndAuthenticateAsync("PISO-CV04-A", CanonicalRole.SalesFloor, cancellationToken);
        floorB = await ProvisionAndAuthenticateAsync("PISO-CV04-B", CanonicalRole.SalesFloor, cancellationToken);
        watch.Stop();

        var counts = new Dictionary<string, int> { ["accounts"] = 7 };
        foreach (var phase in new[] { "CSRF", "LOGIN", "PASSWORD_CHANGE", "MFA_ENROLL", "MFA_VERIFY" })
        {
            evidence.Add(PhaseEvidence.Passed(phase, "NONE", watch.ElapsedMilliseconds, counts));
        }
    }

    private async Task<HostedSession> ProvisionAndAuthenticateAsync(
        string stableCode,
        string role,
        CancellationToken cancellationToken)
    {
        var account = await HostedAuthenticationClient.ProvisionAccountAsync(
            direction, stableCode, role, cancellationToken);
        return await HostedAuthenticationClient.CompleteFirstAccessAsync(
            infrastructure.CreateClient(), account, cancellationToken);
    }

    private async Task PublishValidationPoliciesAsync(CancellationToken cancellationToken)
    {
        Guid releaseId;
        string releaseEtag;
        using (var created = await HostedAuthenticationClient.PostAsync(
            direction.Client,
            "/api/v1/configuration/releases",
            new { },
            direction.Csrf,
            cancellationToken,
            Guid.CreateVersion7()))
        {
            RequirePolicyStatus(created, HttpStatusCode.Created, "CV04_POLICY_RELEASE_CREATE_REJECTED");
            releaseId = await HostedAuthenticationClient.DataGuidAsync(created, "id", cancellationToken);
            releaseEtag = RequireEtag(created);
        }

        foreach (var pair in ValidationPolicyCatalog.All.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            using var response = await HostedAuthenticationClient.PutAsync(
                direction.Client,
                $"/api/v1/task-definitions/{pair.Key}/validation-policy",
                new
                {
                    releaseId,
                    isRequired = true,
                    executorRole = pair.Value.ExecutorRole,
                    validatorRelation = ValidationPolicyValues.ImmediateSuperior,
                    validatorRole = pair.Value.ValidatorRole,
                    allowedResults = ValidationPolicyValues.AllowedResults,
                },
                direction.Csrf,
                cancellationToken,
                Guid.CreateVersion7());
            RequirePolicyStatus(response, HttpStatusCode.Created, "CV04_POLICY_DRAFT_REJECTED");
        }

        DateTimeOffset effectiveFrom;
        await using (var preconditionContext = infrastructure.CreateContext())
        {
            var currentEffectiveFrom = await preconditionContext.ConfigurationReleases.AsNoTracking()
                .Where(item => item.Status == Sgol.BuildingBlocks.Versioning.VersionStatuses.Current)
                .Select(item => item.EffectiveFrom)
                .SingleAsync(cancellationToken)
                ?? throw new DemoFailureException("POLICY", "S01", "CV04_POLICY_PRECONDITION");
            effectiveFrom = DemoContract.NextEffectiveFrom(currentEffectiveFrom, DateTimeOffset.UtcNow);
        }

        using (var published = await HostedAuthenticationClient.PostAsync(
            direction.Client,
            $"/api/v1/configuration/releases/{releaseId:D}/publish",
            new
            {
                effectiveFrom,
                reason = "TECH-E2E-CV-04 política canónica",
            },
            direction.Csrf,
            cancellationToken,
            Guid.CreateVersion7(),
            releaseEtag))
        {
            await RequirePolicyPublicationStatusAsync(published, cancellationToken);
        }

        await using var context = infrastructure.CreateContext();
        var policies = await context.ValidationPolicyVersions.AsNoTracking()
            .Where(item => item.Status == Sgol.BuildingBlocks.Versioning.VersionStatuses.Current)
            .ToListAsync(cancellationToken);
        Require(policies.Count == 8);
        Require(ValidationPolicyCatalog.All.All(pair => policies.Any(policy =>
            policy.TaskDefinitionId == TaskDefinitionCatalog.Require(pair.Key).Id &&
            policy.IsRequired &&
            policy.ExecutorRole == pair.Value.ExecutorRole &&
            policy.ValidatorRelation == ValidationPolicyValues.ImmediateSuperior &&
            policy.ValidatorRole == pair.Value.ValidatorRole)));
    }

    private async Task<Obligations> CreateObligationsAsync(CancellationToken cancellationToken)
    {
        var floorFulfilled = await CreateAsync(floorA.Account, "TAR-0007", "CV04-FLOOR-FULFILLED", "S03", cancellationToken);
        var floorIncomplete = await CreateAsync(floorB.Account, "TAR-0007", "CV04-FLOOR-INCOMPLETE", "S04", cancellationToken);
        var floorPending = await CreateAsync(floorA.Account, "TAR-0007", "CV04-FLOOR-PENDING", "S16", cancellationToken);
        var floorConcurrent = await CreateAsync(floorA.Account, "TAR-0007", "CV04-FLOOR-CONCURRENT", "S18", cancellationToken);
        var subNotFulfilled = await CreateAsync(subcoordinationA.Account, "TAR-0005", "CV04-SUB-NOT-FULFILLED", "S05", cancellationToken);
        var subReplacement = await CreateAsync(subcoordinationA.Account, "TAR-0005", "CV04-SUB-REPLACEMENT", "S06", cancellationToken);
        var subPending = await CreateAsync(subcoordinationB.Account, "TAR-0005", "CV04-SUB-PENDING", "S16", cancellationToken);
        var subConcurrent = await CreateAsync(subcoordinationB.Account, "TAR-0005", "CV04-SUB-CONCURRENT", "S19", cancellationToken);
        var administrationSelf = await CreateAsync(administrationA.Account, "TAR-0005", "CV04-ADMIN-SELF", "S09", cancellationToken);
        var directionSelf = await CreateAsync(direction.Account, "TAR-0005", "CV04-DIRECTION-SELF", "S10", cancellationToken);
        return new(
            floorFulfilled, floorIncomplete, floorPending, floorConcurrent,
            subNotFulfilled, subReplacement, subPending, subConcurrent,
            administrationSelf, directionSelf);
    }

    private async Task<DemoObligation> CreateAsync(
        DemoAccount account,
        string taskCode,
        string origin,
        string scenario,
        CancellationToken cancellationToken)
    {
        try
        {
            return await seed.CreateConcludedAsync(account, taskCode, origin, cancellationToken);
        }
        catch (DemoFailureException exception)
        {
            throw new DemoFailureException(exception.Phase, scenario, exception.Code, exception.Exit);
        }
    }

    private async Task VerifyDeniedAuthorityAsync(Obligations obligations, CancellationToken cancellationToken)
    {
        await AssertIssueRejectedAsync(administrationB, obligations.AdministrationSelf.Id, HttpStatusCode.NotFound, cancellationToken);
        await AssertIssueRejectedAsync(floorA, obligations.SubReplacement.Id, HttpStatusCode.Forbidden, cancellationToken);
        await AssertIssueRejectedAsync(floorB, obligations.SubReplacement.Id, HttpStatusCode.Forbidden, cancellationToken);
        await AssertNoDecisionsAsync(obligations.AdministrationSelf.Id, cancellationToken);
        await AssertNoDecisionsAsync(obligations.SubReplacement.Id, cancellationToken);
    }

    private async Task<IssuedDecisions> RunDecisionScenariosAsync(
        Obligations obligations,
        CancellationToken cancellationToken)
    {
        Guid fulfilled = Guid.Empty;
        Guid replacementCurrent = Guid.Empty;

        await RunAsync("S03", "VALIDATION", async token =>
        {
            var issued = await IssueAsync(subcoordinationA, obligations.FloorFulfilled.Id,
                ValidationResults.Fulfilled, null, token);
            fulfilled = issued.DecisionId;
            await AssertDecisionStateAsync(obligations.FloorFulfilled.Id, 1, 1, ValidationResults.Fulfilled, token);
        }, cancellationToken);

        await RunAsync("S04", "VALIDATION", async token =>
        {
            await IssueAsync(subcoordinationB, obligations.FloorIncomplete.Id,
                ValidationResults.Incomplete, null, token);
            await AssertDecisionStateAsync(obligations.FloorIncomplete.Id, 1, 1, ValidationResults.Incomplete, token);
        }, cancellationToken);

        await RunAsync("S05", "VALIDATION", async token =>
        {
            await IssueAsync(administrationA, obligations.SubNotFulfilled.Id,
                ValidationResults.NotFulfilled, null, token);
            await AssertDecisionStateAsync(obligations.SubNotFulfilled.Id, 1, 1, ValidationResults.NotFulfilled, token);
        }, cancellationToken);

        await RunAsync("S06", "REPLACEMENT", async token =>
        {
            var original = await IssueAsync(administrationA, obligations.SubReplacement.Id,
                ValidationResults.Incomplete, null, token);
            var replacement = await ReplaceAsync(administrationA, original.DecisionId, original.Etag,
                ValidationResults.Fulfilled, token);
            replacementCurrent = replacement.DecisionId;
            await AssertDecisionStateAsync(obligations.SubReplacement.Id, 2, 1, ValidationResults.Fulfilled, token);
        }, cancellationToken);

        await RunAsync("S07", "VALIDATION", async token =>
        {
            var before = await SnapshotAsync(obligations.SubReplacement.Id, token);
            using var response = await SendIssueAsync(administrationA, obligations.SubReplacement.Id,
                before.Etag, ValidationResults.NotFulfilled, token);
            RequireStatus(response, HttpStatusCode.Conflict);
            await RequireUnchangedAsync(obligations.SubReplacement.Id, before, token);
        }, cancellationToken);

        await RunAsync("S08", "VALIDATION", async token =>
        {
            var before = await SnapshotAsync(obligations.SubPending.Id, token);
            using var response = await SendIssueAsync(administrationA, obligations.SubPending.Id,
                before.Etag, "DESCONOCIDA", token);
            RequireStatus(response, HttpStatusCode.UnprocessableEntity);
            await RequireUnchangedAsync(obligations.SubPending.Id, before, token);
        }, cancellationToken);

        await RunAsync("S09", "AUTHORIZATION", async token =>
        {
            var before = await SnapshotAsync(obligations.AdministrationSelf.Id, token);
            using var response = await SendIssueAsync(administrationA, obligations.AdministrationSelf.Id,
                before.Etag, ValidationResults.Fulfilled, token);
            RequireStatus(response, HttpStatusCode.UnprocessableEntity);
            await RequireUnchangedAsync(obligations.AdministrationSelf.Id, before, token);
        }, cancellationToken);

        await RunAsync("S10", "VALIDATION", async token =>
        {
            await IssueAsync(direction, obligations.DirectionSelf.Id,
                ValidationResults.Fulfilled, null, token);
            await AssertDecisionStateAsync(obligations.DirectionSelf.Id, 1, 1, ValidationResults.Fulfilled, token);
            await using var context = infrastructure.CreateContext();
            Require(await context.AuditEvents.AsNoTracking().AnyAsync(item =>
                item.Action == "VALIDATION_DIRECTION_SELF_VALIDATED" && item.Outcome == "SUCCESS", token));
        }, cancellationToken);

        return new(fulfilled, replacementCurrent);
    }

    private async Task VerifyAdministrationScopeAsync(Obligations obligations, CancellationToken cancellationToken)
    {
        var rows = await ReadObligationIdsAsync(administrationA, "/api/v1/supervision/obligations", cancellationToken);
        Require(rows.Contains(obligations.SubReplacement.Id));
        Require(rows.Contains(obligations.FloorFulfilled.Id));
        Require(!rows.Contains(obligations.AdministrationSelf.Id));
        Require(!rows.Contains(obligations.DirectionSelf.Id));
        await AssertVisibleRolesAsync(rows, [CanonicalRole.Subcoordination, CanonicalRole.SalesFloor], cancellationToken);
    }

    private async Task VerifySubcoordinationScopeAsync(Obligations obligations, CancellationToken cancellationToken)
    {
        var rows = await ReadObligationIdsAsync(subcoordinationA, "/api/v1/supervision/obligations", cancellationToken);
        Require(rows.Contains(obligations.FloorFulfilled.Id));
        Require(!rows.Contains(obligations.SubReplacement.Id));
        Require(!rows.Contains(obligations.AdministrationSelf.Id));
        Require(!rows.Contains(obligations.DirectionSelf.Id));
        await AssertVisibleRolesAsync(rows, [CanonicalRole.SalesFloor], cancellationToken);
    }

    private async Task VerifyFloorHasNoSupervisionAsync(CancellationToken cancellationToken)
    {
        using var response = await floorA.Client.GetAsync("/api/v1/supervision/obligations", cancellationToken);
        RequireStatus(response, HttpStatusCode.Forbidden);
    }

    private async Task VerifyFiltersDoNotExpandAsync(Obligations obligations, CancellationToken cancellationToken)
    {
        var basePath = $"/api/v1/supervision/obligations?isoYear={seed.IsoYear}&isoWeek={seed.IsoWeek}";
        var filteredDirection = await ReadObligationIdsAsync(administrationA,
            basePath + $"&responsiblePersonId={direction.Account.PersonId:D}", cancellationToken);
        var filteredPeer = await ReadObligationIdsAsync(administrationA,
            basePath + $"&responsiblePersonId={administrationB.Account.PersonId:D}", cancellationToken);
        var filteredLevel = await ReadObligationIdsAsync(subcoordinationA,
            basePath + $"&level={CanonicalRole.Administration}", cancellationToken);
        Require(filteredDirection.Count == 0 && filteredPeer.Count == 0 && filteredLevel.Count == 0);
        Require(!filteredDirection.Contains(obligations.DirectionSelf.Id));
    }

    private async Task VerifyAntiIdorAsync(
        Obligations obligations,
        IssuedDecisions issued,
        CancellationToken cancellationToken)
    {
        foreach (var path in new[]
        {
            $"/api/v1/obligations/{obligations.SubReplacement.Id:D}",
            $"/api/v1/obligations/{obligations.SubReplacement.Id:D}/evidence",
            $"/api/v1/obligations/{obligations.SubReplacement.Id:D}/validations",
        })
        {
            using var response = await floorA.Client.GetAsync(path, cancellationToken);
            Require(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound);
        }

        var before = await SnapshotAsync(obligations.SubReplacement.Id, cancellationToken);
        using var replacement = await HostedAuthenticationClient.PostAsync(
            floorA.Client,
            $"/api/v1/validation-decisions/{issued.ReplacementCurrent:D}/replacements",
            new
            {
                result = ValidationResults.NotFulfilled,
                foundation = "Intento sintético fuera de alcance.",
                reason = "Intento sintético fuera de alcance.",
            },
            floorA.Csrf,
            cancellationToken,
            Guid.CreateVersion7(),
            before.Etag);
        Require(replacement.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound);
        await RequireUnchangedAsync(obligations.SubReplacement.Id, before, cancellationToken);
    }

    private async Task VerifyPendingAsync(Obligations obligations, CancellationToken cancellationToken)
    {
        var adminPending = await ReadObligationIdsAsync(administrationA, "/api/v1/validations/pending", cancellationToken);
        var subPending = await ReadObligationIdsAsync(subcoordinationA, "/api/v1/validations/pending", cancellationToken);
        Require(adminPending.ToHashSet().SetEquals([obligations.SubPending.Id, obligations.FloorPending.Id,
            obligations.SubConcurrent.Id, obligations.FloorConcurrent.Id]));
        Require(subPending.ToHashSet().SetEquals([obligations.FloorPending.Id, obligations.FloorConcurrent.Id]));
    }

    private async Task VerifyAuditAndRejectedNoEffectAsync(Obligations obligations, CancellationToken cancellationToken)
    {
        await using var beforeContext = infrastructure.CreateContext();
        var successBefore = await beforeContext.AuditEvents.AsNoTracking().CountAsync(item =>
            item.Action.StartsWith("VALIDATION_") && item.Outcome == "SUCCESS", cancellationToken);
        var decisionBefore = await beforeContext.ValidationDecisionVersions.AsNoTracking().CountAsync(cancellationToken);
        await beforeContext.DisposeAsync();

        var snapshot = await SnapshotAsync(obligations.SubPending.Id, cancellationToken);
        using var rejected = await SendIssueAsync(floorA, obligations.SubPending.Id,
            snapshot.Etag, ValidationResults.Fulfilled, cancellationToken);
        RequireStatus(rejected, HttpStatusCode.Forbidden);

        await using var after = infrastructure.CreateContext();
        var successAfter = await after.AuditEvents.AsNoTracking().CountAsync(item =>
            item.Action.StartsWith("VALIDATION_") && item.Outcome == "SUCCESS", cancellationToken);
        var decisionAfter = await after.ValidationDecisionVersions.AsNoTracking().CountAsync(cancellationToken);
        Require(successBefore == successAfter && decisionBefore == decisionAfter);
        Require(await after.AuditEvents.AsNoTracking().AnyAsync(item =>
            item.Action == "VALIDATION_DECISION_ISSUED" && item.Outcome == "SUCCESS", cancellationToken));
        Require(await after.WorkObligations.AsNoTracking().AllAsync(item =>
            item.ExecutionStatus == WorkObligationStatuses.Concluded, cancellationToken));
    }

    private async Task VerifyConcurrentIssueAsync(Obligations obligations, CancellationToken cancellationToken)
    {
        var before = await SnapshotAsync(obligations.FloorConcurrent.Id, cancellationToken);
        var first = SendIssueAsync(subcoordinationA, obligations.FloorConcurrent.Id, before.Etag,
            ValidationResults.Incomplete, cancellationToken);
        var second = SendIssueAsync(subcoordinationA, obligations.FloorConcurrent.Id, before.Etag,
            ValidationResults.NotFulfilled, cancellationToken);
        var responses = await Task.WhenAll(first, second);
        try
        {
            Require(responses.Count(item => item.StatusCode == HttpStatusCode.Created) == 1);
            Require(responses.Count(item => item.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed) == 1);
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
        }
        await AssertDecisionStateAsync(obligations.FloorConcurrent.Id, 1, 1, null, cancellationToken);
    }

    private async Task VerifyConcurrentReplacementAsync(Obligations obligations, CancellationToken cancellationToken)
    {
        var original = await IssueAsync(administrationA, obligations.SubConcurrent.Id,
            ValidationResults.Incomplete, null, cancellationToken);
        var first = SendReplacementAsync(administrationA, original.DecisionId, original.Etag,
            ValidationResults.Fulfilled, cancellationToken);
        var second = SendReplacementAsync(administrationA, original.DecisionId, original.Etag,
            ValidationResults.NotFulfilled, cancellationToken);
        var responses = await Task.WhenAll(first, second);
        try
        {
            Require(responses.Count(item => item.StatusCode == HttpStatusCode.Created) == 1);
            Require(responses.Count(item => item.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed) == 1);
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
        }
        await AssertDecisionStateAsync(obligations.SubConcurrent.Id, 2, 1, null, cancellationToken);
    }

    private async Task VerifyMissingCookieAsync(Obligations obligations, CancellationToken cancellationToken)
    {
        var before = await SnapshotAsync(obligations.FloorPending.Id, cancellationToken);
        using var client = infrastructure.CreateClient();
        using var response = await HostedAuthenticationClient.PostAsync(
            client,
            $"/api/v1/obligations/{obligations.FloorPending.Id:D}/validation-decisions",
            IssueBody(ValidationResults.Fulfilled),
            null,
            cancellationToken,
            Guid.CreateVersion7(),
            before.Etag);
        RequireStatus(response, HttpStatusCode.Unauthorized);
        await RequireUnchangedAsync(obligations.FloorPending.Id, before, cancellationToken);
    }

    private async Task VerifyMissingCsrfAsync(Obligations obligations, CancellationToken cancellationToken)
    {
        var before = await SnapshotAsync(obligations.FloorPending.Id, cancellationToken);
        using var response = await HostedAuthenticationClient.PostAsync(
            subcoordinationA.Client,
            $"/api/v1/obligations/{obligations.FloorPending.Id:D}/validation-decisions",
            IssueBody(ValidationResults.Fulfilled),
            null,
            cancellationToken,
            Guid.CreateVersion7(),
            before.Etag);
        RequireStatus(response, HttpStatusCode.BadRequest);
        await RequireUnchangedAsync(obligations.FloorPending.Id, before, cancellationToken);
    }

    private async Task VerifyMfaAndInvalidationAsync(Obligations obligations, CancellationToken cancellationToken)
    {
        var incompleteAccount = await HostedAuthenticationClient.ProvisionAccountAsync(
            direction, "PISO-CV04-MFA-PENDING", CanonicalRole.SalesFloor, cancellationToken);
        var incompleteClient = infrastructure.CreateClient();
        var incompleteCsrf = await HostedAuthenticationClient.GetCsrfAsync(incompleteClient, cancellationToken);
        using (var login = await HostedAuthenticationClient.PostAsync(incompleteClient, "/api/v1/auth/login",
            new { userName = incompleteAccount.UserName, password = incompleteAccount.TemporaryPassword },
            incompleteCsrf, cancellationToken))
        {
            RequireStatus(login, HttpStatusCode.OK);
        }

        var before = await SnapshotAsync(obligations.FloorPending.Id, cancellationToken);
        using (var blocked = await HostedAuthenticationClient.PostAsync(
            incompleteClient,
            $"/api/v1/obligations/{obligations.FloorPending.Id:D}/validation-decisions",
            IssueBody(ValidationResults.Fulfilled),
            incompleteCsrf,
            cancellationToken,
            Guid.CreateVersion7(),
            before.Etag))
        {
            RequireStatus(blocked, HttpStatusCode.Unauthorized);
        }

        using (var reset = await HostedAuthenticationClient.PostAsync(
            direction.Client,
            $"/api/v1/users/{subcoordinationB.Account.UserId:D}/mfa-reset",
            new
            {
                reason = "Invalidación sintética TECH-E2E-CV-04",
                temporaryPassword = Cv04Infrastructure.NewPassword(),
            },
            direction.Csrf,
            cancellationToken,
            Guid.CreateVersion7()))
        {
            RequireStatus(reset, HttpStatusCode.OK);
        }

        using (var invalidated = await HostedAuthenticationClient.PostAsync(
            subcoordinationB.Client,
            $"/api/v1/obligations/{obligations.FloorPending.Id:D}/validation-decisions",
            IssueBody(ValidationResults.Fulfilled),
            subcoordinationB.Csrf,
            cancellationToken,
            Guid.CreateVersion7(),
            before.Etag))
        {
            RequireStatus(invalidated, HttpStatusCode.Unauthorized);
        }
        await RequireUnchangedAsync(obligations.FloorPending.Id, before, cancellationToken);
    }

    private async Task AssertIssueRejectedAsync(
        HostedSession actor,
        Guid obligationId,
        HttpStatusCode expected,
        CancellationToken cancellationToken)
    {
        var snapshot = await SnapshotAsync(obligationId, cancellationToken);
        using var response = await SendIssueAsync(actor, obligationId, snapshot.Etag,
            ValidationResults.Fulfilled, cancellationToken);
        RequireStatus(response, expected);
        await RequireUnchangedAsync(obligationId, snapshot, cancellationToken);
    }

    private async Task<Issued> IssueAsync(
        HostedSession actor,
        Guid obligationId,
        string result,
        string? escalationReason,
        CancellationToken cancellationToken)
    {
        var snapshot = await SnapshotAsync(obligationId, cancellationToken);
        using var response = await HostedAuthenticationClient.PostAsync(
            actor.Client,
            $"/api/v1/obligations/{obligationId:D}/validation-decisions",
            IssueBody(result, escalationReason),
            actor.Csrf,
            cancellationToken,
            Guid.CreateVersion7(),
            snapshot.Etag);
        RequireStatus(response, HttpStatusCode.Created);
        return new(await ReadDecisionIdAsync(response, cancellationToken), RequireEtag(response));
    }

    private static async Task<Issued> ReplaceAsync(
        HostedSession actor,
        Guid decisionId,
        string etag,
        string result,
        CancellationToken cancellationToken)
    {
        using var response = await SendReplacementAsync(actor, decisionId, etag, result, cancellationToken);
        RequireStatus(response, HttpStatusCode.Created);
        return new(await ReadDecisionIdAsync(response, cancellationToken), RequireEtag(response));
    }

    private static Task<HttpResponseMessage> SendIssueAsync(
        HostedSession actor,
        Guid obligationId,
        string etag,
        string result,
        CancellationToken cancellationToken) =>
        HostedAuthenticationClient.PostAsync(
            actor.Client,
            $"/api/v1/obligations/{obligationId:D}/validation-decisions",
            IssueBody(result),
            actor.Csrf,
            cancellationToken,
            Guid.CreateVersion7(),
            etag);

    private static Task<HttpResponseMessage> SendReplacementAsync(
        HostedSession actor,
        Guid decisionId,
        string etag,
        string result,
        CancellationToken cancellationToken) =>
        HostedAuthenticationClient.PostAsync(
            actor.Client,
            $"/api/v1/validation-decisions/{decisionId:D}/replacements",
            new
            {
                result,
                foundation = "La evidencia sintética vigente acredita el resultado.",
                reason = "Sustitución sintética motivada para TECH-E2E-CV-04.",
            },
            actor.Csrf,
            cancellationToken,
            Guid.CreateVersion7(),
            etag);

    private static object IssueBody(string result, string? escalationReason = null) => new
    {
        result,
        foundation = "La evidencia sintética vigente acredita el resultado.",
        escalationReason,
    };

    private async Task<Snapshot> SnapshotAsync(Guid obligationId, CancellationToken cancellationToken)
    {
        await using var context = infrastructure.CreateContext();
        var requirement = await context.ValidationRequirements.AsNoTracking().SingleAsync(
            item => item.ObligationId == obligationId, cancellationToken);
        var decisions = await context.ValidationDecisionVersions.AsNoTracking()
            .Where(item => item.RequirementId == requirement.Id)
            .ToListAsync(cancellationToken);
        var execution = await context.WorkObligations.AsNoTracking()
            .Where(item => item.Id == obligationId)
            .Select(item => item.ExecutionStatus)
            .SingleAsync(cancellationToken);
        var successfulAudits = await context.AuditEvents.AsNoTracking().CountAsync(item =>
            item.ResourceId == requirement.Id && item.Outcome == "SUCCESS", cancellationToken);
        return new(
            $"\"{requirement.RowVersion}\"",
            requirement.Status,
            decisions.Count,
            decisions.Count(item => item.Status == ValidationStatuses.Current),
            execution,
            successfulAudits);
    }

    private async Task RequireUnchangedAsync(
        Guid obligationId,
        Snapshot expected,
        CancellationToken cancellationToken)
    {
        var actual = await SnapshotAsync(obligationId, cancellationToken);
        Require(actual == expected);
    }

    private async Task AssertNoDecisionsAsync(Guid obligationId, CancellationToken cancellationToken)
    {
        var snapshot = await SnapshotAsync(obligationId, cancellationToken);
        Require(snapshot.Decisions == 0 && snapshot.Current == 0 &&
            snapshot.ExecutionStatus == WorkObligationStatuses.Concluded);
    }

    private async Task AssertDecisionStateAsync(
        Guid obligationId,
        int total,
        int current,
        string? result,
        CancellationToken cancellationToken)
    {
        await using var context = infrastructure.CreateContext();
        var requirement = await context.ValidationRequirements.AsNoTracking().SingleAsync(
            item => item.ObligationId == obligationId, cancellationToken);
        var decisions = await context.ValidationDecisionVersions.AsNoTracking()
            .Where(item => item.RequirementId == requirement.Id)
            .OrderBy(item => item.VersionNo)
            .ToListAsync(cancellationToken);
        Require(decisions.Count == total);
        Require(decisions.Count(item => item.Status == ValidationStatuses.Current) == current);
        if (result is not null)
        {
            Require(decisions.Single(item => item.Status == ValidationStatuses.Current).Result == result);
        }
        Require(await context.WorkObligations.AsNoTracking().AnyAsync(item =>
            item.Id == obligationId && item.ExecutionStatus == WorkObligationStatuses.Concluded,
            cancellationToken));
    }

    private static async Task<List<Guid>> ReadObligationIdsAsync(
        HostedSession actor,
        string path,
        CancellationToken cancellationToken)
    {
        using var response = await actor.Client.GetAsync(path, cancellationToken);
        RequireStatus(response, HttpStatusCode.OK);
        using var document = await HostedAuthenticationClient.ReadJsonAsync(response, cancellationToken);
        return document.RootElement.GetProperty("data").EnumerateArray()
            .Select(item => item.GetProperty("obligation").GetProperty("obligationId").GetGuid())
            .ToList();
    }

    private async Task AssertVisibleRolesAsync(
        IReadOnlyCollection<Guid> obligationIds,
        IReadOnlyCollection<string> allowed,
        CancellationToken cancellationToken)
    {
        await using var context = infrastructure.CreateContext();
        var roles = await (
            from obligation in context.WorkObligations.AsNoTracking()
            join assignment in context.AssignmentVersions.AsNoTracking()
                on obligation.Id equals assignment.ObligationId
            join user in context.AppUsers.AsNoTracking()
                on assignment.PersonId equals user.PersonId
            join role in context.RoleAssignmentVersions.AsNoTracking()
                on user.Id equals role.UserId
            where obligationIds.Contains(obligation.Id) &&
                assignment.Status == "VIGENTE" && role.Status == "ACTIVA"
            select role.RoleCode).Distinct().ToListAsync(cancellationToken);
        Require(roles.All(allowed.Contains));
    }

    private static async Task<Guid> ReadDecisionIdAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        using var document = await HostedAuthenticationClient.ReadJsonAsync(response, cancellationToken);
        return document.RootElement.GetProperty("data").GetProperty("decision")
            .GetProperty("decisionVersionId").GetGuid();
    }

    private async Task<string> FunctionalFingerprintAsync(CancellationToken cancellationToken)
    {
        await using var context = infrastructure.CreateContext();
        var values = new[]
        {
            await context.ValidationPolicyVersions.AsNoTracking().CountAsync(item =>
                item.Status == Sgol.BuildingBlocks.Versioning.VersionStatuses.Current, cancellationToken),
            await context.WorkObligations.AsNoTracking().CountAsync(cancellationToken),
            await context.WorkObligations.AsNoTracking().CountAsync(item =>
                item.ExecutionStatus == WorkObligationStatuses.Concluded, cancellationToken),
            await context.ValidationRequirements.AsNoTracking().CountAsync(cancellationToken),
            await context.ValidationDecisionVersions.AsNoTracking().CountAsync(cancellationToken),
            await context.ValidationDecisionVersions.AsNoTracking().CountAsync(item =>
                item.Status == ValidationStatuses.Current, cancellationToken),
            await context.ValidationDecisionVersions.AsNoTracking().CountAsync(item =>
                item.Status == ValidationStatuses.Superseded, cancellationToken),
            await context.AuditEvents.AsNoTracking().CountAsync(item =>
                item.Action.StartsWith("VALIDATION_") && item.Outcome == "SUCCESS", cancellationToken),
        };
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', values))));
    }

    private async Task RunAsync(
        string scenario,
        string phase,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        _ = ScenarioCatalog.Require(scenario);
        var watch = Stopwatch.StartNew();
        try
        {
            await action(cancellationToken);
            watch.Stop();
            evidence.Add(PhaseEvidence.Passed(phase, scenario, watch.ElapsedMilliseconds));
        }
        catch (DemoFailureException exception)
        {
            throw new DemoFailureException(phase, scenario, exception.Code, exception.Exit);
        }
        catch
        {
            throw new DemoFailureException(phase, scenario, "CV04_SCENARIO_FAILED");
        }
    }

    private static void RequireStatus(HttpResponseMessage response, HttpStatusCode expected)
    {
        if (response.StatusCode != expected)
        {
            throw new DemoFailureException(
                "AUTHORIZATION", "NONE", $"CV04_HTTP_STATUS_{(int)response.StatusCode}");
        }
    }

    private static void RequirePolicyStatus(
        HttpResponseMessage response,
        HttpStatusCode expected,
        string rejectionCode)
    {
        if (response.StatusCode != expected)
        {
            throw new DemoFailureException("POLICY", "S01", rejectionCode);
        }
    }

    private static async Task RequirePolicyPublicationStatusAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.OK)
        {
            return;
        }

        if (response.StatusCode != HttpStatusCode.UnprocessableEntity)
        {
            throw new DemoFailureException("POLICY", "S01", "CV04_POLICY_UNKNOWN_REJECTION");
        }

        using var document = await HostedAuthenticationClient.ReadJsonAsync(response, cancellationToken);
        var problemCode = document.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
        var closedCode = problemCode switch
        {
            "VIGENCIA_SOLAPADA" => "CV04_POLICY_OVERLAP",
            "POLITICA_VALIDACION_INCOMPLETA" => "CV04_POLICY_COVERAGE",
            "PUBLICACION_INVALIDA" => "CV04_POLICY_PUBLICATION_INVALID",
            _ => "CV04_POLICY_UNKNOWN_REJECTION",
        };
        throw new DemoFailureException("POLICY", "S01", closedCode);
    }

    private static string RequireEtag(HttpResponseMessage response)
    {
        var etag = response.Headers.ETag?.ToString();
        Require(!string.IsNullOrWhiteSpace(etag));
        return etag!;
    }

    private static void Require(bool condition)
    {
        if (!condition)
        {
            throw new DemoFailureException("AUTHORIZATION", "NONE", "CV04_HTTP_CONTRACT_FAILED");
        }
    }

    private sealed record Obligations(
        DemoObligation FloorFulfilled,
        DemoObligation FloorIncomplete,
        DemoObligation FloorPending,
        DemoObligation FloorConcurrent,
        DemoObligation SubNotFulfilled,
        DemoObligation SubReplacement,
        DemoObligation SubPending,
        DemoObligation SubConcurrent,
        DemoObligation AdministrationSelf,
        DemoObligation DirectionSelf);

    private sealed record Issued(Guid DecisionId, string Etag);
    private sealed record IssuedDecisions(Guid Fulfilled, Guid ReplacementCurrent);
    private sealed record Snapshot(
        string Etag,
        string RequirementStatus,
        int Decisions,
        int Current,
        string ExecutionStatus,
        int SuccessfulAudits);
}
