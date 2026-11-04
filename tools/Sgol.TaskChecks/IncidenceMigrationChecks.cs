using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Planning;

internal static partial class IncidenceChecks
{
    static async Task DowngradeGuard(SgolDbContext db)
    {
        var held=await db.Set<PreparedProcessStart>().CountAsync(s=>s.IncidenceHoldId!=null);
        var receipts=await db.Set<OperationalIncidence>().CountAsync();
        var rejected=false;
        try { await db.GetService<IMigrator>().MigrateAsync("20261104010000_AddMonthlyCarryoverWindow"); }
        catch(Npgsql.PostgresException e) when(e.SqlState=="P0001") { rejected=true; }
        Check(rejected && receipts==await db.Set<OperationalIncidence>().CountAsync() && held==await db.Set<PreparedProcessStart>().CountAsync(s=>s.IncidenceHoldId!=null),
            "downgrade with incidence history rejected before losing receipts or prepared holds");
    }
    internal static async Task MigrationChecks(SgolDbContext db)
    {
        if ((await db.Database.GetAppliedMigrationsAsync()).Any()) throw new Exception("Migration preservation check requires a new isolated database; never downgrade existing data.");
        await db.GetService<IMigrator>().MigrateAsync("20261104010000_AddMonthlyCarryoverWindow");
        var clock = new Clock(); var f = await QaFixtures.CreateAsync(db);
        await Row(db,f,clock,clock.UtcNow.AddHours(1));
        var retired = await Row(db,f,clock,clock.UtcNow.AddHours(-1));
        var tracked = await db.OperationalAssignments.SingleAsync(a=>a.Id==retired.Id);
        tracked.RetiredAt=clock.UtcNow;tracked.Revision++;await db.SaveChangesAsync();db.ChangeTracker.Clear();
        async Task<string> Snapshot() => JsonSerializer.Serialize(new {
            Assignments=await db.OperationalAssignments.IgnoreQueryFilters().AsNoTracking().OrderBy(a=>a.Id).ToArrayAsync(),
            Facts=await db.Set<TaskBonusFact>().AsNoTracking().OrderBy(f=>f.Id).ToArrayAsync(),
            Availability=await db.AvailabilityDayVersions.AsNoTracking().OrderBy(a=>a.Id).ToArrayAsync()
        });
        var before=await Snapshot();
        await db.Database.MigrateAsync();
        Check(before==await Snapshot(),"upgrade from previous schema preserves active/retired assignments, bonus facts and availability exactly");
        await db.Database.MigrateAsync();
        Check(before==await Snapshot() && !db.Database.HasPendingModelChanges(),"repeated migration is unchanged and model matches");
    }
}
