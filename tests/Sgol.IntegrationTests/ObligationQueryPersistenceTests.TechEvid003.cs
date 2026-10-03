using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Sgol.Assignment.Contracts;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.Evidence.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Evidence;
using Sgol.Web.Presentation.Endpoints;
using Xunit;

namespace Sgol.IntegrationTests;

public sealed partial class ObligationQueryPersistenceTests
{
    [Fact]
    public async Task TechEvid003DirectionMustReadPersistedEvidenceAfterCurrentAssignmentEndsWithoutEffects()
    {
        var scenario = await ResetAndSeedAsync();
        await using var db = CreateContext();
        var id = scenario.SalesObligationId;
        // Create valid history while assigned, then retain it with no current assignment.
        await ObligationConclusionTestData.ConcludeAsync(db, id, Now);
        Assert.NotEmpty(await db.EvidenceVersions.AsNoTracking()
            .Where(v => db.EvidenceItems.Any(i => i.Id == v.EvidenceItemId && i.ObligationId == id)).ToListAsync());
        Assert.True(await db.FileObjects.AsNoTracking().AnyAsync(f => f.ObligationId == id &&
            f.LinkedEvidenceItemId != null && f.ScanStatus == EvidenceFileStatuses.Clean));
        var storage = new TechEvid003ForbiddenStorage();
        var service = new EfEvidenceContributionService(db, new AuditTransaction(db), storage,
            null!, null!, new FixedClock(Now), new Uuid7Generator(new FixedClock(Now)));

        var beforeAssigned = await TechEvid003SnapshotAsync(db);
        foreach (var actor in new[] { scenario.Sales, scenario.Subcoordination, scenario.Administration, scenario.Direction })
        {
            var result = await TechEvid003ListAsync(service, actor.UserId, id);
            Assert.Equal(200, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
            var body = System.Text.Json.JsonSerializer.Serialize(Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
            Assert.DoesNotContain("ObjectKey", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("download", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("bucket", body, StringComparison.OrdinalIgnoreCase);
        }
        TechEvid003AssertProblem(await TechEvid003ListAsync(service, scenario.Sales.UserId,
            scenario.DirectionObligationId), 404, "OBLIGACION_NO_ENCONTRADA");
        TechEvid003AssertProblem(await TechEvid003ListAsync(service, scenario.Administration.UserId,
            scenario.AdministrationPeerObligationId), 404, "OBLIGACION_NO_ENCONTRADA");
        Assert.Equal(beforeAssigned, await TechEvid003SnapshotAsync(db));

        await db.AppUsers.Where(u => u.Id == scenario.Sales.UserId)
            .ExecuteUpdateAsync(set => set.SetProperty(u => u.Status, Sgol.Identity.Contracts.AccountStatus.Inactive));
        var beforeInactiveResponsible = await TechEvid003SnapshotAsync(db);
        Assert.Equal(200, Assert.IsAssignableFrom<IStatusCodeHttpResult>(
            await TechEvid003ListAsync(service, scenario.Direction.UserId, id)).StatusCode);
        TechEvid003AssertProblem(await TechEvid003ListAsync(service, scenario.Administration.UserId, id),
            404, "OBLIGACION_NO_ENCONTRADA");
        Assert.Equal(beforeInactiveResponsible, await TechEvid003SnapshotAsync(db));
        await db.AppUsers.Where(u => u.Id == scenario.Sales.UserId)
            .ExecuteUpdateAsync(set => set.SetProperty(u => u.Status, Sgol.Identity.Contracts.AccountStatus.Active));

        var assignment = await db.AssignmentVersions.SingleAsync(a => a.ObligationId == id &&
            a.Status == AssignmentVersionStatuses.Current);
        assignment.Supersede();
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        Assert.False(await db.AssignmentVersions.AnyAsync(a => a.ObligationId == id &&
            a.Status == AssignmentVersionStatuses.Current));
        var before = await TechEvid003SnapshotAsync(db);

        // HU-023 reference confirms the same obligation is visible to Direction.
        var detail = await Reader(db).GetAsync(new(scenario.Direction.UserId, id, null, 25));
        Assert.Equal(id, detail.Detail.ObligationId);
        foreach (var actor in new[] { scenario.Sales, scenario.Subcoordination, scenario.Administration })
            TechEvid003AssertProblem(await TechEvid003ListAsync(service, actor.UserId, id),
                404, "OBLIGACION_NO_ENCONTRADA");
        TechEvid003AssertProblem(await TechEvid003ListAsync(service, Guid.CreateVersion7(), id),
            403, "ACCESO_DENEGADO");
        TechEvid003AssertProblem(await TechEvid003ListAsync(service, scenario.Direction.UserId, Guid.CreateVersion7()),
            404, "OBLIGACION_NO_ENCONTRADA");

        var direction = await TechEvid003ListAsync(service, scenario.Direction.UserId, id);
        var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(direction).StatusCode;
        Assert.Equal(before, await TechEvid003SnapshotAsync(db));
        Assert.Empty(db.ChangeTracker.Entries());
        Assert.Equal(0, storage.Calls);
        // Keep the approved oracle: a 404 confirms the defect, never a passing substitute.
        Assert.True(status == 200,
            $"Direction metadata expected 200, actual {status}; HU-023 detail 200; assigned four-role controls, " +
            "unassigned non-Direction negatives, invalid actor, missing resource, no-effect and zero S3 verified.");
    }

    private static async Task<string> TechEvid003SnapshotAsync(Sgol.Web.Infrastructure.Persistence.SgolDbContext db)
    {
        var rows = await Front016NoEffectSnapshot.ReadAsync(db);
        foreach (var table in new[] { "file_object", "app_user", "employment_version", "role_assignment_version" })
        {
            // Identifiers come exclusively from the fixed table allowlist above, never request input.
#pragma warning disable EF1003
            rows += "\n" + table + ":" + await db.Database.SqlQueryRaw<string>(
                "SELECT COALESCE(jsonb_agg(to_jsonb(t) ORDER BY to_jsonb(t)::text)::text, '[]') AS \"Value\" FROM " + table + " t").SingleAsync();
#pragma warning restore EF1003
        }
        // Compare complete rows without printing credentials, object metadata or audit payloads on assertion failure.
        return Hash(rows);
    }

    private static Task<IResult> TechEvid003ListAsync(IEvidenceContributionService service, Guid actor, Guid id)
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, actor.ToString("D"))], "test"));
        context.Request.Scheme = "https";
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = $"/api/v1/obligations/{id:D}/evidence";
        return EvidenceApiEndpoints.ListAsync(context, id, service, CancellationToken.None);
    }

    private static void TechEvid003AssertProblem(IResult result, int status, string code)
    {
        Assert.Equal(status, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        var problem = Assert.IsType<Microsoft.AspNetCore.Mvc.ProblemDetails>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal(code, problem.Extensions["code"]);
        Assert.True(problem.Extensions.ContainsKey("correlationId"));
    }

    private sealed class TechEvid003ForbiddenStorage : IPrivateObjectStorage
    {
        public int Calls { get; private set; }
        public Task<EvidenceDownloadAuthorization> CreateCleanDownloadAuthorizationAsync(EvidenceObjectMetadata metadata,
            Guid fileId, DateTimeOffset expiresAt, CancellationToken cancellationToken) => throw Forbidden();
        private InvalidOperationException Forbidden() { Calls++; return new InvalidOperationException("Metadata must not use S3."); }
        public Task<EvidenceUploadAuthorization> CreateQuarantineUploadAuthorizationAsync(EvidenceObjectMetadata metadata,
            DateTimeOffset expiresAt, CancellationToken cancellationToken) => throw Forbidden();
        public Task PutQuarantineAsync(EvidenceObjectMetadata metadata, Stream content, CancellationToken cancellationToken) => throw Forbidden();
        public Task<EvidenceObjectMetadata> GetMetadataAsync(EvidenceStorageArea area, EvidenceObjectKey key, CancellationToken cancellationToken) => throw Forbidden();
        public Task<Stream> OpenReadAsync(EvidenceStorageArea area, EvidenceObjectKey key, CancellationToken cancellationToken) => throw Forbidden();
        public Task PromoteToCleanAsync(EvidenceObjectMetadata expected, CancellationToken cancellationToken) => throw Forbidden();
        public Task DeleteAsync(EvidenceStorageArea area, EvidenceObjectKey key, CancellationToken cancellationToken) => throw Forbidden();
        public Task CheckAvailabilityAsync(CancellationToken cancellationToken) => throw Forbidden();
    }
}
