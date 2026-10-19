using System.Data;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sgol.Assignment.Contracts;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.Configuration.Contracts;
using Sgol.Evidence.Contracts;
using Sgol.Generation.Contracts;
using Sgol.Identity.Contracts;
using Sgol.JobInfrastructure;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Sgol.Web.Infrastructure.Persistence.Idempotency;
using Sgol.Web.Infrastructure.Evidence;

namespace Sgol.Web.Infrastructure.Persistence.Evidence;

public sealed class EfEvidenceContributionService(
    SgolDbContext dbContext,
    AuditTransaction auditTransaction,
    IPrivateObjectStorage storage,
    IEvidenceObjectKeyFactory keyFactory,
    IOutboxWriter outboxWriter,
    IClock clock,
    IUuidGenerator uuidGenerator, IDataProtectionProvider protection, IConfiguration configuration) : IEvidenceContributionService
{
    public async Task<EvidenceCameraSession> BeginCaptureAsync(Guid actor, Guid obligationId, string requirementCode, CancellationToken cancellationToken = default)
    {
        if (!configuration.GetValue("Evidence:Camera:Enabled", true)) throw new EvidenceTypeNotImplementedException();
        var access = await RequireMutationAccessAsync(actor, obligationId, clock.UtcNow, cancellationToken);
        var requirement = await RequireRequirementAsync(access.Obligation, requirementCode, cancellationToken);
        if (requirement.Kind != EvidenceRequirementKinds.Photograph) throw new EvidenceRequirementInvalidException();
        await RequireBinaryRequirementAsync(access, requirement, cancellationToken);
        var grant = new EvidenceCameraGrant(Guid.NewGuid(), actor, obligationId, requirement.Id, clock.UtcNow.AddMinutes(30));
        await auditTransaction.ExecuteAsync(Audit("EVIDENCE_CAMERA_OPENED", "WORK_OBLIGATION", obligationId, actor, Guid.NewGuid(), clock.UtcNow,
            null, new { captureSessionId = grant.Id, expiresAt = grant.ExpiresAt }), _ => Task.CompletedTask, cancellationToken);
        return new(new EvidenceCameraAuthorization(protection).Issue(grant), grant.Id, grant.ExpiresAt);
    }

    private const string InspectionEvent = "EVIDENCE.FILE_INSPECTION_REQUESTED.V1";
    private static readonly TimeSpan IdempotencyRetention = TimeSpan.FromHours(24);

    public async Task<EvidenceUploadIntentResult> CreateUploadIntentAsync(
        CreateEvidenceUploadCommand command, CancellationToken cancellationToken = default)
    {
        var input = Normalize(command);
        var now = clock.UtcNow;
        var access = await RequireMutationAccessAsync(input.ActorUserId, input.ObligationId, now, cancellationToken);
        var requirement = await RequireRequirementAsync(access.Obligation, input.RequirementCode, cancellationToken);
        EnsureBinaryKind(requirement);
        ValidateBinaryContract(requirement, input.DeclaredMediaType, input.OriginalFileName, input.DocumentSubtype);
        EvidenceCameraGrant? capture = null;
        if (requirement.Kind == EvidenceRequirementKinds.Photograph)
        {
            if (!configuration.GetValue("Evidence:Camera:Enabled", true)) throw new EvidenceTypeNotImplementedException();
            capture = new EvidenceCameraAuthorization(protection).Read(input.CaptureToken, input.ActorUserId, input.ObligationId, requirement.Id, now)
                ?? throw new EvidenceRequestInvalidException();
        }
        else if (input.CaptureToken is not null) throw new EvidenceRequestInvalidException();
        if (access.Obligation.ExecutionStatus == WorkObligationStatuses.Concluded &&
            !await dbContext.EvidenceItems.AsNoTracking().AnyAsync(x => x.ObligationId == input.ObligationId && x.RequirementVersionId == requirement.Id, cancellationToken))
        {
            throw new EvidenceItemNotFoundException();
        }

        const string operation = "FILE_UPLOAD_INTENT_CREATE";
        var resource = input.ObligationId.ToString("D");
        var scope = IdempotencyProtocol.Scope(input.ActorUserId.ToString("D"), operation, resource);
        object uploadBody = capture is null ? new { input.ObligationId, input.RequirementCode, input.OriginalFileName,
            input.DeclaredMediaType, input.SizeBytes, input.Sha256, input.DocumentSubtype } : new
        {
            input.ObligationId,
            input.RequirementCode,
            input.OriginalFileName,
            input.DeclaredMediaType,
            input.SizeBytes,
            input.Sha256,
            input.DocumentSubtype,
            captureSessionId = capture?.Id,
        };
        var hash = IdempotencyProtocol.HashCanonical(operation, input.ActorUserId.ToString("D"), resource, uploadBody);
        var legacyScope = LegacyScope("UPLOAD", input.ActorUserId, input.ObligationId);
        var legacyHash = Hash(new { input.ActorUserId, input.IdempotencyKey, input.CorrelationId, input.ObligationId, input.RequirementCode,
            input.OriginalFileName, input.DeclaredMediaType, input.SizeBytes, input.Sha256, input.DocumentSubtype });
        var replay = await FindReplayAsync(scope, legacyScope, input.IdempotencyKey, hash, legacyHash,
            input.ActorUserId, input.CorrelationId, operation, input.ObligationId, cancellationToken);
        if (replay is not null)
        {
            var existing = await dbContext.FileObjects.AsNoTracking().SingleAsync(x => x.Id == replay.ResourceId, cancellationToken);
            if (now > existing.UploadExpiresAt) throw new EvidenceUploadExpiredException();
            var metadata = Metadata(existing);
            var authorization = await storage.CreateQuarantineUploadAuthorizationAsync(metadata, existing.UploadExpiresAt, cancellationToken);
            return new EvidenceUploadIntentResult(existing.Id, "PENDIENTE_CARGA", authorization, true);
        }
        await RequireBinaryRequirementAsync(access, requirement, cancellationToken);

        var id = uuidGenerator.NewUuid();
        var key = keyFactory.Create();
        var file = new FileObject(id, BranchScope.LorettaId, input.ObligationId,
            access.Obligation.EvidencePolicyVersionId!.Value, requirement.Id, requirement.RequirementCode,
            requirement.Kind, input.DocumentSubtype, key.Value, input.OriginalFileName,
            input.DeclaredMediaType, input.SizeBytes, input.Sha256, input.ActorUserId, now);
        if (capture is not null) file.SetCapture(capture.Id, capture.ExpiresAt);
        var authorizationResult = await storage.CreateQuarantineUploadAuthorizationAsync(
            Metadata(file), file.UploadExpiresAt, cancellationToken);

        try
        {
            await auditTransaction.ExecuteAsync(IsolationLevel.Serializable, async token =>
            {
                await RequireBinaryRequirementAsync(access, requirement, token);
                if (dbContext.Database.IsNpgsql())
                {
                    await dbContext.Database.ExecuteSqlInterpolatedAsync(
                        $"SELECT pg_advisory_xact_lock(hashtextextended({input.ActorUserId.ToString("D")}, 0))", token);
                }
                var count = await dbContext.FileObjects.CountAsync(x => x.UploadedBy == input.ActorUserId && x.CreatedAt > now.AddHours(-1), token);
                if (count >= 30) throw new EvidenceRateLimitException();
                dbContext.FileObjects.Add(file);
                AddIdempotency(
                    scope, input.IdempotencyKey, hash, "FILE_OBJECT", id, 201, now,
                    new { fileId = id, status = "PENDIENTE_CARGA", uploadExpiresAt = file.UploadExpiresAt },
                    now + IdempotencyRetention,
                    $"/api/v1/files/{id:D}");
                return Audit("EVIDENCE_UPLOAD_INTENT_CREATED", "FILE_OBJECT", id, input.ActorUserId,
                    input.CorrelationId, now, null, new { fileId = id, status = "PENDIENTE_CARGA" });
            }, cancellationToken);
        }
        catch (DbUpdateException exception) when (Constraint(exception) == "PK_idempotency_record")
        {
            dbContext.ChangeTracker.Clear();
            var concurrent = await FindReplayAsync(scope, legacyScope, input.IdempotencyKey, hash, legacyHash,
                input.ActorUserId, input.CorrelationId, operation, input.ObligationId, cancellationToken)
                ?? throw new EvidenceIdempotencyConflictException();
            var existing = await dbContext.FileObjects.AsNoTracking().SingleAsync(x => x.Id == concurrent.ResourceId, cancellationToken);
            if (now > existing.UploadExpiresAt) throw new EvidenceUploadExpiredException();
            return new EvidenceUploadIntentResult(existing.Id, "PENDIENTE_CARGA",
                await storage.CreateQuarantineUploadAuthorizationAsync(Metadata(existing), existing.UploadExpiresAt, cancellationToken), true);
        }
        catch
        {
            dbContext.ChangeTracker.Clear();
            throw;
        }

        EvidenceTelemetry.UploadIntents.Add(1, new("result", "CREATED"), new("mediaType", input.DeclaredMediaType));
        return new EvidenceUploadIntentResult(id, "PENDIENTE_CARGA", authorizationResult, false);
    }

    public async Task<EvidenceFileStatusDetails> CompleteUploadAsync(
        CompleteEvidenceUploadCommand command, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var file = await dbContext.FileObjects.SingleOrDefaultAsync(x => x.Id == command.FileId, cancellationToken)
            ?? throw new EvidenceFileNotFoundException();
        var access = await RequireMutationAccessAsync(command.ActorUserId, file.ObligationId, now, cancellationToken);
        if (file.UploadedBy != command.ActorUserId) throw new EvidenceFileNotFoundException();
        var requirement = await RequireRequirementAsync(access.Obligation, file.RequirementCode, cancellationToken);
        EnsureBinaryKind(requirement);
        const string operation = "FILE_UPLOAD_COMPLETE";
        var resource = command.FileId.ToString("D");
        var scope = IdempotencyProtocol.Scope(command.ActorUserId.ToString("D"), operation, resource);
        var hash = IdempotencyProtocol.HashCanonical(
            operation, command.ActorUserId.ToString("D"), resource, new { command.FileId });
        var legacyScope = LegacyScope("COMPLETE", command.ActorUserId, command.FileId);
        var legacyHash = Hash(new { command.FileId });
        var replay = await FindReplayAsync(scope, legacyScope, command.IdempotencyKey, hash, legacyHash,
            command.ActorUserId, command.CorrelationId, operation, command.FileId, cancellationToken);
        if (replay is not null)
        {
            if (replay.ResponseCode != 202) throw new EvidenceFileStateException();
            if (replay.ProtocolVersion == IdempotencyProtocol.CurrentVersion)
                return IdempotencyProtocol.ReadPayload<EvidenceFileStatusDetails>(replay);
            return ToStatus(file);
        }
        await RequireBinaryRequirementAsync(access, requirement, cancellationToken);
        if (now > file.UploadExpiresAt) throw new EvidenceUploadExpiredException();

        EvidenceObjectMetadata actual;
        try
        {
            actual = await storage.GetMetadataAsync(EvidenceStorageArea.Quarantine, EvidenceObjectKey.Parse(file.ObjectKey), cancellationToken);
        }
        catch (EvidenceObjectNotFoundException)
        {
            throw new EvidenceUploadMissingException();
        }

        var expected = Metadata(file);
        var matches = actual == expected;
        try
        {
            await auditTransaction.ExecuteAsync(async token =>
            {
                await RequireBinaryRequirementAsync(access, requirement, token);
                if (!matches)
                {
                    file.MarkTerminal(EvidenceFileStatuses.Invalid, null, null, "UPLOAD_METADATA_MISMATCH", now);
                    AddIdempotency(
                        scope, command.IdempotencyKey, hash, "FILE_OBJECT", file.Id, 422, now,
                        ToStatus(file), DateTimeOffset.MaxValue);
                    return Audit("EVIDENCE_UPLOAD_REJECTED", "FILE_OBJECT", file.Id, command.ActorUserId,
                        command.CorrelationId, now, new { status = EvidenceFileStatuses.Pending }, new { status = file.ScanStatus });
                }

                file.ConfirmUpload(now);
                outboxWriter.Enqueue(InspectionEvent, file.Id,
                    JsonSerializer.SerializeToElement(new { fileObjectId = file.Id }), command.CorrelationId);
                AddIdempotency(
                    scope, command.IdempotencyKey, hash, "FILE_OBJECT", file.Id, 202, now,
                    ToStatus(file), DateTimeOffset.MaxValue);
                return Audit("EVIDENCE_UPLOAD_COMPLETED", "FILE_OBJECT", file.Id, command.ActorUserId,
                    command.CorrelationId, now, new { status = "PENDIENTE_CARGA" }, new { status = "PENDIENTE_ESCANEO" });
            }, cancellationToken);
        }
        catch (DbUpdateException exception) when (Constraint(exception) == "PK_idempotency_record")
        {
            dbContext.ChangeTracker.Clear();
            var concurrent = await FindReplayAsync(scope, legacyScope, command.IdempotencyKey, hash, legacyHash,
                command.ActorUserId, command.CorrelationId, operation, command.FileId, cancellationToken)
                ?? throw new EvidenceIdempotencyConflictException();
            file = await dbContext.FileObjects.AsNoTracking().SingleAsync(x => x.Id == concurrent.ResourceId, cancellationToken);
            if (concurrent.ResponseCode != 202) throw new EvidenceFileStateException();
            if (concurrent.ProtocolVersion == IdempotencyProtocol.CurrentVersion)
                return IdempotencyProtocol.ReadPayload<EvidenceFileStatusDetails>(concurrent);
            return ToStatus(file);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            throw new EvidenceFileStateException();
        }
        catch
        {
            dbContext.ChangeTracker.Clear();
            throw;
        }

        if (!matches) throw new EvidenceFileStateException();
        EvidenceTelemetry.UploadCompletions.Add(1, tag: new("result", "ACCEPTED"));
        return ToStatus(file);
    }

    public async Task<EvidenceFileStatusDetails> GetFileStatusAsync(Guid actorUserId, Guid fileId, CancellationToken cancellationToken = default)
    {
        var file = await dbContext.FileObjects.AsNoTracking().SingleOrDefaultAsync(x => x.Id == fileId, cancellationToken)
            ?? throw new EvidenceFileNotFoundException();
        if (!await CanViewAsync(actorUserId, file.ObligationId, clock.UtcNow, cancellationToken)) throw new EvidenceFileNotFoundException();
        return ToStatus(file);
    }

    public async Task<EvidenceDetails> ContributeAsync(ContributeEvidenceCommand command, CancellationToken cancellationToken = default)
    {
        var selectedIds = NormalizeFileIds(command.FileId, command.FileIds);
        if (string.IsNullOrWhiteSpace(command.RequirementCode) ||
            (command.FileId.HasValue == (command.StructuredPayload is not null)) || command.FileId == Guid.Empty)
            throw new EvidenceRequestInvalidException();
        var now = clock.UtcNow;
        var access = await RequireMutationAccessAsync(command.ActorUserId, command.ObligationId, now, cancellationToken);
        if (access.Obligation.ExecutionStatus != WorkObligationStatuses.Pending || access.ResponsiblePersonId != access.Actor.PersonId)
            throw new EvidenceReplacementNotAllowedException();
        var requirement = await RequireRequirementAsync(access.Obligation, command.RequirementCode, cancellationToken);
        JsonDocument? structured = null;
        if (command.FileId.HasValue)
            EnsureBinaryKind(requirement);
        else
            structured = RequireStructuredPayload(access, requirement, command.StructuredPayload!);
        const string operation = "EVIDENCE_CONTRIBUTE";
        var resource = command.ObligationId.ToString("D");
        object body = command.FileIds is null ? new { command.ObligationId, command.RequirementCode, command.FileId, structuredPayload = structured?.RootElement } :
            new { command.ObligationId, command.RequirementCode, command.FileId, fileIds = selectedIds, structuredPayload = structured?.RootElement };
        var scope = IdempotencyProtocol.Scope(command.ActorUserId.ToString("D"), operation, resource);
        var hash = IdempotencyProtocol.HashCanonical(operation, command.ActorUserId.ToString("D"), resource, body);
        var legacyScope = LegacyScope("CONTRIBUTE", command.ActorUserId, command.ObligationId);
        var legacyHash = Hash(body);
        var replay = await FindReplayAsync(scope, legacyScope, command.IdempotencyKey, hash, legacyHash,
            command.ActorUserId, command.CorrelationId, operation, command.ObligationId, cancellationToken);
        if (replay is not null)
            return replay.ProtocolVersion == IdempotencyProtocol.CurrentVersion
                ? IdempotencyProtocol.ReadPayload<EvidenceDetails>(replay)
                : await LoadDetailsAsync(replay.ResourceId, cancellationToken);
        if (command.FileId.HasValue) await RequireBinaryRequirementAsync(access, requirement, cancellationToken);

        EvidenceDetails? result = null;
        try
        {
            await auditTransaction.ExecuteAsync(IsolationLevel.Serializable, async token =>
            {
                if (await dbContext.EvidenceItems.AnyAsync(x => x.ObligationId == command.ObligationId && x.RequirementVersionId == requirement.Id, token))
                    throw new EvidenceAlreadyExistsException();
                var item = new EvidenceItem(uuidGenerator.NewUuid(), command.ObligationId,
                    access.Obligation.EvidencePolicyVersionId!.Value, requirement.Id, requirement.RequirementCode, requirement.Kind, now);
                FileObject? file = null;
                List<FileObject> linkedFiles = [];
                EvidenceVersion version;
                if (command.FileId.HasValue)
                {
                    await RequireBinaryRequirementAsync(access, requirement, token);
                    linkedFiles = await RequireFileSetAsync(selectedIds, command.ActorUserId, access.Obligation, requirement, token);
                    file = linkedFiles[0];
                    now = clock.UtcNow;
                    foreach (var linkedFile in linkedFiles) linkedFile.Link(item.Id, now);
                    version = new EvidenceVersion(uuidGenerator.NewUuid(), item.Id, 1, file.Id, command.ActorUserId, now);
                }
                else
                {
                    version = new EvidenceVersion(uuidGenerator.NewUuid(), item.Id, 1, structured!, command.ActorUserId, now);
                }
                AddVersionFiles(version, linkedFiles);
                dbContext.EvidenceItems.Add(item);
                dbContext.EvidenceVersions.Add(version);
                result = Map(item, version, file) with { Files = linkedFiles.Select(MapFile).ToArray() };
                AddIdempotency(
                    scope, command.IdempotencyKey, hash, "EVIDENCE_VERSION", version.Id, 201, now,
                    result, DateTimeOffset.MaxValue,
                    $"/api/v1/obligations/{command.ObligationId:D}/evidence");
                var structuredHash = structured is null ? null : Hash(structured.RootElement);
                return Audit("EVIDENCE_CONTRIBUTED", "EVIDENCE_ITEM", item.Id, command.ActorUserId,
                    command.CorrelationId, now, null, new
                    {
                        itemId = item.Id,
                        versionId = version.Id,
                        versionNo = 1,
                        requirementCode = item.RequirementCode,
                        requirementKind = item.RequirementKind,
                        evidenceKind = file is null ? "STRUCTURED" : "FILE",
                        schemaVersion = file is null ? 1 : (int?)null,
                        structuredPayloadSha256 = structuredHash
                    });
            }, cancellationToken);
        }
        catch (DbUpdateException exception) when (Constraint(exception) == "PK_idempotency_record")
        {
            dbContext.ChangeTracker.Clear();
            var concurrent = await FindReplayAsync(scope, legacyScope, command.IdempotencyKey, hash, legacyHash,
                command.ActorUserId, command.CorrelationId, operation, command.ObligationId, cancellationToken)
                ?? throw new EvidenceIdempotencyConflictException();
            return concurrent.ProtocolVersion == IdempotencyProtocol.CurrentVersion
                ? IdempotencyProtocol.ReadPayload<EvidenceDetails>(concurrent)
                : await LoadDetailsAsync(concurrent.ResourceId, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            throw new EvidenceFileAlreadyLinkedException();
        }
        catch (DbUpdateException exception) when (Constraint(exception) is EvidenceItemConfiguration.UniqueRequirementIndex or "IX_evidence_version_file_object_id")
        {
            dbContext.ChangeTracker.Clear();
            throw Constraint(exception) == EvidenceItemConfiguration.UniqueRequirementIndex
                ? new EvidenceAlreadyExistsException()
                : new EvidenceFileAlreadyLinkedException();
        }
        catch
        {
            dbContext.ChangeTracker.Clear();
            throw;
        }
        EvidenceTelemetry.Links.Add(1, new("requirementKind", requirement.Kind), new("result", "CREATED"));
        return result!;
    }

    public async Task<EvidenceDetails> ReplaceAsync(ReplaceEvidenceCommand command, CancellationToken cancellationToken = default)
    {
        var selectedIds = NormalizeFileIds(command.FileId, command.FileIds);
        if (command.EvidenceItemId == Guid.Empty || command.ExpectedRowVersion < 1 ||
            (command.FileId.HasValue == (command.StructuredPayload is not null)) || command.FileId == Guid.Empty)
            throw new EvidenceRequestInvalidException();
        var now = clock.UtcNow;
        var access = await RequireMutationAccessAsync(command.ActorUserId, command.ObligationId, now, cancellationToken);
        var reason = NormalizeReason(command.Reason, access.Obligation.ExecutionStatus == WorkObligationStatuses.Concluded);
        if (access.Obligation.ExecutionStatus == WorkObligationStatuses.Pending)
        {
            if (access.Actor.PersonId != access.ResponsiblePersonId) throw new EvidenceReplacementNotAllowedException();
        }
        else if (!RoleHierarchy.IsStrictlySuperior(access.Actor.RoleCode, access.ResponsibleRoleCode))
        {
            throw new EvidenceReplacementNotAllowedException();
        }
        var requestedItem = await dbContext.EvidenceItems.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == command.EvidenceItemId && x.ObligationId == command.ObligationId, cancellationToken)
            ?? throw new EvidenceItemNotFoundException();
        var requestedRequirement = await dbContext.EvidenceRequirementVersions.AsNoTracking()
            .SingleAsync(x => x.Id == requestedItem.RequirementVersionId, cancellationToken);
        JsonDocument? requestedStructured = null;
        if (command.FileId.HasValue)
            EnsureBinaryKind(requestedRequirement);
        else
            requestedStructured = RequireStructuredPayload(access, requestedRequirement, command.StructuredPayload!);
        const string operation = "EVIDENCE_REPLACE";
        var resource = command.EvidenceItemId.ToString("D");
        object body = command.FileIds is null ? new { command.ObligationId, command.EvidenceItemId, command.FileId, structuredPayload = requestedStructured?.RootElement, reason } :
            new { command.ObligationId, command.EvidenceItemId, command.FileId, fileIds = selectedIds, structuredPayload = requestedStructured?.RootElement, reason };
        var legacyBody = new
        {
            command.ObligationId,
            command.EvidenceItemId,
            command.FileId,
            structuredPayload = requestedStructured?.RootElement,
            reason,
            command.ExpectedRowVersion
        };
        var scope = IdempotencyProtocol.Scope(command.ActorUserId.ToString("D"), operation, resource);
        var requestHash = IdempotencyProtocol.HashCanonical(
            operation, command.ActorUserId.ToString("D"), resource, body, command.ExpectedRowVersion);
        var legacyScope = LegacyScope("REPLACE", command.ActorUserId, command.EvidenceItemId);
        var legacyRequestHash = Hash(legacyBody);
        var existingReplay = await FindReplayAsync(scope, legacyScope, command.IdempotencyKey, requestHash, legacyRequestHash,
            command.ActorUserId, command.CorrelationId, operation, command.EvidenceItemId, cancellationToken);
        if (existingReplay is not null)
            return existingReplay.ProtocolVersion == IdempotencyProtocol.CurrentVersion
                ? IdempotencyProtocol.ReadPayload<EvidenceDetails>(existingReplay)
                : await LoadDetailsAsync(existingReplay.ResourceId, cancellationToken);
        if (command.FileId.HasValue) await RequireBinaryRequirementAsync(access, requestedRequirement, cancellationToken);
        EvidenceDetails? result = null;
        try
        {
            await auditTransaction.ExecuteAsync(IsolationLevel.Serializable, async token =>
            {
                var item = await dbContext.EvidenceItems.SingleOrDefaultAsync(x => x.Id == command.EvidenceItemId && x.ObligationId == command.ObligationId, token)
                    ?? throw new EvidenceItemNotFoundException();
                item.Advance(command.ExpectedRowVersion);
                var current = await dbContext.EvidenceVersions.Include(v => v.Files).SingleAsync(x => x.EvidenceItemId == item.Id && x.Status == EvidenceVersionStatuses.Current, token);
                var requirement = await dbContext.EvidenceRequirementVersions.SingleAsync(x => x.Id == item.RequirementVersionId, token);
                JsonDocument? structured = null;
                List<FileObject> linkedFiles = [];
                FileObject? file = null;
                if (command.FileId.HasValue)
                {
                    await RequireBinaryRequirementAsync(access, requirement, token);
                    linkedFiles = await RequireFileSetAsync(selectedIds, command.ActorUserId, access.Obligation, requirement, token);
                    file = linkedFiles[0];
                }
                else
                {
                    structured = RequireStructuredPayload(access, requirement, command.StructuredPayload!);
                }
                var files = new List<FileObject>();
                files.AddRange(linkedFiles);
                var oldIds = current.Files.Select(x => x.FileObjectId).Where(x => x != current.FileObjectId).ToArray();
                files.AddRange(await dbContext.FileObjects.AsNoTracking().Where(x => oldIds.Contains(x.Id)).ToArrayAsync(token));
                if (current.FileObjectId.HasValue)
                    files.Add(await dbContext.FileObjects.AsNoTracking().SingleAsync(x => x.Id == current.FileObjectId.Value, token));
                EnsureReasonDoesNotReferenceFiles(reason, [.. files]);
                current.Supersede();
                EvidenceVersion successor;
                if (file is not null)
                {
                    now = clock.UtcNow;
                    foreach (var linkedFile in linkedFiles) linkedFile.Link(item.Id, now);
                    successor = new EvidenceVersion(uuidGenerator.NewUuid(), item.Id, current.VersionNo + 1,
                        file.Id, command.ActorUserId, now, reason, current.Id);
                }
                else
                {
                    successor = new EvidenceVersion(uuidGenerator.NewUuid(), item.Id, current.VersionNo + 1,
                        structured!, command.ActorUserId, now, reason, current.Id);
                }
                AddVersionFiles(successor, linkedFiles);
                dbContext.EvidenceVersions.Add(successor);
                result = Map(item, successor, file) with { Files = linkedFiles.Select(MapFile).ToArray() };
                AddIdempotency(
                    scope, command.IdempotencyKey, requestHash, "EVIDENCE_VERSION", successor.Id, 201, now,
                    result, DateTimeOffset.MaxValue,
                    $"/api/v1/obligations/{command.ObligationId:D}/evidence/{command.EvidenceItemId:D}/replacements");
                var structuredHash = structured is null ? null : Hash(structured.RootElement);
                return Audit("EVIDENCE_REPLACED", "EVIDENCE_ITEM", item.Id, command.ActorUserId,
                    command.CorrelationId, now, new { versionId = current.Id, status = EvidenceVersionStatuses.Current },
                    new
                    {
                        versionId = successor.Id,
                        supersedesId = current.Id,
                        status = EvidenceVersionStatuses.Current,
                        requirementCode = item.RequirementCode,
                        requirementKind = item.RequirementKind,
                        evidenceKind = file is null ? "STRUCTURED" : "FILE",
                        schemaVersion = file is null ? 1 : (int?)null,
                        structuredPayloadSha256 = structuredHash
                    }, reason);
            }, cancellationToken);
        }
        catch (DbUpdateException exception) when (Constraint(exception) == "PK_idempotency_record")
        {
            dbContext.ChangeTracker.Clear();
            var concurrent = await FindReplayAsync(scope, legacyScope, command.IdempotencyKey, requestHash, legacyRequestHash,
                command.ActorUserId, command.CorrelationId, operation, command.EvidenceItemId, cancellationToken)
                ?? throw new EvidenceIdempotencyConflictException();
            return concurrent.ProtocolVersion == IdempotencyProtocol.CurrentVersion
                ? IdempotencyProtocol.ReadPayload<EvidenceDetails>(concurrent)
                : await LoadDetailsAsync(concurrent.ResourceId, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            throw new EvidenceVersionConflictException();
        }
        catch (Exception exception) when (IsEvidenceVersionRace(exception))
        {
            dbContext.ChangeTracker.Clear();
            throw new EvidenceVersionConflictException();
        }
        catch
        {
            dbContext.ChangeTracker.Clear();
            throw;
        }
        EvidenceTelemetry.Replacements.Add(1, new("requirementKind", result!.Requirement.Kind), new("result", "CREATED"));
        return result!;
    }

    public async Task<EvidencePage> ListAsync(EvidenceQuery query, CancellationToken cancellationToken = default)
    {
        if (query.Limit is < 1 or > 100 || (query.Status is not null && query.Status is not (EvidenceVersionStatuses.Current or EvidenceVersionStatuses.Superseded)))
            throw new EvidenceRequestInvalidException();
        if (!await CanViewAsync(query.ActorUserId, query.ObligationId, clock.UtcNow, cancellationToken)) throw new EvidenceObligationNotFoundException();
        EvidenceCursor? cursor = null;
        if (query.Cursor is not null)
        {
            cursor = DecodeCursor(query.Cursor);
            if (cursor.Version != 1 || cursor.ObligationId != query.ObligationId || cursor.RequirementCode != query.RequirementCode || cursor.Status != query.Status)
                throw new EvidenceRequestInvalidException();
        }
        var rows = from version in dbContext.EvidenceVersions.AsNoTracking()
                   join item in dbContext.EvidenceItems.AsNoTracking() on version.EvidenceItemId equals item.Id
                   join fileRow in dbContext.FileObjects.AsNoTracking() on version.FileObjectId equals (Guid?)fileRow.Id into fileRows
                   from file in fileRows.DefaultIfEmpty()
                   join requirement in dbContext.EvidenceRequirementVersions.AsNoTracking() on item.RequirementVersionId equals requirement.Id
                   where item.ObligationId == query.ObligationId &&
                         (query.RequirementCode == null || item.RequirementCode == query.RequirementCode) &&
                         (query.Status == null || version.Status == query.Status) &&
                         (cursor == null || requirement.Ordinal > cursor.Ordinal ||
                          (requirement.Ordinal == cursor.Ordinal && version.VersionNo < cursor.VersionNo) ||
                          (requirement.Ordinal == cursor.Ordinal && version.VersionNo == cursor.VersionNo && version.Id.CompareTo(cursor.EvidenceVersionId) > 0))
                   orderby requirement.Ordinal, version.VersionNo descending, version.Id
                   select new { item, version, file, requirement.Ordinal };
        var materialized = await rows.Take(query.Limit + 1).ToListAsync(cancellationToken);
        var items = new List<EvidenceDetails>();
        foreach (var x in materialized.Take(query.Limit)) items.Add(await WithFilesAsync(Map(x.item, x.version, x.file), cancellationToken));
        string? next = null;
        if (materialized.Count > query.Limit)
        {
            var last = materialized[query.Limit - 1];
            next = EncodeCursor(new EvidenceCursor(1, query.ObligationId, query.RequirementCode, query.Status,
                last.Ordinal, last.version.VersionNo, last.version.Id));
        }
        return new EvidencePage(items, next);
    }

    private async Task<FileObject> RequireLinkableFileAsync(Guid id, Guid actor, WorkObligation obligation,
        EvidenceRequirementVersion requirement, CancellationToken token)
    {
        var file = await dbContext.FileObjects
            .FromSqlInterpolated($"SELECT * FROM file_object WHERE id = {id} FOR UPDATE")
            .SingleOrDefaultAsync(token) ?? throw new EvidenceFileNotFoundException();
        if (file.UploadedBy != actor || file.ObligationId != obligation.Id ||
            file.EvidencePolicyVersionId != obligation.EvidencePolicyVersionId || file.RequirementVersionId != requirement.Id)
            throw new EvidenceFileNotFoundException();
        if (file.LinkedEvidenceItemId is not null) throw new EvidenceFileAlreadyLinkedException();
        if (file.ScanStatus != EvidenceFileStatuses.Clean || file.LinkExpiresAt is null || clock.UtcNow > file.LinkExpiresAt)
            throw new EvidenceFileNotCleanException();
        var actual = await storage.GetMetadataAsync(EvidenceStorageArea.Clean, EvidenceObjectKey.Parse(file.ObjectKey), token);
        if (actual != Metadata(file)) throw new EvidenceFileNotCleanException();
        return file;
    }

    private async Task<Access> RequireMutationAccessAsync(Guid actorId, Guid obligationId, DateTimeOffset now, CancellationToken token)
    {
        var obligation = await dbContext.WorkObligations.SingleOrDefaultAsync(x => x.Id == obligationId && x.BranchId == BranchScope.LorettaId, token)
            ?? throw new EvidenceObligationNotFoundException();
        var taskCode = await (from version in dbContext.TaskDefinitionVersions.AsNoTracking()
                              join task in dbContext.TaskDefinitions.AsNoTracking() on version.TaskDefinitionId equals task.Id
                              where version.Id == obligation.TaskDefinitionVersionId
                              select task.TaskCode).SingleOrDefaultAsync(token)
            ?? throw new EvidenceObligationNotFoundException();
        var actor = await ActorAsync(actorId, now, token) ?? throw new EvidenceAccessDeniedException();
        var assignment = await dbContext.AssignmentVersions.AsNoTracking().SingleOrDefaultAsync(x => x.ObligationId == obligationId && x.Status == AssignmentVersionStatuses.Current, token)
            ?? throw new EvidenceObligationNotFoundException();
        var responsibleUser = await dbContext.AppUsers.AsNoTracking().SingleOrDefaultAsync(x => x.PersonId == assignment.PersonId && x.Status == AccountStatus.Active, token)
            ?? throw new EvidenceObligationNotFoundException();
        var responsibleRole = await dbContext.RoleAssignmentVersions.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == responsibleUser.Id && x.BranchId == BranchScope.LorettaId && x.Status == RoleAssignmentStatus.Active && x.ValidFrom <= now && (x.ValidTo == null || now < x.ValidTo), token)
            ?? throw new EvidenceObligationNotFoundException();
        var permitted = EvidenceAuthorization.CanReplace(obligation.ExecutionStatus,
            actor.PersonId == assignment.PersonId,
            RoleHierarchy.IsStrictlySuperior(actor.RoleCode, responsibleRole.RoleCode) && actor.RoleCode != CanonicalRole.SalesFloor);
        if (!permitted) throw new EvidenceObligationNotFoundException();
        return new Access(obligation, actor, assignment.PersonId, responsibleRole.RoleCode, taskCode);
    }

    private async Task<bool> CanViewAsync(Guid actorId, Guid obligationId, DateTimeOffset now, CancellationToken token)
    {
        var actor = await ActorAsync(actorId, now, token);
        if (actor is null) throw new EvidenceAccessDeniedException();
        if (!await dbContext.WorkObligations.AsNoTracking().AnyAsync(x =>
            x.Id == obligationId && x.BranchId == BranchScope.LorettaId, token)) return false;
        if (actor.RoleCode == CanonicalRole.Direction) return true;
        var assignment = await dbContext.AssignmentVersions.AsNoTracking().SingleOrDefaultAsync(x => x.ObligationId == obligationId && x.Status == AssignmentVersionStatuses.Current, token);
        if (assignment is null) return false;
        var user = await dbContext.AppUsers.AsNoTracking().SingleOrDefaultAsync(x => x.PersonId == assignment.PersonId && x.Status == AccountStatus.Active, token);
        if (user is null) return false;
        var role = await dbContext.RoleAssignmentVersions.AsNoTracking().SingleOrDefaultAsync(x =>
            x.UserId == user.Id && x.BranchId == BranchScope.LorettaId &&
            x.Status == RoleAssignmentStatus.Active && x.ValidFrom <= now &&
            (x.ValidTo == null || now < x.ValidTo), token);
        return role is not null && RoleHierarchy.CanAccess(actor.RoleCode, role.RoleCode, actor.PersonId == assignment.PersonId);
    }

    private async Task<Actor?> ActorAsync(Guid userId, DateTimeOffset now, CancellationToken token) => await (
        from user in dbContext.AppUsers.AsNoTracking()
        join employment in dbContext.EmploymentVersions.AsNoTracking() on user.PersonId equals employment.PersonId
        join role in dbContext.RoleAssignmentVersions.AsNoTracking() on user.Id equals role.UserId
        where user.Id == userId && user.Status == AccountStatus.Active && user.MfaEnrolledAt != null &&
              employment.BranchId == BranchScope.LorettaId && employment.Status == EmploymentStatus.Active && employment.ValidFrom <= now && (employment.ValidTo == null || now < employment.ValidTo) &&
              role.BranchId == BranchScope.LorettaId && role.Status == RoleAssignmentStatus.Active && role.ValidFrom <= now && (role.ValidTo == null || now < role.ValidTo)
        select new Actor(user.Id, user.PersonId, role.RoleCode)).SingleOrDefaultAsync(token);

    private async Task<EvidenceRequirementVersion> RequireRequirementAsync(WorkObligation obligation, string code, CancellationToken token)
    {
        if (obligation.EvidencePolicyVersionId is null) throw new EvidenceRequirementInvalidException();
        return await dbContext.EvidenceRequirementVersions.AsNoTracking().SingleOrDefaultAsync(
            x => x.PolicyVersionId == obligation.EvidencePolicyVersionId && x.RequirementCode == code, token)
            ?? throw new EvidenceRequirementInvalidException();
    }

    private async Task RequireBinaryRequirementAsync(Access access, EvidenceRequirementVersion requirement, CancellationToken token)
    {
        EnsureBinaryKind(requirement);
        if (requirement.ConditionCode == EvidenceConditionCodes.Always) return;
        if (requirement.ConditionCode != EvidenceConditionCodes.DifferenceOrDamage) throw new EvidenceConditionUnresolvedException();

        var payloads = await (from version in dbContext.EvidenceVersions.AsNoTracking()
                              join item in dbContext.EvidenceItems.AsNoTracking() on version.EvidenceItemId equals item.Id
                              where item.ObligationId == access.Obligation.Id &&
                                    item.EvidencePolicyVersionId == access.Obligation.EvidencePolicyVersionId &&
                                    item.RequirementCode == "F_ENT_001" &&
                                    version.Status == EvidenceVersionStatuses.Current
                              select version.StructuredPayload).Take(2).ToListAsync(token);
        if (payloads.Count != 1 || payloads[0] is null ||
            !StructuredEvidencePayloadValidator.TryResolveDifferenceOrDamage(payloads[0]!.RootElement, out var applies))
            throw new EvidenceConditionUnresolvedException();
        if (!applies) throw new EvidenceConditionalRequirementException();
    }

    private static void EnsureBinaryKind(EvidenceRequirementVersion requirement)
    {
        if (requirement.Kind is not (EvidenceRequirementKinds.Photograph or EvidenceRequirementKinds.ReferencedDocument))
            throw new EvidenceTypeNotImplementedException();
    }

    private static JsonDocument RequireStructuredPayload(Access access, EvidenceRequirementVersion requirement, JsonDocument payload)
    {
        if (requirement.ConditionCode != EvidenceConditionCodes.Always ||
            requirement.Kind is not (EvidenceRequirementKinds.DigitalRecord or EvidenceRequirementKinds.StructuredData or
                EvidenceRequirementKinds.StructuredChecklist or EvidenceRequirementKinds.ReferencedForm))
        {
            throw new EvidenceTypeNotImplementedException();
        }

        return StructuredEvidencePayloadValidator.ValidateAndCanonicalize(
            access.TaskCode, requirement.RequirementCode, requirement.Kind, payload.RootElement);
    }

    private async Task<IdempotencyRecord?> FindReplayAsync(
        string scope,
        string legacyScope,
        Guid key,
        string hash,
        string legacyHash,
        Guid actorUserId,
        Guid correlationId,
        string operation,
        Guid resourceId,
        CancellationToken token)
    {
        var record = await dbContext.IdempotencyRecords.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Scope == scope && x.Key == key, token)
            ?? await dbContext.IdempotencyRecords.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Scope == legacyScope && x.Key == key, token);
        if (record is not null)
        {
            var expectedHash = record.ProtocolVersion == IdempotencyProtocol.CurrentVersion ? hash : legacyHash;
            if (!string.Equals(record.RequestHash, expectedHash, StringComparison.Ordinal))
            {
                await AuditIdempotencyConflictAsync(
                    actorUserId, correlationId, key, operation, resourceId, token);
                throw new EvidenceIdempotencyConflictException();
            }
        }

        return record;
    }

    private Task AuditIdempotencyConflictAsync(
        Guid actorUserId,
        Guid correlationId,
        Guid key,
        string operation,
        Guid resourceId,
        CancellationToken token) => IdempotencyProtocol.PersistConflictAsync(
            auditTransaction,
            IdempotencyProtocol.ConflictAudit(
                uuidGenerator.NewUuid(), clock.UtcNow, actorUserId,
                operation is "FILE_UPLOAD_INTENT_CREATE" or "FILE_UPLOAD_COMPLETE" ? "FILE_OBJECT" : "EVIDENCE_VERSION",
                resourceId,
                BranchScope.LorettaId, correlationId, key, operation),
            token);

    private void AddIdempotency(
        string scope,
        Guid key,
        string hash,
        string resourceType,
        Guid resourceId,
        int responseCode,
        DateTimeOffset now,
        object responsePayload,
        DateTimeOffset expiresAt,
        string? responseLocation = null) =>
        dbContext.IdempotencyRecords.Add(IdempotencyProtocol.Completed(
            scope, key, hash, resourceType, resourceId, responseCode,
            responsePayload, now, expiresAt, responseLocation: responseLocation));

    private async Task<EvidenceDetails> LoadDetailsAsync(Guid versionId, CancellationToken token)
    {
        var row = await (from version in dbContext.EvidenceVersions.AsNoTracking()
                         join item in dbContext.EvidenceItems.AsNoTracking() on version.EvidenceItemId equals item.Id
                         join fileRow in dbContext.FileObjects.AsNoTracking() on version.FileObjectId equals (Guid?)fileRow.Id into fileRows
                         from file in fileRows.DefaultIfEmpty()
                         where version.Id == versionId
                         select new { item, version, file }).SingleAsync(token);
        return await WithFilesAsync(Map(row.item, row.version, row.file), token);
    }

    private static EvidenceDetails Map(EvidenceItem item, EvidenceVersion version, FileObject? file) => new(
        item.Id, item.RowVersion, new(item.RequirementVersionId, item.RequirementCode, item.RequirementKind),
        new(version.Id, version.VersionNo, version.Status, version.SubmittedBy, version.SubmittedAt, version.Reason, version.SupersedesId),
        file is null ? null : new(file.Id, file.OriginalName, file.DetectedMediaType ?? file.DeclaredMediaType, file.SizeBytes, file.Sha256, file.DocumentSubtype),
        version.StructuredPayload);

    private static Guid[] NormalizeFileIds(Guid? primary, IReadOnlyList<Guid>? ids)
    {
        if (ids is null) return primary.HasValue ? [primary.Value] : [];
        if (ids.Count is < 1 or > 5 || primary != ids[0] || ids.Any(x => x == Guid.Empty) || ids.Distinct().Count() != ids.Count)
            throw new EvidenceRequestInvalidException();
        return ids.ToArray();
    }
    private async Task<List<FileObject>> RequireFileSetAsync(Guid[] ids, Guid actor, WorkObligation obligation,
        EvidenceRequirementVersion requirement, CancellationToken token)
    {
        if (requirement.Kind == EvidenceRequirementKinds.Photograph && !configuration.GetValue("Evidence:Camera:Enabled", true))
            throw new EvidenceTypeNotImplementedException();
        if (ids.Length is < 1 or > 5 || requirement.Kind != EvidenceRequirementKinds.Photograph && ids.Length != 1)
            throw new EvidenceRequestInvalidException();
        var result = new List<FileObject>();
        // Lock in a stable order to avoid deadlocks between overlapping requests.
        foreach (var id in ids.Order()) result.Add(await RequireLinkableFileAsync(id, actor, obligation, requirement, token));
        if (requirement.Kind == EvidenceRequirementKinds.Photograph &&
            (result.Any(x => x.CaptureSessionId is null || x.CaptureExpiresAt <= clock.UtcNow) ||
             result.Select(x => x.CaptureSessionId).Distinct().Count() != 1 ||
             result.Sum(x => x.ReceivedSizeBytes ?? x.SizeBytes) > 31457280 || result.Sum(x => x.SizeBytes) > 31457280))
            throw new EvidenceRequestInvalidException();
        return ids.Select(id => result.Single(x => x.Id == id)).ToList();
    }
    private static void AddVersionFiles(EvidenceVersion version, List<FileObject> files)
    {
        for (var i = 0; i < files.Count; i++) version.Files.Add(new() { EvidenceVersionId = version.Id, FileObjectId = files[i].Id, Position = i });
        if (files.Count > 0) version.SealFiles();
    }
    private static EvidenceFileDetails MapFile(FileObject file) => new(file.Id, file.OriginalName,
        file.DetectedMediaType ?? file.DeclaredMediaType, file.SizeBytes, file.Sha256, file.DocumentSubtype);
    private async Task<EvidenceDetails> WithFilesAsync(EvidenceDetails detail, CancellationToken token)
    {
        var rows = await (from link in dbContext.Set<EvidenceVersionFile>().AsNoTracking()
                          join file in dbContext.FileObjects.AsNoTracking() on link.FileObjectId equals file.Id
                          where link.EvidenceVersionId == detail.Version.EvidenceVersionId
                          orderby link.Position select file).ToArrayAsync(token);
        var count = await dbContext.EvidenceVersions.Where(v => v.Id == detail.Version.EvidenceVersionId).Select(v => v.FileCount).SingleAsync(token);
        if (rows.Length != count) throw new EvidenceFileStateException();
        return detail with { Files = rows.Length > 0 ? rows.Select(MapFile).ToArray() : detail.File is null ? [] : [detail.File] };
    }

    private static EvidenceFileStatusDetails ToStatus(FileObject file) => new(file.Id,
        file.UploadCompletedAt is null && file.ScanStatus == EvidenceFileStatuses.Pending ? "PENDIENTE_CARGA" :
        file.ScanStatus == EvidenceFileStatuses.Pending ? "PENDIENTE_ESCANEO" : file.ScanStatus,
        file.OriginalName, file.DeclaredMediaType, file.DetectedMediaType, file.SizeBytes, file.Sha256,
        file.CreatedAt, file.UploadExpiresAt, file.UploadCompletedAt, file.ScannedAt, file.ScanErrorCode, file.LinkedEvidenceItemId);

    private static EvidenceObjectMetadata Metadata(FileObject file) => new(EvidenceObjectKey.Parse(file.ObjectKey), file.SizeBytes, file.Sha256,
        file.DeclaredMediaType switch { "image/jpeg" => EvidenceMediaType.Jpeg, "image/png" => EvidenceMediaType.Png, "application/pdf" => EvidenceMediaType.Pdf, _ => throw new EvidenceUnsupportedMediaTypeException() });

    private static CreateEvidenceUploadCommand Normalize(CreateEvidenceUploadCommand value)
    {
        if (value.ActorUserId == Guid.Empty || value.IdempotencyKey == Guid.Empty || value.CorrelationId == Guid.Empty || value.ObligationId == Guid.Empty ||
            string.IsNullOrWhiteSpace(value.RequirementCode) || string.IsNullOrWhiteSpace(value.OriginalFileName) ||
            string.IsNullOrWhiteSpace(value.DeclaredMediaType) || value.SizeBytes < 1 || value.Sha256 is null ||
            value.Sha256.Length != 64 || value.Sha256.Any(c => c is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
            throw new EvidenceRequestInvalidException();
        if (value.SizeBytes > EvidenceFileLimits.MaximumBytes) throw new EvidenceFileTooLargeException();
        var name = Path.GetFileName(value.OriginalFileName).Normalize(NormalizationForm.FormC).Trim();
        if (name.Length is < 1 or > 255 || name != value.OriginalFileName.Trim() || name.Any(c => char.IsControl(c) || c is '/' or '\\')) throw new EvidenceRequestInvalidException();
        return value with { OriginalFileName = name };
    }

    private static void ValidateBinaryContract(EvidenceRequirementVersion requirement, string mediaType, string name, string? subtype)
    {
        var extension = Path.GetExtension(name).ToLowerInvariant();
        var valid = requirement.Kind switch
        {
            EvidenceRequirementKinds.Photograph => (mediaType == "image/jpeg" && extension is ".jpg" or ".jpeg") || (mediaType == "image/png" && extension == ".png"),
            EvidenceRequirementKinds.ReferencedDocument => mediaType == "application/pdf" && extension == ".pdf",
            _ => false
        };
        if (!valid) throw new EvidenceUnsupportedMediaTypeException();
        var reception = requirement.RequirementCode == "DOCUMENTO_RECEPCION";
        if (reception != (subtype is "NOTA" or "REMISION" or "FACTURA") || (!reception && subtype is not null)) throw new EvidenceRequestInvalidException();
    }

    private static string? NormalizeReason(string? value, bool required)
    {
        var normalized = value?.Normalize(NormalizationForm.FormC).Trim();
        if (string.IsNullOrEmpty(normalized)) { if (required) throw new EvidenceReasonRequiredException(); return null; }
        if (normalized.Length > 500 || normalized.Any(c => char.IsControl(c)) || normalized.Contains('<') || normalized.Contains('>') || Uri.TryCreate(normalized, UriKind.Absolute, out _)) throw new EvidenceRequestInvalidException();
        return normalized;
    }

    private static void EnsureReasonDoesNotReferenceFiles(string? reason, params FileObject[] files)
    {
        if (reason is null) return;
        foreach (var file in files)
        {
            if (reason.Contains(file.OriginalName, StringComparison.OrdinalIgnoreCase) ||
                reason.Contains(file.ObjectKey, StringComparison.Ordinal) ||
                reason.Contains(file.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new EvidenceRequestInvalidException();
            }
        }
    }

    private static string LegacyScope(string operation, Guid actor, Guid resource) => $"EVIDENCE:{operation}:{actor:D}:{resource:D}";
    private static string? Constraint(DbUpdateException exception) =>
        (exception.InnerException as PostgresException)?.ConstraintName;
    private static bool IsEvidenceVersionRace(Exception exception)
    {
        var postgres = exception as PostgresException ?? (exception as DbUpdateException)?.InnerException as PostgresException;
        return postgres?.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected ||
            postgres?.ConstraintName is EvidenceVersionConfiguration.CurrentVersionIndex or
                "IX_evidence_version_evidence_item_id_version_no" or
                "IX_evidence_version_file_object_id" or
                "IX_evidence_version_supersedes_id";
    }
    private static string Hash<T>(T value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    private static string EncodeCursor(EvidenceCursor value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static EvidenceCursor DecodeCursor(string value)
    {
        try
        {
            var base64 = value.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + ((4 - base64.Length % 4) % 4), '=');
            return JsonSerializer.Deserialize<EvidenceCursor>(Encoding.UTF8.GetString(Convert.FromBase64String(base64)))
                ?? throw new EvidenceRequestInvalidException();
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            throw new EvidenceRequestInvalidException();
        }
    }
    private AuditEvent Audit(string action, string resourceType, Guid resourceId, Guid actor, Guid correlation, DateTimeOffset at,
        object? before, object? after, string? reason = null) => new()
        {
            Id = uuidGenerator.NewUuid(),
            OccurredAt = at,
            ActorUserId = actor,
            ActorType = "USER",
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId,
            BranchId = BranchScope.LorettaId,
            CorrelationId = correlation,
            BeforeData = before is null ? null : JsonSerializer.SerializeToDocument(before),
            AfterData = after is null ? null : JsonSerializer.SerializeToDocument(after),
            Reason = reason,
            Outcome = "SUCCESS"
        };

    private sealed record Actor(Guid UserId, Guid PersonId, string RoleCode);
    private sealed record Access(WorkObligation Obligation, Actor Actor, Guid ResponsiblePersonId, string ResponsibleRoleCode, string TaskCode);
    private sealed record EvidenceCursor(int Version, Guid ObligationId, string? RequirementCode, string? Status,
        short Ordinal, int VersionNo, Guid EvidenceVersionId);
}
