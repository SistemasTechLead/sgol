using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Sgol.Evidence.Contracts;
using Sgol.Execution.Contracts;
using Sgol.Generation.Contracts;
using Sgol.Notifications.Contracts;
using Sgol.Testing;
using Sgol.Web.Infrastructure.Evidence;
using Sgol.Web.Infrastructure.Persistence;

namespace Sgol.Cv03Demo;

internal sealed class Cv03ScenarioEngine(Cv03Infrastructure infrastructure)
{
    private Cv03SeedData? seed;
    private EvidenceDetails? calculationEvidence;
    private ObligationConclusionResult? conclusion;

    public async Task<IReadOnlyDictionary<string, long>> RunAsync(string id, CancellationToken cancellationToken)
    {
        if (seed is null)
        {
            seed = await Cv03Seed.CreateAsync(infrastructure, cancellationToken);
        }
        return id switch
        {
            "S01" => await S01(cancellationToken),
            "S02" => await S02(cancellationToken),
            "S03" => await S03(cancellationToken),
            "S04" => await S04(cancellationToken),
            "S05" => await S05(cancellationToken),
            "S06" => await S06(cancellationToken),
            "S07" => await S07(cancellationToken),
            "S08" => await S08(cancellationToken),
            "S09" => await S09(cancellationToken),
            "S10" => await S10(cancellationToken),
            "S11" => await S11(cancellationToken),
            "S12" => await S12(cancellationToken),
            "S13" => await S13(cancellationToken),
            "S14" => await S14(cancellationToken),
            "S15" => await S15(cancellationToken),
            "S16" => await S16(cancellationToken),
            "S17" => await S17(cancellationToken),
            "S18" => await S18(cancellationToken),
            "S19" => await S19(cancellationToken),
            "S20" => await S20(cancellationToken),
            "S21" => await S21(cancellationToken),
            "S22" => await S22(cancellationToken),
            "S23" => await S23(cancellationToken),
            "S24" => await S24(cancellationToken),
            _ => throw new DemoScenarioAssertionException(),
        };
    }

    private async Task<IReadOnlyDictionary<string, long>> S01(CancellationToken token)
    {
        infrastructure.Actor.CurrentUserId = seed!.Actors.ResponsibleA.UserId;
        using var response = await infrastructure.Client.GetAsync("/api/v1/obligations?limit=100", token);
        Require(response.StatusCode == HttpStatusCode.OK, "CV03_S01_LIST_STATUS");
        var body = await response.Content.ReadAsStringAsync(token);
        Require(body.Contains(seed.Obligations["OWN"].Id.ToString("D"), StringComparison.OrdinalIgnoreCase), "CV03_S01_OWN_MISSING");
        Require(!body.Contains(seed.Obligations["FOREIGN"].Id.ToString("D"), StringComparison.OrdinalIgnoreCase), "CV03_S01_FOREIGN_VISIBLE");
        using var detail = await infrastructure.Client.GetAsync($"/api/v1/obligations/{seed.Obligations["OWN"].Id:D}", token);
        Require(detail.StatusCode == HttpStatusCode.OK, "CV03_S01_DETAIL_STATUS");
        return Facts(("visible", Count(body, "obligationId")));
    }

    private async Task<IReadOnlyDictionary<string, long>> S02(CancellationToken token)
    {
        infrastructure.Actor.CurrentUserId = seed!.Actors.ResponsibleA.UserId;
        using var foreign = await infrastructure.Client.GetAsync($"/api/v1/obligations/{seed.Obligations["FOREIGN"].Id:D}", token);
        using var missing = await infrastructure.Client.GetAsync($"/api/v1/obligations/{infrastructure.Uuids.NewUuid():D}", token);
        Require(foreign.StatusCode == HttpStatusCode.NotFound && missing.StatusCode == HttpStatusCode.NotFound);
        using var foreignJson = JsonDocument.Parse(await foreign.Content.ReadAsStringAsync(token));
        using var missingJson = JsonDocument.Parse(await missing.Content.ReadAsStringAsync(token));
        Require(foreignJson.RootElement.GetProperty("code").GetString() == "OBLIGACION_NO_ENCONTRADA");
        Require(missingJson.RootElement.GetProperty("code").GetString() == "OBLIGACION_NO_ENCONTRADA");
        infrastructure.Actor.CurrentUserId = seed.Actors.Outside.UserId;
        using var outside = await infrastructure.Client.GetAsync($"/api/v1/obligations/{seed.Obligations["OWN"].Id:D}", token);
        Require(outside.StatusCode == HttpStatusCode.NotFound);
        infrastructure.Actor.CurrentUserId = seed.Actors.ResponsibleA.UserId;
        return Facts(("notFound", 3));
    }

    private async Task<IReadOnlyDictionary<string, long>> S03(CancellationToken token)
    {
        await using var scope = infrastructure.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        var obligation = seed!.Obligations["OWN"];
        var frozen = await context.WorkObligations.AsNoTracking().Where(item => item.Id == obligation.Id)
            .Select(item => item.EvidencePolicyVersionId).SingleAsync(token);
        Require(frozen == obligation.PolicyId);
        var requirements = await context.EvidenceRequirementVersions.AsNoTracking()
            .CountAsync(item => item.PolicyVersionId == frozen, token);
        Require(requirements == 2);
        return Facts(("requirements", requirements));
    }

    private async Task<IReadOnlyDictionary<string, long>> S04(CancellationToken token)
    {
        await using var scope = infrastructure.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IEvidenceContributionService>();
        var obligation = seed!.Obligations["STRUCTURED"];
        calculationEvidence = await service.ContributeAsync(new(seed.Actors.ResponsibleA.UserId,
            infrastructure.Uuids.NewUuid(), infrastructure.Uuids.NewUuid(), obligation.Id, "CALCULO_AVANCE", null,
            Cv03Seed.Payload("CALCULO_AVANCE", false)), token);
        var action = await service.ContributeAsync(new(seed.Actors.ResponsibleA.UserId,
            infrastructure.Uuids.NewUuid(), infrastructure.Uuids.NewUuid(), obligation.Id, "ACCION_O_CONFORMIDAD", null,
            Cv03Seed.Payload("ACCION_O_CONFORMIDAD", false)), token);
        Require(calculationEvidence.File is null && action.File is null && calculationEvidence.StructuredPayload is not null);
        return Facts(("structuredVersions", 2));
    }

    private async Task<IReadOnlyDictionary<string, long>> S05(CancellationToken token)
    {
        var stage = "CV03_S05_INTENT_FAILED";
        try
        {
            var obligation = seed!.Obligations["BINARY"];
            var bytes = EvidenceCorpus.Png();
            var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
            EvidenceUploadIntentResult intent;
            await using (var intentScope = infrastructure.Services.CreateAsyncScope())
            {
                var intentService = intentScope.ServiceProvider.GetRequiredService<IEvidenceContributionService>();
                intent = await intentService.CreateUploadIntentAsync(new(seed.Actors.ResponsibleA.UserId,
                    infrastructure.Uuids.NewUuid(), infrastructure.Uuids.NewUuid(), obligation.Id, "FOTOGRAFIA_FINAL",
                    "synthetic.png", "image/png", bytes.Length, hash, null), token);
            }
            stage = "CV03_S05_UPLOAD_FAILED";
            using (var request = new HttpRequestMessage(HttpMethod.Put, intent.Upload.Url))
            {
                request.Content = new ByteArrayContent(bytes);
                request.Content.Headers.ContentType = new(intent.Upload.Headers.ContentType);
                request.Headers.TryAddWithoutValidation("If-None-Match", intent.Upload.Headers.IfNoneMatch);
                request.Headers.TryAddWithoutValidation("x-amz-meta-sgol-sha256", intent.Upload.Headers.Sha256);
                request.Headers.TryAddWithoutValidation("x-amz-meta-sgol-media-type", intent.Upload.Headers.MediaType);
                request.Headers.TryAddWithoutValidation("x-amz-meta-sgol-size-bytes", intent.Upload.Headers.SizeBytes);
                using var upload = await infrastructure.Client.SendAsync(request, token);
                Require(upload.IsSuccessStatusCode, stage);
            }
            stage = "CV03_S05_COMPLETE_FAILED";
            await using (var completeScope = infrastructure.Services.CreateAsyncScope())
            {
                var completeService = completeScope.ServiceProvider.GetRequiredService<IEvidenceContributionService>();
                _ = await completeService.CompleteUploadAsync(new(seed.Actors.ResponsibleA.UserId, infrastructure.Uuids.NewUuid(),
                    infrastructure.Uuids.NewUuid(), intent.FileId), token);
            }
            stage = "CV03_S05_WORKER_FAILED";
            await infrastructure.RunWorkerUntilProcessedAsync(intent.FileId, token);
            await using var statusScope = infrastructure.Services.CreateAsyncScope();
            var statusService = statusScope.ServiceProvider.GetRequiredService<IEvidenceContributionService>();
            var status = await statusService.GetFileStatusAsync(seed.Actors.ResponsibleA.UserId, intent.FileId, token);
            stage = "CV03_S05_CLEAN_STATUS_FAILED";
            Require(status.Status == EvidenceFileStatuses.Clean, stage);
            stage = "CV03_S05_CONTRIBUTE_FAILED";
            var evidence = await statusService.ContributeAsync(new(seed.Actors.ResponsibleA.UserId, infrastructure.Uuids.NewUuid(),
                infrastructure.Uuids.NewUuid(), obligation.Id, "FOTOGRAFIA_FINAL", intent.FileId, null), token);
            stage = "CV03_S05_LINK_FAILED";
            Require(evidence.File?.FileId == intent.FileId, stage);
            return Facts(("cleanFiles", 1));
        }
        catch (EvidenceUploadMissingException)
        {
            throw new DemoScenarioAssertionException("CV03_S05_OBJECT_MISSING");
        }
        catch (Exception exception) when (exception is EvidenceFileStateException or EvidenceObjectIntegrityException)
        {
            throw new DemoScenarioAssertionException("CV03_S05_METADATA_MISMATCH");
        }
        catch (EvidenceUploadExpiredException)
        {
            throw new DemoScenarioAssertionException("CV03_S05_UPLOAD_EXPIRED");
        }
        catch (EvidenceStorageUnavailableException)
        {
            throw new DemoScenarioAssertionException("CV03_S05_STORAGE_UNAVAILABLE");
        }
        catch (EvidenceAccessDeniedException)
        {
            throw new DemoScenarioAssertionException("CV03_S05_ACCESS_FAILED");
        }
        catch (EvidenceFileNotFoundException)
        {
            throw new DemoScenarioAssertionException("CV03_S05_FILE_NOT_FOUND");
        }
        catch (EvidenceObligationNotFoundException)
        {
            throw new DemoScenarioAssertionException("CV03_S05_OBLIGATION_NOT_FOUND");
        }
        catch (Exception exception) when (exception is EvidenceConditionalRequirementException or EvidenceConditionUnresolvedException)
        {
            throw new DemoScenarioAssertionException("CV03_S05_CONDITION_FAILED");
        }
        catch (EvidenceIdempotencyConflictException)
        {
            throw new DemoScenarioAssertionException("CV03_S05_IDEMPOTENCY_FAILED");
        }
        catch (Exception exception) when (exception is EvidenceRequestInvalidException or ArgumentException)
        {
            throw new DemoScenarioAssertionException("CV03_S05_REQUEST_FAILED");
        }
        catch (Exception exception) when (exception is EvidenceRequirementInvalidException or EvidenceTypeNotImplementedException or EvidenceUnsupportedMediaTypeException)
        {
            throw new DemoScenarioAssertionException("CV03_S05_REQUIREMENT_FAILED");
        }
        catch (DbUpdateException)
        {
            throw new DemoScenarioAssertionException("CV03_S05_PERSIST_FAILED");
        }
        catch (NpgsqlException)
        {
            throw new DemoScenarioAssertionException("CV03_S05_PERSIST_FAILED");
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or TimeoutException)
        {
            throw new DemoScenarioAssertionException("CV03_S05_IO_FAILED");
        }
        catch (InvalidOperationException)
        {
            throw new DemoScenarioAssertionException("CV03_S05_STATE_FAILED");
        }
        catch (DemoScenarioAssertionException)
        {
            throw;
        }
        catch
        {
            throw new DemoScenarioAssertionException(stage);
        }
    }

    private async Task<IReadOnlyDictionary<string, long>> S06(CancellationToken token)
    {
        await using var scope = infrastructure.Services.CreateAsyncScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<EvidenceInspectionPipeline>();
        await using var invalid = new MemoryStream(Encoding.UTF8.GetBytes("not-an-image"));
        var invalidResult = await pipeline.InspectAsync(invalid, "image/png", "invalid.png", token);
        Require(invalidResult.Result == EvidenceScanResult.Invalido);
        Require(Environment.GetEnvironmentVariable("SGOL_EVIDENCE_EICAR_TESTS") == "true");
        var scanner = scope.ServiceProvider.GetRequiredService<IFileMalwareScanner>();
        string[] fragments = ["AP[4\\PZX54(P^)7CC)7}", "ANTIVIRUS-TEST-FILE!$H+H*", "X5O!P%@", "$EICAR-STANDARD-"];
        var eicar = Encoding.ASCII.GetBytes(string.Concat(fragments[2], fragments[0], fragments[3], fragments[1]));
        await using var infected = new MemoryStream(eicar);
        var infectedResult = await scanner.ScanAsync(infected, eicar.Length, token);
        Require(infectedResult.Result == EvidenceScanResult.Infectado);
        return Facts(("invalid", 1), ("infected", 1));
    }

    private async Task<IReadOnlyDictionary<string, long>> S07(CancellationToken token)
    {
        await using var scope = infrastructure.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IEvidenceContributionService>();
        using var changed = JsonDocument.Parse("{\"schemaVersion\":1,\"expectedTarget\":100,\"actualSales\":80,\"sourceReference\":\"CV03-R2\"}");
        var replacement = await service.ReplaceAsync(new(seed!.Actors.ResponsibleA.UserId, infrastructure.Uuids.NewUuid(),
            infrastructure.Uuids.NewUuid(), seed.Obligations["STRUCTURED"].Id, calculationEvidence!.EvidenceItemId,
            null, changed, null, calculationEvidence.ItemRowVersion), token);
        var history = await service.ListAsync(new(seed.Actors.ResponsibleA.UserId, seed.Obligations["STRUCTURED"].Id,
            "CALCULO_AVANCE", null, null, 100), token);
        Require(history.Items.Count == 2 && history.Items.Count(item => item.Version.Status == EvidenceVersionStatuses.Current) == 1 &&
            history.Items.Count(item => item.Version.Status == EvidenceVersionStatuses.Superseded) == 1);
        calculationEvidence = replacement;
        return Facts(("versions", history.Items.Count));
    }

    private async Task<IReadOnlyDictionary<string, long>> S08(CancellationToken token)
    {
        var review = await Review(seed!.Obligations["STRUCTURED"], token);
        Require(review.Result == EvidenceReviewResults.Incomplete);
        Require(review.MissingRequirements.Any(item => item.RequirementCode == "ACCION_O_CONFORMIDAD"));
        await using var scope = infrastructure.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        var superseded = await context.EvidenceVersions.AsNoTracking().SingleAsync(item =>
            item.EvidenceItemId == calculationEvidence!.EvidenceItemId && item.Status == EvidenceVersionStatuses.Superseded, token);
        Require(!review.Requirements.Any(item => item.EvidenceVersionId == superseded.Id));
        return Facts(("missing", review.MissingRequirements.Count));
    }

    private async Task<IReadOnlyDictionary<string, long>> S09(CancellationToken token)
    {
        var review = await Review(seed!.Obligations["TAR0092_TRUE"], token);
        var photo = review.Requirements.Single(item => item.RequirementCode == "FOTO_DIFERENCIA_DANO");
        Require(review.Result == EvidenceReviewResults.Complete && photo.Applicability == EvidenceReviewApplicability.Applicable && photo.Satisfied);
        return Facts(("applicable", 1));
    }

    private async Task<IReadOnlyDictionary<string, long>> S10(CancellationToken token)
    {
        var review = await Review(seed!.Obligations["TAR0092_FALSE"], token);
        var photo = review.Requirements.Single(item => item.RequirementCode == "FOTO_DIFERENCIA_DANO");
        Require(review.Result == EvidenceReviewResults.Complete && photo.Applicability == EvidenceReviewApplicability.NotApplicable && photo.EvidenceVersionId is null);
        return Facts(("notApplicable", 1));
    }

    private async Task<IReadOnlyDictionary<string, long>> S11(CancellationToken token)
    {
        var review = await Review(seed!.Obligations["COMPLETE"], token);
        Require(review.Result == EvidenceReviewResults.Complete && review.MissingRequirements.Count == 0);
        return Facts(("requirements", review.Requirements.Count));
    }

    private async Task<IReadOnlyDictionary<string, long>> S12(CancellationToken token)
    {
        var review = await Review(seed!.Obligations["INCOMPLETE"], token);
        Require(review.Result == EvidenceReviewResults.Incomplete && review.MissingRequirements.Any(item => item.RequirementCode == "CHECKLIST_COMPLETO"));
        return Facts(("missing", review.MissingRequirements.Count));
    }

    private async Task<IReadOnlyDictionary<string, long>> S13(CancellationToken token)
    {
        await using var scope = infrastructure.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IObligationConclusionService>();
        var obligation = seed!.Obligations["INCOMPLETE"];
        await RequireThrows<ObligationEvidenceMissingException>(() => service.ConcludeAsync(new(seed.Actors.ResponsibleA.UserId,
            infrastructure.Uuids.NewUuid(), infrastructure.Uuids.NewUuid(), obligation.Id, 1), token));
        var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        Require(!await context.ExecutionResults.AsNoTracking().AnyAsync(item => item.ObligationId == obligation.Id, token));
        Require(await context.WorkObligations.AsNoTracking().Where(item => item.Id == obligation.Id)
            .Select(item => item.ExecutionStatus).SingleAsync(token) == WorkObligationStatuses.Pending);
        return Facts(("results", 0));
    }

    private async Task<IReadOnlyDictionary<string, long>> S14(CancellationToken token)
    {
        await using var scope = infrastructure.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IObligationConclusionService>();
        var obligation = seed!.Obligations["COMPLETE"];
        conclusion = await service.ConcludeAsync(new(seed.Actors.ResponsibleA.UserId, infrastructure.Uuids.NewUuid(),
            infrastructure.Uuids.NewUuid(), obligation.Id, 1), token);
        Require(conclusion.ExecutionStatus == WorkObligationStatuses.Concluded && conclusion.ExecutionResult.EvidenceReviewSnapshotId != Guid.Empty);
        return Facts(("results", 1), ("rowVersion", conclusion.RowVersion));
    }

    private async Task<IReadOnlyDictionary<string, long>> S15(CancellationToken token)
    {
        await using var scope = infrastructure.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IObligationConclusionService>();
        var obligation = seed!.Obligations["COMPLETE"];
        var record = await scope.ServiceProvider.GetRequiredService<SgolDbContext>().IdempotencyRecords.AsNoTracking()
            .SingleAsync(item => item.ResourceId == conclusion!.ExecutionResult.Id, token);
        var replay = await service.ConcludeAsync(new(seed.Actors.ResponsibleA.UserId, record.Key, infrastructure.Uuids.NewUuid(),
            obligation.Id, 1), token);
        Require(replay.ExecutionResult.Id == conclusion!.ExecutionResult.Id);
        return Facts(("sameResult", 1));
    }

    private async Task<IReadOnlyDictionary<string, long>> S16(CancellationToken token)
    {
        var obligation = seed!.Obligations["RACE"];
        await using var firstScope = infrastructure.Services.CreateAsyncScope();
        await using var secondScope = infrastructure.Services.CreateAsyncScope();
        var first = firstScope.ServiceProvider.GetRequiredService<IObligationConclusionService>();
        var second = secondScope.ServiceProvider.GetRequiredService<IObligationConclusionService>();
        var tasks = new[]
        {
            Capture(() => first.ConcludeAsync(new(seed.Actors.ResponsibleA.UserId, infrastructure.Uuids.NewUuid(), infrastructure.Uuids.NewUuid(), obligation.Id, 1), token)),
            Capture(() => second.ConcludeAsync(new(seed.Actors.ResponsibleA.UserId, infrastructure.Uuids.NewUuid(), infrastructure.Uuids.NewUuid(), obligation.Id, 1), token)),
        };
        var outcomes = await Task.WhenAll(tasks);
        Require(outcomes.Count(item => item is null) == 1);
        await using var verifyScope = infrastructure.Services.CreateAsyncScope();
        var context = verifyScope.ServiceProvider.GetRequiredService<SgolDbContext>();
        Require(await context.ExecutionResults.AsNoTracking().CountAsync(item => item.ObligationId == obligation.Id, token) == 1);
        return Facts(("winner", 1));
    }

    private async Task<IReadOnlyDictionary<string, long>> S17(CancellationToken token)
    {
        await using var scope = infrastructure.Services.CreateAsyncScope();
        var reader = scope.ServiceProvider.GetRequiredService<IInboxReader>();
        var actor = seed!.Actors.ResponsibleA.UserId;
        var current = await reader.ReadAsync(new(actor, seed.Obligations["AVAILABLE"].PeriodId, null, null, 100, "ALL", null, 100), token);
        var future = await reader.ReadAsync(new(actor, seed.Obligations["FUTURE"].PeriodId, null, null, 100, "ALL", null, 100), token);
        Require(current.Tasks.Items.Any(item => item.TaskState == InboxTaskStates.Available));
        Require(current.Tasks.Items.Any(item => item.TaskState == InboxTaskStates.Overdue));
        Require(future.Tasks.Items.Any(item => item.TaskState == InboxTaskStates.Future));
        return Facts(("current", current.Tasks.Items.Count), ("future", future.Tasks.Items.Count));
    }

    private async Task<IReadOnlyDictionary<string, long>> S18(CancellationToken token)
    {
        await using var scope = infrastructure.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        var obligation = seed!.Obligations["OVERDUE"];
        var status = await context.WorkObligations.AsNoTracking().Where(item => item.Id == obligation.Id)
            .Select(item => item.ExecutionStatus).SingleAsync(token);
        Require(status == WorkObligationStatuses.Pending);
        return Facts(("pending", 1));
    }

    private async Task<IReadOnlyDictionary<string, long>> S19(CancellationToken token)
    {
        await using var scope = infrastructure.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        var assignment = seed!.Obligations["BINARY"].AssignmentId;
        var notices = await context.InternalNotices.AsNoTracking().CountAsync(item => item.ResourceId == assignment &&
            item.RecipientUserId == seed.Actors.ResponsibleA.UserId, token);
        Require(notices == 1);
        return Facts(("notices", notices));
    }

    private async Task<IReadOnlyDictionary<string, long>> S20(CancellationToken token)
    {
        await using var scope = infrastructure.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        var actorId = seed!.Actors.ResponsibleA.UserId;
        var notice = await context.InternalNotices.AsNoTracking().FirstAsync(item =>
            item.RecipientUserId == actorId && item.ReadAt == null, token);
        infrastructure.Clock.AdvancePast(notice.CreatedAt);
        infrastructure.Actor.CurrentUserId = actorId;
        using var invalidCsrfRequest = infrastructure.CreateCsrfRequest(
            HttpMethod.Post, $"/api/v1/me/notices/{notice.Id:D}/read", actorId);
        invalidCsrfRequest.Headers.Remove("X-CSRF-TOKEN");
        invalidCsrfRequest.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", "invalid");
        using var invalidCsrf = await infrastructure.Client.SendAsync(invalidCsrfRequest, token);
        Require(invalidCsrf.StatusCode == HttpStatusCode.BadRequest, "CV03_S20_CSRF_REJECTION_FAILED");
        using var firstRequest = infrastructure.CreateCsrfRequest(HttpMethod.Post, $"/api/v1/me/notices/{notice.Id:D}/read", actorId);
        using var first = await infrastructure.Client.SendAsync(firstRequest, token);
        Require(first.StatusCode == HttpStatusCode.OK, "CV03_S20_FIRST_STATUS_FAILED");
        using var replayRequest = infrastructure.CreateCsrfRequest(HttpMethod.Post, $"/api/v1/me/notices/{notice.Id:D}/read", actorId);
        using var replay = await infrastructure.Client.SendAsync(replayRequest, token);
        Require(replay.StatusCode == HttpStatusCode.OK, "CV03_S20_REPLAY_STATUS_FAILED");
        var firstBody = await first.Content.ReadAsStringAsync(token);
        var replayBody = await replay.Content.ReadAsStringAsync(token);
        Require(firstBody.Contains(InternalNoticeReadResults.MarkedRead, StringComparison.Ordinal),
            "CV03_S20_FIRST_RESULT_FAILED");
        Require(replayBody.Contains(InternalNoticeReadResults.AlreadyRead, StringComparison.Ordinal),
            "CV03_S20_REPLAY_RESULT_FAILED");
        var audits = await context.AuditEvents.AsNoTracking().CountAsync(item => item.Action == "INTERNAL_NOTICE_READ" && item.ResourceId == notice.Id, token);
        Require(audits == 1, "CV03_S20_AUDIT_COUNT_FAILED");
        return Facts(("audits", audits));
    }

    private async Task<IReadOnlyDictionary<string, long>> S21(CancellationToken token)
    {
        var before = await MutationFingerprint(token);
        await using var scope = infrastructure.Services.CreateAsyncScope();
        _ = await scope.ServiceProvider.GetRequiredService<IObligationQueryReader>().ListAsync(new(seed!.Actors.ResponsibleA.UserId,
            null, null, null, null, null, null, 100), token);
        _ = await scope.ServiceProvider.GetRequiredService<IInboxReader>().ReadAsync(new(seed.Actors.ResponsibleA.UserId,
            seed.Obligations["AVAILABLE"].PeriodId, null, null, 100, "ALL", null, 100), token);
        var after = await MutationFingerprint(token);
        Require(before == after);
        return Facts(("unchanged", 1));
    }

    private async Task<IReadOnlyDictionary<string, long>> S22(CancellationToken token)
    {
        await using var scope = infrastructure.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        var unknown = await context.OutboxEvents.AsNoTracking().CountAsync(item =>
            item.EventType != EvidenceInspectionOutboxHandler.ContractEventType, token);
        Require(unknown == 0);
        return Facts(("externalMessages", 0));
    }

    private async Task<IReadOnlyDictionary<string, long>> S23(CancellationToken token)
    {
        await using var connection = new NpgsqlConnection(infrastructure.Configuration.GetConnectionString("Sgol"));
        await connection.OpenAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM (VALUES ('validation_policy_version'),('validation_requirement'),('validation_decision_version')) AS v(name) WHERE to_regclass('public.' || name) IS NOT NULL";
        var count = Convert.ToInt64(await command.ExecuteScalarAsync(token), System.Globalization.CultureInfo.InvariantCulture);
        Require(count == 0);
        return Facts(("futureTables", count));
    }

    private async Task<IReadOnlyDictionary<string, long>> S24(CancellationToken token)
    {
        var firstFingerprint = await MutationFingerprint(infrastructure, token);
        await using var second = await Cv03Infrastructure.StartAsync(token);
        var secondEngine = new Cv03ScenarioEngine(second);
        foreach (var scenario in ScenarioCatalog.All.Where(item => item.Id != "S24"))
            _ = await secondEngine.RunAsync(scenario.Id, token);
        var secondFingerprint = await MutationFingerprint(second, token);
        Require(firstFingerprint == secondFingerprint);
        return Facts(("reproducedScenarios", 23), ("residualResources", 0));
    }

    private async Task<EvidenceReviewDetails> Review(SeededObligation obligation, CancellationToken token)
    {
        await using var scope = infrastructure.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IEvidenceReviewService>().ReviewAsync(new(
            seed!.Actors.ResponsibleA.UserId, infrastructure.Uuids.NewUuid(), obligation.Id), token);
    }

    private Task<string> MutationFingerprint(CancellationToken token) => MutationFingerprint(infrastructure, token);

    private static async Task<string> MutationFingerprint(Cv03Infrastructure target, CancellationToken token)
    {
        await using var scope = target.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        var values = new[]
        {
            await context.WorkObligations.AsNoTracking().CountAsync(token), await context.EvidenceReviewSnapshots.AsNoTracking().CountAsync(token),
            await context.ExecutionResults.AsNoTracking().CountAsync(token), await context.AuditEvents.AsNoTracking().CountAsync(token),
            await context.IdempotencyRecords.AsNoTracking().CountAsync(token), await context.InternalNotices.AsNoTracking().CountAsync(token),
            await context.AssignmentVersions.AsNoTracking().CountAsync(token), await context.FileObjects.AsNoTracking().CountAsync(token),
            await context.EvidenceItems.AsNoTracking().CountAsync(token), await context.EvidenceVersions.AsNoTracking().CountAsync(token),
            await context.OutboxEvents.AsNoTracking().CountAsync(token),
        };
        return string.Join('|', values);
    }

    private static Dictionary<string, long> Facts(params (string Key, long Value)[] values) =>
        values.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);

    private static void Require(bool condition, string errorCode = "CV03_SCENARIO_FAILED")
    {
        if (!condition) throw new DemoScenarioAssertionException(errorCode);
    }

    private static async Task RequireThrows<T>(Func<Task> action) where T : Exception
    {
        try { await action(); }
        catch (T) { return; }
        throw new DemoScenarioAssertionException();
    }

    private static async Task<Exception?> Capture(Func<Task<ObligationConclusionResult>> action)
    {
        try { _ = await action(); return null; }
        catch (Exception exception) { return exception; }
    }

    private static long Count(string value, string fragment)
    {
        long count = 0;
        for (var index = 0; (index = value.IndexOf(fragment, index, StringComparison.Ordinal)) >= 0; index += fragment.Length) count++;
        return count;
    }
}
