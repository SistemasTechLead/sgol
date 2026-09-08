using System.Data;
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
using Sgol.Web.Infrastructure.Evidence;

namespace Sgol.Web.Infrastructure.Persistence.Evidence;

public sealed class EfEvidenceContributionService(
    SgolDbContext dbContext,
    AuditTransaction auditTransaction,
    IPrivateObjectStorage storage,
    IEvidenceObjectKeyFactory keyFactory,
    IOutboxWriter outboxWriter,
    IClock clock,
    IUuidGenerator uuidGenerator) : IEvidenceContributionService
{
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
        if (access.Obligation.ExecutionStatus == WorkObligationStatuses.Concluded &&
            !await dbContext.EvidenceItems.AsNoTracking().AnyAsync(x => x.ObligationId == input.ObligationId && x.RequirementVersionId == requirement.Id, cancellationToken))
        {
            throw new EvidenceItemNotFoundException();
        }

        var scope = Scope("UPLOAD", input.ActorUserId, input.ObligationId);
        var hash = Hash(input);
        var replay = await FindReplayAsync(scope, input.IdempotencyKey, hash, cancellationToken);
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
                AddIdempotency(scope, input.IdempotencyKey, hash, "FILE_OBJECT", id, 201, now);
                return Audit("EVIDENCE_UPLOAD_INTENT_CREATED", "FILE_OBJECT", id, input.ActorUserId,
                    input.CorrelationId, now, null, new { fileId = id, status = "PENDIENTE_CARGA" });
            }, cancellationToken);
        }
        catch (DbUpdateException exception) when (Constraint(exception) == "PK_idempotency_record")
        {
            dbContext.ChangeTracker.Clear();
            var concurrent = await FindReplayAsync(scope, input.IdempotencyKey, hash, cancellationToken)
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
        var scope = Scope("COMPLETE", command.ActorUserId, command.FileId);
        var hash = Hash(new { command.FileId });
        var replay = await FindReplayAsync(scope, command.IdempotencyKey, hash, cancellationToken);
        if (replay is not null)
        {
            if (replay.ResponseCode != 202) throw new EvidenceFileStateException();
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
                    AddIdempotency(scope, command.IdempotencyKey, hash, "FILE_OBJECT", file.Id, 422, now);
                    return Audit("EVIDENCE_UPLOAD_REJECTED", "FILE_OBJECT", file.Id, command.ActorUserId,
                        command.CorrelationId, now, new { status = EvidenceFileStatuses.Pending }, new { status = file.ScanStatus });
                }

                file.ConfirmUpload(now);
                outboxWriter.Enqueue(InspectionEvent, file.Id,
                    JsonSerializer.SerializeToElement(new { fileObjectId = file.Id }), command.CorrelationId);
                AddIdempotency(scope, command.IdempotencyKey, hash, "FILE_OBJECT", file.Id, 202, now);
                return Audit("EVIDENCE_UPLOAD_COMPLETED", "FILE_OBJECT", file.Id, command.ActorUserId,
                    command.CorrelationId, now, new { status = "PENDIENTE_CARGA" }, new { status = "PENDIENTE_ESCANEO" });
            }, cancellationToken);
        }
        catch (DbUpdateException exception) when (Constraint(exception) == "PK_idempotency_record")
        {
            dbContext.ChangeTracker.Clear();
            var concurrent = await FindReplayAsync(scope, command.IdempotencyKey, hash, cancellationToken)
                ?? throw new EvidenceIdempotencyConflictException();
            file = await dbContext.FileObjects.AsNoTracking().SingleAsync(x => x.Id == concurrent.ResourceId, cancellationToken);
            if (concurrent.ResponseCode != 202) throw new EvidenceFileStateException();
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
        var scope = Scope("CONTRIBUTE", command.ActorUserId, command.ObligationId);
        var hash = Hash(new { command.ObligationId, command.RequirementCode, command.FileId, structuredPayload = structured?.RootElement });
        var replay = await FindReplayAsync(scope, command.IdempotencyKey, hash, cancellationToken);
        if (replay is not null) return await LoadDetailsAsync(replay.ResourceId, cancellationToken);
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
                EvidenceVersion version;
                if (command.FileId.HasValue)
                {
                    await RequireBinaryRequirementAsync(access, requirement, token);
                    file = await RequireLinkableFileAsync(command.FileId.Value, command.ActorUserId, access.Obligation, requirement, now, token);
                    file.Link(item.Id, now);
                    version = new EvidenceVersion(uuidGenerator.NewUuid(), item.Id, 1, file.Id, command.ActorUserId, now);
                }
                else
                {
                    version = new EvidenceVersion(uuidGenerator.NewUuid(), item.Id, 1, structured!, command.ActorUserId, now);
                }
                dbContext.EvidenceItems.Add(item);
                dbContext.EvidenceVersions.Add(version);
                AddIdempotency(scope, command.IdempotencyKey, hash, "EVIDENCE_VERSION", version.Id, 201, now);
                result = Map(item, version, file);
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
            var concurrent = await FindReplayAsync(scope, command.IdempotencyKey, hash, cancellationToken)
                ?? throw new EvidenceIdempotencyConflictException();
            return await LoadDetailsAsync(concurrent.ResourceId, cancellationToken);
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
        var scope = Scope("REPLACE", command.ActorUserId, command.EvidenceItemId);
        var requestHash = Hash(new
        {
            command.ObligationId,
            command.EvidenceItemId,
            command.FileId,
            structuredPayload = requestedStructured?.RootElement,
            reason,
            command.ExpectedRowVersion
        });
        var existingReplay = await FindReplayAsync(scope, command.IdempotencyKey, requestHash, cancellationToken);
        if (existingReplay is not null) return await LoadDetailsAsync(existingReplay.ResourceId, cancellationToken);
        if (command.FileId.HasValue) await RequireBinaryRequirementAsync(access, requestedRequirement, cancellationToken);
        EvidenceDetails? result = null;
        try
        {
            await auditTransaction.ExecuteAsync(IsolationLevel.Serializable, async token =>
            {
                var item = await dbContext.EvidenceItems.SingleOrDefaultAsync(x => x.Id == command.EvidenceItemId && x.ObligationId == command.ObligationId, token)
                    ?? throw new EvidenceItemNotFoundException();
                item.Advance(command.ExpectedRowVersion);
                var current = await dbContext.EvidenceVersions.SingleAsync(x => x.EvidenceItemId == item.Id && x.Status == EvidenceVersionStatuses.Current, token);
                var requirement = await dbContext.EvidenceRequirementVersions.SingleAsync(x => x.Id == item.RequirementVersionId, token);
                JsonDocument? structured = null;
                FileObject? file = null;
                if (command.FileId.HasValue)
                {
                    await RequireBinaryRequirementAsync(access, requirement, token);
                    file = await RequireLinkableFileAsync(command.FileId.Value, command.ActorUserId, access.Obligation, requirement, now, token);
                }
                else
                {
                    structured = RequireStructuredPayload(access, requirement, command.StructuredPayload!);
                }
                var files = new List<FileObject>();
                if (file is not null) files.Add(file);
                if (current.FileObjectId.HasValue)
                    files.Add(await dbContext.FileObjects.AsNoTracking().SingleAsync(x => x.Id == current.FileObjectId.Value, token));
                EnsureReasonDoesNotReferenceFiles(reason, [.. files]);
                current.Supersede();
                EvidenceVersion successor;
                if (file is not null)
                {
                    file.Link(item.Id, now);
                    successor = new EvidenceVersion(uuidGenerator.NewUuid(), item.Id, current.VersionNo + 1,
                        file.Id, command.ActorUserId, now, reason, current.Id);
                }
                else
                {
                    successor = new EvidenceVersion(uuidGenerator.NewUuid(), item.Id, current.VersionNo + 1,
                        structured!, command.ActorUserId, now, reason, current.Id);
                }
                dbContext.EvidenceVersions.Add(successor);
                AddIdempotency(scope, command.IdempotencyKey, requestHash, "EVIDENCE_VERSION", successor.Id, 201, now);
                result = Map(item, successor, file);
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
            var concurrent = await FindReplayAsync(scope, command.IdempotencyKey, requestHash, cancellationToken)
                ?? throw new EvidenceIdempotencyConflictException();
            return await LoadDetailsAsync(concurrent.ResourceId, cancellationToken);
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
        var items = materialized.Take(query.Limit).Select(x => Map(x.item, x.version, x.file)).ToArray();
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
        EvidenceRequirementVersion requirement, DateTimeOffset now, CancellationToken token)
    {
        var file = await dbContext.FileObjects.SingleOrDefaultAsync(x => x.Id == id, token) ?? throw new EvidenceFileNotFoundException();
        if (file.UploadedBy != actor || file.ObligationId != obligation.Id ||
            file.EvidencePolicyVersionId != obligation.EvidencePolicyVersionId || file.RequirementVersionId != requirement.Id)
            throw new EvidenceFileNotFoundException();
        if (file.LinkedEvidenceItemId is not null) throw new EvidenceFileAlreadyLinkedException();
        if (file.ScanStatus != EvidenceFileStatuses.Clean || file.LinkExpiresAt is null || now > file.LinkExpiresAt)
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
        var permitted = obligation.ExecutionStatus == WorkObligationStatuses.Pending
            ? actor.PersonId == assignment.PersonId
            : RoleHierarchy.IsStrictlySuperior(actor.RoleCode, responsibleRole.RoleCode) && actor.RoleCode != CanonicalRole.SalesFloor;
        if (!permitted) throw new EvidenceObligationNotFoundException();
        return new Access(obligation, actor, assignment.PersonId, responsibleRole.RoleCode, taskCode);
    }

    private async Task<bool> CanViewAsync(Guid actorId, Guid obligationId, DateTimeOffset now, CancellationToken token)
    {
        var actor = await ActorAsync(actorId, now, token);
        if (actor is null) throw new EvidenceAccessDeniedException();
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

    private async Task<IdempotencyRecord?> FindReplayAsync(string scope, Guid key, string hash, CancellationToken token)
    {
        var record = await dbContext.IdempotencyRecords.AsNoTracking().SingleOrDefaultAsync(x => x.Scope == scope && x.Key == key, token);
        if (record is not null && record.RequestHash != hash) throw new EvidenceIdempotencyConflictException();
        return record;
    }

    private void AddIdempotency(string scope, Guid key, string hash, string resourceType, Guid resourceId, int responseCode, DateTimeOffset now) =>
        dbContext.IdempotencyRecords.Add(new IdempotencyRecord { Scope = scope, Key = key, RequestHash = hash, Status = "COMPLETADA", ResourceType = resourceType, ResourceId = resourceId, ResponseCode = responseCode, CreatedAt = now, ExpiresAt = now + IdempotencyRetention });

    private async Task<EvidenceDetails> LoadDetailsAsync(Guid versionId, CancellationToken token)
    {
        var row = await (from version in dbContext.EvidenceVersions.AsNoTracking()
                         join item in dbContext.EvidenceItems.AsNoTracking() on version.EvidenceItemId equals item.Id
                         join fileRow in dbContext.FileObjects.AsNoTracking() on version.FileObjectId equals (Guid?)fileRow.Id into fileRows
                         from file in fileRows.DefaultIfEmpty()
                         where version.Id == versionId
                         select new { item, version, file }).SingleAsync(token);
        return Map(row.item, row.version, row.file);
    }

    private static EvidenceDetails Map(EvidenceItem item, EvidenceVersion version, FileObject? file) => new(
        item.Id, item.RowVersion, new(item.RequirementVersionId, item.RequirementCode, item.RequirementKind),
        new(version.Id, version.VersionNo, version.Status, version.SubmittedBy, version.SubmittedAt, version.Reason, version.SupersedesId),
        file is null ? null : new(file.Id, file.OriginalName, file.DetectedMediaType ?? file.DeclaredMediaType, file.SizeBytes, file.Sha256, file.DocumentSubtype),
        version.StructuredPayload);

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

    private static string Scope(string operation, Guid actor, Guid resource) => $"EVIDENCE:{operation}:{actor:D}:{resource:D}";
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
