using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sgol.Assignment.Contracts;
using Sgol.Evidence.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Evidence;
using Sgol.Web.Infrastructure.Persistence.Evidence;
using Xunit;

namespace Sgol.IntegrationTests;

public sealed partial class ObligationQueryPersistenceTests
{
    [Fact]
    public async Task TechEvid003DownloadPendingBinaryHidesPeerAndSuperiorBeforeStorage()
    {
        var scenario = await ResetAndSeedAsync();
        await using var db = CreateContext();
        var obligation = await db.WorkObligations.AsNoTracking().SingleAsync(o => o.Id == scenario.AdministrationObligationId);
        var requirement = await db.EvidenceRequirementVersions.AsNoTracking().FirstAsync(r =>
            r.PolicyVersionId == obligation.EvidencePolicyVersionId &&
            (r.Kind == Sgol.Configuration.Contracts.EvidenceRequirementKinds.Photograph ||
             r.Kind == Sgol.Configuration.Contracts.EvidenceRequirementKinds.ReferencedDocument));
        var item = new EvidenceItem(Guid.CreateVersion7(), obligation.Id, requirement.PolicyVersionId,
            requirement.Id, requirement.RequirementCode, requirement.Kind, Now.AddMinutes(-1));
        var hash = Hash("synthetic pending download");
        var mediaType = requirement.Kind == Sgol.Configuration.Contracts.EvidenceRequirementKinds.Photograph ? "image/png" : "application/pdf";
        var file = new FileObject(Guid.CreateVersion7(), obligation.BranchId, obligation.Id,
            requirement.PolicyVersionId, requirement.Id, requirement.RequirementCode, requirement.Kind, null,
            $"v1/{hash[..2]}/{hash[2..4]}/{hash}", "synthetic", mediaType, 128, hash,
            scenario.Administration.UserId, Now.AddMinutes(-4));
        file.ConfirmUpload(Now.AddMinutes(-3)); file.MarkClean(mediaType, "synthetic", Now.AddMinutes(-2));
        file.Link(item.Id, Now.AddMinutes(-1));
        db.EvidenceItems.Add(item); db.FileObjects.Add(file);
        db.EvidenceVersions.Add(new EvidenceVersion(Guid.CreateVersion7(), item.Id, 1, file.Id,
            scenario.Administration.UserId, Now.AddMinutes(-1)));
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var before = await TechEvid003SnapshotAsync(db);
        var storage = new DownloadStorage(db);
        var service = DownloadService(db, storage);
        foreach (var actor in new[] { scenario.AdministrationPeer, scenario.Subcoordination, scenario.Sales })
            await DownloadFailureAsync(() => service.AuthorizeAsync(actor.UserId, file.Id), 404, "ARCHIVO_NO_ENCONTRADO");
        Assert.Equal(0, storage.MetadataCalls);
        foreach (var actor in new[] { scenario.Administration, scenario.Direction })
            Assert.Equal(file.Id, (await service.AuthorizeAsync(actor.UserId, file.Id)).FileId);
        Assert.Equal(before, await TechEvid003SnapshotAsync(db));
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task TechEvid003DownloadKeepsExactOldFileWhenReplacementCommitsBeforeFinalSnapshot()
    {
        var scenario = await ResetAndSeedAsync();
        await using var db = CreateContext();
        await ObligationConclusionTestData.ConcludeAsync(db, scenario.SalesObligationId, Now);
        var file = await db.FileObjects.AsNoTracking().FirstAsync(f => f.ObligationId == scenario.SalesObligationId);
        string? afterReplacement = null;
        var storage = new DownloadStorage(db)
        {
            AfterMetadata = async () =>
        {
            await using var other = CreateContext();
            await using var transaction = await other.Database.BeginTransactionAsync();
            var previous = await other.EvidenceVersions.SingleAsync(v => v.FileObjectId == file.Id);
            previous.Supersede(); await other.SaveChangesAsync();
            var successor = CloneFile(file);
            other.FileObjects.Add(successor);
            other.EvidenceVersions.Add(new EvidenceVersion(Guid.CreateVersion7(), previous.EvidenceItemId, 2,
                successor.Id, scenario.Direction.UserId, Now, "Synthetic replacement", previous.Id));
            await other.SaveChangesAsync(); await transaction.CommitAsync();
            afterReplacement = await TechEvid003SnapshotAsync(other);
        }
        };
        var result = await DownloadService(db, storage).AuthorizeAsync(scenario.Direction.UserId, file.Id);
        Assert.Equal(file.Id, result.FileId);
        Assert.Equal(afterReplacement, await TechEvid003SnapshotAsync(db));
        Assert.Equal(1, storage.Signatures);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task TechEvid003DownloadUsesCurrentHierarchyAndPreservesBothVersionsWithoutEffects()
    {
        var scenario = await ResetAndSeedAsync();
        await using var db = CreateContext();
        var id = scenario.SalesObligationId;
        await ObligationConclusionTestData.ConcludeAsync(db, id, Now);
        var first = await db.FileObjects.AsNoTracking().FirstAsync(f => f.ObligationId == id);
        var before = await TechEvid003SnapshotAsync(db);
        var storage = new DownloadStorage(db);
        var service = DownloadService(db, storage);
        foreach (var actor in new[] { scenario.Sales, scenario.Subcoordination, scenario.Administration, scenario.Direction })
        {
            var result = await service.AuthorizeAsync(actor.UserId, first.Id);
            Assert.Equal(first.Id, result.FileId);
            Assert.Equal(Now.AddMinutes(5), result.Download.ExpiresAt);
        }
        Assert.Equal(before, await TechEvid003SnapshotAsync(db));
        Assert.Equal(4, storage.Signatures);
        var oldVersion = await db.EvidenceVersions.SingleAsync(v => v.FileObjectId == first.Id);
        await using var replacementTransaction = await db.Database.BeginTransactionAsync();
        oldVersion.Supersede();
        await db.SaveChangesAsync();
        var second = CloneFile(first);
        db.FileObjects.Add(second);
        db.EvidenceVersions.Add(new EvidenceVersion(Guid.CreateVersion7(), oldVersion.EvidenceItemId, 2, second.Id,
            scenario.Direction.UserId, Now, "Synthetic replacement", oldVersion.Id));
        await db.SaveChangesAsync();
        await replacementTransaction.CommitAsync();
        await replacementTransaction.DisposeAsync();
        db.ChangeTracker.Clear();
        before = await TechEvid003SnapshotAsync(db);
        Assert.Equal(first.Id, (await service.AuthorizeAsync(scenario.Direction.UserId, first.Id)).FileId);
        Assert.Equal(second.Id, (await service.AuthorizeAsync(scenario.Direction.UserId, second.Id)).FileId);
        Assert.Equal(before, await TechEvid003SnapshotAsync(db));

        var currentAssignment = await db.AssignmentVersions.SingleAsync(a => a.ObligationId == id && a.Status == AssignmentVersionStatuses.Current);
        currentAssignment.Supersede();
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        before = await TechEvid003SnapshotAsync(db);
        Assert.Equal(first.Id, (await service.AuthorizeAsync(scenario.Direction.UserId, first.Id)).FileId);
        var metadataCalls = storage.MetadataCalls;
        foreach (var actor in new[] { scenario.Sales, scenario.Subcoordination, scenario.Administration })
            await DownloadFailureAsync(() => service.AuthorizeAsync(actor.UserId, first.Id), 404, "ARCHIVO_NO_ENCONTRADO");
        Assert.Equal(metadataCalls, storage.MetadataCalls);
        Assert.Equal(before, await TechEvid003SnapshotAsync(db));
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task TechEvid003DownloadDeniesInvalidActorAndUnlinkedFileBeforeStorage()
    {
        var scenario = await ResetAndSeedAsync();
        await using var db = CreateContext();
        await ObligationConclusionTestData.ConcludeAsync(db, scenario.SalesObligationId, Now);
        var file = await db.FileObjects.FirstAsync(f => f.ObligationId == scenario.SalesObligationId);
        var unlinked = CloneFile(file, linked: false);
        db.FileObjects.Add(unlinked); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var before = await TechEvid003SnapshotAsync(db);
        var storage = new DownloadStorage(db);
        var service = DownloadService(db, storage);
        await DownloadFailureAsync(() => service.AuthorizeAsync(Guid.CreateVersion7(), file.Id), 403, "ACCESO_DENEGADO");
        await DownloadFailureAsync(() => service.AuthorizeAsync(scenario.Direction.UserId, Guid.CreateVersion7()), 404, "ARCHIVO_NO_ENCONTRADO");
        await DownloadFailureAsync(() => service.AuthorizeAsync(scenario.Direction.UserId, unlinked.Id), 404, "ARCHIVO_NO_ENCONTRADO");
        _ = await service.AuthorizeAsync(scenario.AdministrationPeer.UserId, file.Id);
        Assert.Equal(before, await TechEvid003SnapshotAsync(db));
        Assert.Equal(1, storage.MetadataCalls);
        Assert.Equal(1, storage.Signatures);
    }

    [Theory]
    [InlineData("account", 403, "ACCESO_DENEGADO")]
    [InlineData("role", 403, "ACCESO_DENEGADO")]
    [InlineData("employment", 403, "ACCESO_DENEGADO")]
    [InlineData("assignment", 404, "ARCHIVO_NO_ENCONTRADO")]
    public async Task TechEvid003DownloadRechecksActorAfterMetadataBeforeSigning(string change, int status, string code)
    {
        var scenario = await ResetAndSeedAsync();
        await using var db = CreateContext();
        await ObligationConclusionTestData.ConcludeAsync(db, scenario.SalesObligationId, Now);
        var file = await db.FileObjects.AsNoTracking().FirstAsync(f => f.ObligationId == scenario.SalesObligationId);
        string? afterExternalChange = null;
        var storage = new DownloadStorage(db)
        {
            AfterMetadata = async () =>
            {
                await using var other = CreateContext();
                if (change == "account")
                    await other.AppUsers.Where(u => u.Id == scenario.Direction.UserId)
                        .ExecuteUpdateAsync(set => set.SetProperty(u => u.Status, AccountStatus.Inactive));
                else if (change == "role")
                {
                    var role = await other.RoleAssignmentVersions.SingleAsync(r => r.UserId == scenario.Direction.UserId && r.Status == RoleAssignmentStatus.Active);
                    role.Status = RoleAssignmentStatus.Superseded; role.ValidTo = Now; role.RowVersion++;
                    await other.SaveChangesAsync();
                }
                else if (change == "employment")
                {
                    var employment = await other.EmploymentVersions.SingleAsync(e => e.PersonId == scenario.Direction.PersonId && e.ValidTo == null);
                    other.EmploymentVersions.Add(employment.CreateSuccessor(Guid.CreateVersion7(), EmploymentStatus.Inactive, Now));
                    await other.SaveChangesAsync();
                }
                else
                {
                    var assignment = await other.AssignmentVersions.SingleAsync(a => a.ObligationId == scenario.SalesObligationId && a.Status == AssignmentVersionStatuses.Current);
                    assignment.Supersede(); await other.SaveChangesAsync();
                }
                afterExternalChange = await TechEvid003SnapshotAsync(other);
            }
        };
        await DownloadFailureAsync(() => DownloadService(db, storage).AuthorizeAsync(
            change == "assignment" ? scenario.Sales.UserId : scenario.Direction.UserId, file.Id), status, code);
        Assert.Equal(1, storage.MetadataCalls);
        Assert.Equal(0, storage.Signatures);
        Assert.Empty(db.ChangeTracker.Entries());
        Assert.Equal(afterExternalChange, await TechEvid003SnapshotAsync(db));
    }

    [Theory]
    [InlineData("nonreciprocal", 500, "CADENA_EVIDENCIA_INCONSISTENTE")]
    [InlineData("unclean", 422, "ARCHIVO_NO_LIMPIO")]
    [InlineData("missing", 503, "INFRAESTRUCTURA_EVIDENCIA_NO_DISPONIBLE")]
    [InlineData("metadata", 503, "INFRAESTRUCTURA_EVIDENCIA_NO_DISPONIBLE")]
    [InlineData("signer", 503, "INFRAESTRUCTURA_EVIDENCIA_NO_DISPONIBLE")]
    [InlineData("ttl", 503, "INFRAESTRUCTURA_EVIDENCIA_NO_DISPONIBLE")]
    public async Task TechEvid003DownloadFailsClosedWithoutWrites(string defect, int status, string code)
    {
        var scenario = await ResetAndSeedAsync();
        await using var db = CreateContext();
        await ObligationConclusionTestData.ConcludeAsync(db, scenario.SalesObligationId, Now);
        var file = await db.FileObjects.AsNoTracking().FirstAsync(f => f.ObligationId == scenario.SalesObligationId);
        if (defect == "nonreciprocal")
        {
            var otherItem = await db.EvidenceItems.Where(i => i.ObligationId == scenario.SalesObligationId && i.Id != file.LinkedEvidenceItemId)
                .Select(i => i.Id).FirstAsync();
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE file_object DISABLE TRIGGER trg_file_object_guard");
            try
            {
                await db.FileObjects.Where(f => f.Id == file.Id).ExecuteUpdateAsync(set => set.SetProperty(f => f.LinkedEvidenceItemId, (Guid?)otherItem));
            }
            finally { await db.Database.ExecuteSqlRawAsync("ALTER TABLE file_object ENABLE TRIGGER trg_file_object_guard"); }
        }
        if (defect == "unclean")
        {
            // Explicit corrupted fixture: turn off only the file guard to represent an invalid persisted linked state.
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE file_object DROP CONSTRAINT \"CK_file_object_link\"");
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE file_object DISABLE TRIGGER trg_file_object_guard");
            try
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE file_object SET scan_status='PENDIENTE', bucket_class='QUARANTINE', detected_media_type=NULL, scanned_at=NULL, link_expires_at=NULL WHERE id={file.Id}");
            }
            finally { await db.Database.ExecuteSqlRawAsync("ALTER TABLE file_object ENABLE TRIGGER trg_file_object_guard"); }
        }
        var before = await TechEvid003SnapshotAsync(db);
        var storage = new DownloadStorage(db) { Defect = defect };
        await DownloadFailureAsync(() => DownloadService(db, storage, defect == "ttl" ? 301 : 300)
            .AuthorizeAsync(scenario.Direction.UserId, file.Id), status, code);
        Assert.Equal(before, await TechEvid003SnapshotAsync(db));
        Assert.Empty(db.ChangeTracker.Entries());
        if (defect is "nonreciprocal" or "unclean") Assert.Equal(0, storage.MetadataCalls);
        if (defect != "signer") Assert.Equal(0, storage.Signatures);
    }

    private static EfEvidenceDownloadService DownloadService(Sgol.Web.Infrastructure.Persistence.SgolDbContext db,
        DownloadStorage storage, int validity = 300) => new(db, storage, new FixedClock(Now),
            Options.Create(new EvidenceDownloadOptions { ValiditySeconds = validity }));

    private static async Task DownloadFailureAsync(Func<Task<EvidenceDownloadDetails>> operation,
        int status, string code)
    {
        var failure = await Assert.ThrowsAsync<EvidenceDownloadException>(operation);
        Assert.Equal(status, failure.StatusCode);
        Assert.Equal(code, failure.Code);
    }

    private static FileObject CloneFile(FileObject first, bool linked = true)
    {
        var hash = Hash(Guid.CreateVersion7().ToString("D"));
        var second = new FileObject(Guid.CreateVersion7(), first.BranchId, first.ObligationId,
            first.EvidencePolicyVersionId, first.RequirementVersionId, first.RequirementCode, first.RequirementKind,
            first.DocumentSubtype, $"v1/{hash[..2]}/{hash[2..4]}/{hash}", "synthetic.png", first.DeclaredMediaType,
            first.SizeBytes, hash, first.UploadedBy, Now.AddMinutes(-4));
        second.ConfirmUpload(Now.AddMinutes(-3));
        second.MarkClean(first.DetectedMediaType!, "synthetic", Now.AddMinutes(-2));
        if (linked) second.Link(first.LinkedEvidenceItemId!.Value, Now.AddMinutes(-1));
        return second;
    }

    private sealed class DownloadStorage(Sgol.Web.Infrastructure.Persistence.SgolDbContext db) : IPrivateObjectStorage
    {
        public int MetadataCalls { get; private set; }
        public int Signatures { get; private set; }
        public string? Defect { get; init; }
        public Func<Task>? AfterMetadata { get; init; }
        public async Task<EvidenceObjectMetadata> GetMetadataAsync(EvidenceStorageArea area, EvidenceObjectKey key, CancellationToken cancellationToken)
        {
            MetadataCalls++;
            Assert.Equal(EvidenceStorageArea.Clean, area);
            if (Defect == "missing") throw new EvidenceObjectNotFoundException();
            var file = await db.FileObjects.AsNoTracking().SingleAsync(f => f.ObjectKey == key.Value, cancellationToken);
            if (AfterMetadata is not null) await AfterMetadata();
            return new(key, Defect == "metadata" ? file.SizeBytes + 1 : file.SizeBytes, file.Sha256,
                file.DetectedMediaType == "application/pdf" ? EvidenceMediaType.Pdf : EvidenceMediaType.Png);
        }
        public Task<EvidenceDownloadAuthorization> CreateCleanDownloadAuthorizationAsync(EvidenceObjectMetadata metadata, Guid fileId,
            DateTimeOffset expiresAt, CancellationToken cancellationToken)
        {
            Signatures++;
            if (Defect == "signer") throw new EvidenceStorageUnavailableException();
            return Task.FromResult(new EvidenceDownloadAuthorization(new Uri("https://storage.example.test/synthetic-object"), expiresAt));
        }
        public Task<EvidenceUploadAuthorization> CreateQuarantineUploadAuthorizationAsync(EvidenceObjectMetadata metadata, DateTimeOffset expiresAt, CancellationToken cancellationToken) => throw Forbidden();
        public Task PutQuarantineAsync(EvidenceObjectMetadata metadata, Stream content, CancellationToken cancellationToken) => throw Forbidden();
        public Task<Stream> OpenReadAsync(EvidenceStorageArea area, EvidenceObjectKey key, CancellationToken cancellationToken) => throw Forbidden();
        public Task PromoteToCleanAsync(EvidenceObjectMetadata expected, CancellationToken cancellationToken) => throw Forbidden();
        public Task DeleteAsync(EvidenceStorageArea area, EvidenceObjectKey key, CancellationToken cancellationToken) => throw Forbidden();
        public Task CheckAvailabilityAsync(CancellationToken cancellationToken) => throw Forbidden();
        private static InvalidOperationException Forbidden() => new("A pure download cannot invoke this operation.");
    }
}
