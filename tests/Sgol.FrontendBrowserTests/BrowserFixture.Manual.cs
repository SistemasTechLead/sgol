using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.Configuration.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence.Versioning;

namespace Sgol.FrontendBrowserTests;

internal sealed partial class BrowserFixture
{
    public async Task SeedManualCalendarAsync(Guid direction)
    {
        await using var context = new SgolDbContext(new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(connectionString).Options);
        var clock = new BrowserFixedClock(DateTimeOffset.UtcNow);
        var ids = new Uuid7Generator(clock);
        var audit = new AuditTransaction(context);
        var releases = new EfConfigurationReleaseService(context, audit, new VersioningTransaction(context, audit), clock, ids);
        var calendar = new EfCalendarService(context, audit, clock, ids);
        var release = await releases.CreateDraftAsync(new(direction, Guid.CreateVersion7(), Guid.CreateVersion7()));
        var local = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(CalendarContract.TimeZone));
        var first = DateOnly.FromDateTime(local.DateTime).AddDays(-2);
        for (var index = 0; index < 18; index++)
            await calendar.PutAsync(new(direction, Guid.CreateVersion7(), Guid.CreateVersion7(), first.AddDays(index), release.Id,
                index == 4 ? CalendarContract.Holiday : CalendarContract.WorkingDay, index != 4, "Calendario sintético FRONT-013", null));
        await releases.PublishAsync(new(direction, Guid.CreateVersion7(), Guid.CreateVersion7(), release.Id, release.RowVersion,
            clock.UtcNow, "Calendario sintético FRONT-013"));
    }
    // Fault injection in the disposable synthetic database exercises the otherwise sealed UI intention conflict path.
    public async Task<string> SetManualReplayHashAsync(Guid requestId, string replacement)
    {
        await using var db = new SgolDbContext(new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(connectionString).Options);
        var original = await db.IdempotencyRecords.Where(r => r.ResourceId == requestId).Select(r => r.RequestHash).SingleAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE idempotency_record SET request_hash = {replacement} WHERE resource_id = {requestId}");
        return original;
    }
}
