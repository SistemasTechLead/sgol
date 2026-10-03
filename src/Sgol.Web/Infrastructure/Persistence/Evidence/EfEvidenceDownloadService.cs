using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sgol.Assignment.Contracts;
using Sgol.BuildingBlocks.Time;
using Sgol.Configuration.Contracts;
using Sgol.Evidence.Contracts;
using Sgol.Generation.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Evidence;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;

namespace Sgol.Web.Infrastructure.Persistence.Evidence;

public sealed class EfEvidenceDownloadService(
    SgolDbContext dbContext,
    IPrivateObjectStorage storage,
    IClock clock,
    IOptions<EvidenceDownloadOptions> options) : IEvidenceDownloadService
{
    public async Task RequireActorAsync(Guid actorUserId, CancellationToken cancellationToken = default) =>
        _ = await ActorAsync(actorUserId, clock.UtcNow, cancellationToken);

    public async Task<EvidenceDownloadDetails> AuthorizeAsync(
        Guid actorUserId, Guid fileId, CancellationToken cancellationToken = default)
    {
        var initialAt = clock.UtcNow;
        var actor = await ActorAsync(actorUserId, initialAt, cancellationToken);
        var checkedFile = await ReadAsync(actor, fileId, initialAt, cancellationToken);
        try
        {
            var actual = await storage.GetMetadataAsync(EvidenceStorageArea.Clean, checkedFile.Metadata.Key, cancellationToken);
            if (actual != checkedFile.Metadata) throw Unavailable();
        }
        catch (EvidenceDownloadException) { throw; }
        catch (Exception ex) when (ex is EvidenceStorageUnavailableException or EvidenceObjectNotFoundException or EvidenceObjectIntegrityException)
        { throw Unavailable(); }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", cancellationToken);
        // S3 signing uses whole UTC seconds. Capture once at that precision for this final snapshot.
        var capturedAt = clock.UtcNow;
        var authorizedAt = DateTimeOffset.FromUnixTimeSeconds(capturedAt.ToUnixTimeSeconds());
        // Never let S3's second precision extend authority that expired within the current second.
        var currentActor = await ActorAsync(actorUserId, capturedAt, cancellationToken);
        var currentFile = await ReadAsync(currentActor, fileId, capturedAt, cancellationToken);
        actor = await ActorAsync(actorUserId, authorizedAt, cancellationToken);
        var finalFile = await ReadAsync(actor, fileId, authorizedAt, cancellationToken);
        if (actor != currentActor || finalFile != currentFile || finalFile != checkedFile) throw Inconsistent();
        int validity;
        try { validity = options.Value.ValiditySeconds; }
        catch (OptionsValidationException) { throw Unavailable(); }
        if (validity is < 1 or > 300) throw Unavailable();
        var expiresAt = authorizedAt.AddSeconds(validity);
        EvidenceDownloadAuthorization authorization;
        try
        {
            authorization = await storage.CreateCleanDownloadAuthorizationAsync(
                finalFile.Metadata, fileId, expiresAt, cancellationToken);
        }
        catch (Exception ex) when (ex is EvidenceStorageUnavailableException or EvidenceObjectNotFoundException or
            EvidenceObjectIntegrityException or OptionsValidationException)
        { throw Unavailable(); }
        if (authorization.ExpiresAt != expiresAt || !authorization.Url.IsAbsoluteUri ||
            authorization.Url.Scheme is not ("https" or "http") || clock.UtcNow >= expiresAt) throw Unavailable();
        await transaction.CommitAsync(cancellationToken);
        if (clock.UtcNow >= expiresAt) throw Unavailable();
        return new(fileId, authorization);
    }

    private async Task<Actor> ActorAsync(Guid userId, DateTimeOffset at, CancellationToken token)
    {
        var actors = await (
            from user in dbContext.AppUsers.AsNoTracking()
            join employment in dbContext.EmploymentVersions.AsNoTracking() on user.PersonId equals employment.PersonId
            join role in dbContext.RoleAssignmentVersions.AsNoTracking() on user.Id equals role.UserId
            where user.Id == userId && user.Status == AccountStatus.Active && user.MfaEnrolledAt != null &&
                employment.BranchId == BranchScope.LorettaId && employment.Status == EmploymentStatus.Active &&
                employment.ValidFrom <= at && (employment.ValidTo == null || at < employment.ValidTo) &&
                role.BranchId == BranchScope.LorettaId && role.Status == RoleAssignmentStatus.Active &&
                role.ValidFrom <= at && (role.ValidTo == null || at < role.ValidTo)
            select new Actor(user.PersonId, role.RoleCode)).Take(2).ToListAsync(token);
        if (actors.Count != 1 || actors[0].RoleCode is not (CanonicalRole.Direction or CanonicalRole.Administration or
            CanonicalRole.Subcoordination or CanonicalRole.SalesFloor))
            throw new EvidenceDownloadException(403, "ACCESO_DENEGADO");
        return actors[0];
    }

    private IQueryable<WorkObligation> Visible(Actor actor, DateTimeOffset at)
    {
        var obligations = dbContext.WorkObligations.AsNoTracking().Where(o => o.BranchId == BranchScope.LorettaId);
        if (actor.RoleCode == CanonicalRole.Direction) return obligations;
        var ids = from assignment in dbContext.AssignmentVersions.AsNoTracking()
                  join user in dbContext.AppUsers.AsNoTracking() on assignment.PersonId equals user.PersonId
                  join employment in dbContext.EmploymentVersions.AsNoTracking() on user.PersonId equals employment.PersonId
                  join role in dbContext.RoleAssignmentVersions.AsNoTracking() on user.Id equals role.UserId
                  where assignment.Status == AssignmentVersionStatuses.Current && user.Status == AccountStatus.Active &&
                      employment.BranchId == BranchScope.LorettaId && employment.Status == EmploymentStatus.Active &&
                      employment.ValidFrom <= at && (employment.ValidTo == null || at < employment.ValidTo) &&
                      role.BranchId == BranchScope.LorettaId && role.Status == RoleAssignmentStatus.Active &&
                      role.ValidFrom <= at && (role.ValidTo == null || at < role.ValidTo) &&
                      (role.RoleCode == CanonicalRole.SalesFloor || role.RoleCode == CanonicalRole.Subcoordination ||
                       role.RoleCode == CanonicalRole.Administration || role.RoleCode == CanonicalRole.Direction) &&
                      (assignment.PersonId == actor.PersonId ||
                       actor.RoleCode == CanonicalRole.Subcoordination && role.RoleCode == CanonicalRole.SalesFloor ||
                       actor.RoleCode == CanonicalRole.Administration &&
                           (role.RoleCode == CanonicalRole.SalesFloor || role.RoleCode == CanonicalRole.Subcoordination)) &&
                      dbContext.EmploymentVersions.Count(e => e.PersonId == user.PersonId && e.BranchId == BranchScope.LorettaId &&
                          e.Status == EmploymentStatus.Active && e.ValidFrom <= at && (e.ValidTo == null || at < e.ValidTo)) == 1 &&
                      dbContext.RoleAssignmentVersions.Count(r => r.UserId == user.Id && r.BranchId == BranchScope.LorettaId &&
                          r.Status == RoleAssignmentStatus.Active && r.ValidFrom <= at && (r.ValidTo == null || at < r.ValidTo)) == 1
                  select assignment.ObligationId;
        return obligations.Where(o => ids.Contains(o.Id));
    }

    private async Task<CheckedFile> ReadAsync(Actor actor, Guid fileId, DateTimeOffset at, CancellationToken token)
    {
        var row = await (from file in dbContext.FileObjects.AsNoTracking()
                         join obligation in Visible(actor, at) on file.ObligationId equals obligation.Id
                         where file.Id == fileId && file.BranchId == BranchScope.LorettaId
                         select new { file, obligation }).SingleOrDefaultAsync(token);
        if (row is null || row.file.LinkedEvidenceItemId is null) throw NotFound();
        var versions = await dbContext.EvidenceVersions.AsNoTracking().Where(v => v.FileObjectId == fileId).Take(2).ToListAsync(token);
        if (versions.Count == 0) throw Inconsistent();
        if (versions.Count != 1) throw Inconsistent();
        var version = versions[0];
        var item = await dbContext.EvidenceItems.AsNoTracking().SingleOrDefaultAsync(i => i.Id == version.EvidenceItemId, token);
        if (item is null || item.Id != row.file.LinkedEvidenceItemId || item.ObligationId != row.obligation.Id ||
            item.EvidencePolicyVersionId != row.file.EvidencePolicyVersionId ||
            item.RequirementVersionId != row.file.RequirementVersionId ||
            item.RequirementCode != row.file.RequirementCode || item.RequirementKind != row.file.RequirementKind ||
            row.obligation.EvidencePolicyVersionId != row.file.EvidencePolicyVersionId ||
            version.Status is not (EvidenceVersionStatuses.Current or EvidenceVersionStatuses.Superseded)) throw Inconsistent();
        var requirement = await dbContext.EvidenceRequirementVersions.AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == row.file.RequirementVersionId, token);
        var policy = await dbContext.EvidencePolicyVersions.AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == row.file.EvidencePolicyVersionId, token);
        if (requirement is null || policy is null || requirement.PolicyVersionId != policy.Id ||
            requirement.TaskDefinitionId != policy.TaskDefinitionId || policy.TaskDefinitionVersionId != row.obligation.TaskDefinitionVersionId ||
            requirement.RequirementCode != item.RequirementCode || requirement.Kind != item.RequirementKind ||
            requirement.Kind is not (EvidenceRequirementKinds.Photograph or EvidenceRequirementKinds.ReferencedDocument)) throw Inconsistent();
        if (row.file.ScanStatus != EvidenceFileStatuses.Clean || row.file.BucketClass != EvidenceBucketClasses.Clean)
            throw new EvidenceDownloadException(422, "ARCHIVO_NO_LIMPIO");
        var media = row.file.DetectedMediaType switch
        {
            "image/jpeg" => EvidenceMediaType.Jpeg,
            "image/png" => EvidenceMediaType.Png,
            "application/pdf" => EvidenceMediaType.Pdf,
            _ => throw Inconsistent()
        };
        if (row.file.DeclaredMediaType != row.file.DetectedMediaType ||
            !EvidenceObjectKey.TryParse(row.file.ObjectKey, out var key)) throw Inconsistent();
        return new(fileId, row.obligation.Id, item.Id, version.Id, policy.Id, requirement.Id,
            new(key, row.file.SizeBytes, row.file.Sha256, media));
    }

    private static EvidenceDownloadException NotFound() => new(404, "ARCHIVO_NO_ENCONTRADO");
    private static EvidenceDownloadException Inconsistent() => new(500, "CADENA_EVIDENCIA_INCONSISTENTE");
    private static EvidenceDownloadException Unavailable() => new(503, "INFRAESTRUCTURA_EVIDENCIA_NO_DISPONIBLE");
    private sealed record Actor(Guid PersonId, string RoleCode);
    private sealed record CheckedFile(Guid FileId, Guid ObligationId, Guid ItemId, Guid VersionId,
        Guid PolicyId, Guid RequirementId, EvidenceObjectMetadata Metadata);
}
