using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Sgol.BuildingBlocks.Time;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Identity;
using Sgol.Web.Infrastructure.Persistence.Planning;

var connection = Environment.GetEnvironmentVariable("SGOL_SCREEN_CONNECTION")
    ?? throw new InvalidOperationException("Set SGOL_SCREEN_CONNECTION.");
var settings = new NpgsqlConnectionStringBuilder(connection);
if (settings.Host != "localhost" || settings.Port != 5440 ||
    settings.Database != "sgol_local" && !settings.Database!.StartsWith("sgol_screen_checks_", StringComparison.Ordinal))
    throw new InvalidOperationException("Only the local SGOL database or a screen-checks clone is allowed.");
var builder = WebApplication.CreateBuilder([]);
builder.Logging.ClearProviders();
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Sgol"] = connection });
builder.Services.AddSgolPersistence(builder.Configuration);
await using var app = builder.Build();
await using var scope = app.Services.CreateAsyncScope();
var db = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
var screens = scope.ServiceProvider.GetRequiredService<ScreenAccountService>();
var now = scope.ServiceProvider.GetRequiredService<IClock>().UtcNow;

if (args is ["migrate"])
{
    var pending = (await db.Database.GetPendingMigrationsAsync()).ToArray();
    if (pending.Any(x => !x.EndsWith("_AddScreenAccounts", StringComparison.Ordinal) && !x.EndsWith("_AddScreenDeviceLinking", StringComparison.Ordinal)))
        throw new InvalidOperationException("Other pending migrations require their own review.");
    await db.Database.MigrateAsync();
    if (db.Database.HasPendingModelChanges()) throw new Exception("Model and migrations differ.");
    Console.WriteLine("PASS screen-account migration; model matches snapshot.");
    return;
}

if (args is ["inspect"])
{
    var state = await Snapshot();
    Console.WriteLine(JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
    return;
}

var directions = await (from u in db.AppUsers.AsNoTracking()
    join r in db.RoleAssignmentVersions.AsNoTracking() on u.Id equals r.UserId
    where u.Status == AccountStatus.Active && r.BranchId == BranchScope.LorettaId &&
        r.RoleCode == CanonicalRole.Direction && r.Status == RoleAssignmentStatus.Active &&
        r.ValidFrom <= now && (r.ValidTo == null || r.ValidTo > now)
    select u.Id).Distinct().ToArrayAsync();
if (directions.Length != 1) throw new Exception("Expected exactly one active Direction account; select the actor explicitly before proceeding.");
var actor = directions.Single();
if (args is ["http-checks", var testOrigin] && settings.Database != "sgol_local")
{
    var before = await Snapshot();
    await ScreenDeviceChecks.RunAsync(scope.ServiceProvider, actor, new Uri(testOrigin));
    var after = await Snapshot();
    if (JsonSerializer.Serialize(before) != JsonSerializer.Serialize(after)) throw new Exception("Device linking altered operational state.");
    Console.WriteLine("PASS full device lifecycle preserved every operational table fingerprint and capacity ledger.");
    return;
}
if (args is ["pending"])
{
    var requests = await (from request in db.Set<ScreenLinkRequest>().AsNoTracking()
        join account in db.Set<ScreenAccount>().AsNoTracking() on request.ScreenId equals account.Id
        where request.ApprovedAt == null && request.CancelledAt == null && request.ExpiresAt > now
        select new { account.Code, account.DisplayName, request.DeviceName, request.ApprovalCode, request.ExpiresAt }).ToArrayAsync();
    Console.WriteLine(JsonSerializer.Serialize(requests, new JsonSerializerOptions { WriteIndented = true }));
    return;
}
if (args is ["approve", var approval])
{
    var linked = await scope.ServiceProvider.GetRequiredService<ScreenDeviceService>().ApproveAsync(actor, approval);
    Console.WriteLine($"Approved: {linked.Code}; device={linked.DeviceName}; status={linked.Status}");
    return;
}
if (args is ["revoke", var screenCode, var reason])
{
    await scope.ServiceProvider.GetRequiredService<ScreenDeviceService>().RevokeAsync(actor, screenCode, reason);
    Console.WriteLine($"Revoked: {screenCode}; device credentials and browser sessions rejected on their next validation.");
    return;
}
if (args is ["create", var code, var name, var reportPath])
{
    var before = await Snapshot();
    var account = await screens.CreateAsync(actor, code, name,
        "Alta autorizada de pantalla de zapatería; sin persona, relación laboral, rol operativo ni carga.");
    db.ChangeTracker.Clear();
    var after = await Snapshot();
    if (JsonSerializer.Serialize(before) != JsonSerializer.Serialize(after))
        throw new Exception("Operational state changed during provisioning; inspect before reporting success.");
    await VerifyIsolation(account);
    var auditCount = await db.AuditEvents.CountAsync(x => x.ResourceId == account.Id && x.Action == "SCREEN_ACCOUNT_CREATED");
    if (auditCount != 1) throw new Exception("Expected one creation audit, including on retries.");
    var report = JsonSerializer.Serialize(new
    {
        Account = account, Before = before, After = after, CreationAuditCount = auditCount,
        OperationalStateUnchanged = true, Authentication = "Pending device linking; no credentials issued"
    }, new JsonSerializerOptions { WriteIndented = true });
    await File.WriteAllTextAsync(reportPath, report);
    Console.WriteLine($"PASS created/verified {account.Code}: {account.Id}; {account.Status}");
    Console.WriteLine($"PASS workers={after.WorkerCount}; load minutes={after.LoadMinutes}; capacity rows={after.CapacityCount}; assignments={after.AssignmentCount}; unchanged.");
    return;
}

if (args is ["checks"] && settings.Database != "sgol_local")
{
    var before = await Snapshot();
    var account = await screens.CreateAsync(actor, "SCREEN-CHECKS", "Pantalla de prueba", "Integration checks on isolated clone");
    await VerifyIsolation(account);
    var retry = await screens.CreateAsync(actor, "screen-checks", "Pantalla de prueba", "Retry");
    if (retry.Id != account.Id || await db.AuditEvents.CountAsync(x => x.ResourceId == account.Id && x.Action == "SCREEN_ACCOUNT_CREATED") != 1)
        throw new Exception("Duplicate provisioning must be idempotent.");
    await ExpectDenied(() => screens.CreateAsync(account.Id, "SCREEN-DENIED", "Denied", "Must not create accounts"));
    await ExpectDenied(() => scope.ServiceProvider.GetRequiredService<IDirectionPreparedPlanService>().AuthorizeAsync(account.Id));
    await ExpectDenied(() => scope.ServiceProvider.GetRequiredService<OperationalSupervisionService>().AuthorizeAsync(account.Id, default));
    await ExpectDenied(() => scope.ServiceProvider.GetRequiredService<LiveBoardReader>().ReadAsync(account.Id, null));
    var auth = scope.ServiceProvider.GetRequiredService<IHostedAuthenticationService>();
    if (await auth.ValidateSessionAsync(account.Id, "forged-screen-stamp", now, now.AddHours(1)) is not null)
        throw new Exception("A screen was accepted as a normal authenticated user.");
    try
    {
        await auth.LoginAsync(account.Code, "No assigned password 2026!", Guid.CreateVersion7());
        throw new Exception("An unlinked screen authenticated through the password login.");
    }
    catch (AuthenticationFailedException) { }
    var after = await Snapshot();
    if (JsonSerializer.Serialize(before) != JsonSerializer.Serialize(after)) throw new Exception("Provisioning altered operational data.");
    // The DB status constraint prevents provisioning from silently granting an active session.
    await using (var tx = await db.Database.BeginTransactionAsync())
    {
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE screen_account SET status = 'ACTIVA' WHERE id = {account.Id}");
            throw new Exception("Unexpected activation without device linking.");
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.CheckViolation) { }
        await tx.RollbackAsync();
    }
    // Use the device ID as a forged recipient on an existing assignment; FK must reject it.
    var assignment = await db.OperationalAssignments.AsNoTracking().Select(x => x.Id).FirstAsync();
    await using (var tx = await db.Database.BeginTransactionAsync())
    {
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE operational_assignment SET \"PersonId\" = {account.Id} WHERE \"Id\" = {assignment}");
            throw new Exception("Device was accepted as an assignment recipient.");
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation) { }
        await tx.RollbackAsync();
    }
    Console.WriteLine("PASS no person/account/employment/role; zero capacity; absent from workers and ledger.");
    Console.WriteLine("PASS operational tables, eligible workers, load and capacity unchanged; duplicate creates audited once.");
    Console.WriteLine("PASS device cannot create accounts, administer plans, supervise, access the ordinary board or use normal password/session authentication.");
    Console.WriteLine("PASS database rejects activation without linking and forged assignment recipient.");
    return;
}
throw new ArgumentException("Use inspect | migrate | create CODE NAME REPORT_PATH | checks (clone only).");

async Task VerifyIsolation(ScreenAccount account)
{
    if (account.Status != ScreenAccount.PendingLink || account.Purpose != ScreenAccount.BoardReadOnly ||
        await db.People.AnyAsync(x => x.Id == account.Id) ||
        await db.AppUsers.AnyAsync(x => x.Id == account.Id || x.PersonId == account.Id) ||
        await db.EmploymentVersions.AnyAsync(x => x.PersonId == account.Id) ||
        await db.RoleAssignmentVersions.AnyAsync(x => x.UserId == account.Id) ||
        await db.OperationalAssignments.AnyAsync(x => x.PersonId == account.Id))
        throw new Exception("Screen identity is not isolated.");
    var ledger = await OperationalCapacityService.LedgerAsync(db, now, default);
    if (ledger.Workers.Any(x => x.Id == account.Id) || ledger.Tasks.Any(x => x.PersonId == account.Id) ||
        ledger.DayCapacity(account.Id, ledger.Today) != 0)
        throw new Exception("Screen affects planning capacity.");
}

async Task<OperationalState> Snapshot()
{
    var tables = new Dictionary<string, string>();
    await db.Database.OpenConnectionAsync();
    // Fingerprint all operational tables, including historical snapshots, schedules and catalogs.
    // Only technical/audit state and the new device registry are outside this comparison.
    var excluded = new[] { "audit_event", "screen_account", "screen_link_request", "screen_browser_ticket", "__EFMigrationsHistory", "data_protection_key", "outbox_event", "scheduled_job_run" };
    await using (var cmd = db.Database.GetDbConnection().CreateCommand())
    {
        cmd.CommandText = "SELECT tablename FROM pg_tables WHERE schemaname = 'public' ORDER BY tablename";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var table = reader.GetString(0);
            if (!excluded.Contains(table)) tables[table] = "";
        }
    }
    foreach (var table in tables.Keys.ToArray())
    {
        await using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = $"SELECT coalesce(string_agg(row::text, E'\\n' ORDER BY row::text), '') FROM (SELECT to_jsonb(t) AS row FROM \"{table.Replace("\"", "\"\"")}\" t) s";
        tables[table] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes((string)(await cmd.ExecuteScalarAsync())!)));
    }
    var ledger = await OperationalCapacityService.LedgerAsync(db, now, default);
    var ledgerJson = JsonSerializer.Serialize(new
    {
        Workers = ledger.Workers.OrderBy(x => x.Id), ledger.Tasks, ledger.Capacities,
        DailyCapacity = ledger.Workers.OrderBy(x => x.Id).Select(x => new { x.Id, Minutes = ledger.DayCapacity(x.Id, ledger.Today) })
    });
    return new(tables, ledger.Workers.Length, ledger.Tasks.Sum(x => (long)x.LoadMinutes), ledger.Capacities.Length,
        await db.OperationalAssignments.CountAsync(), Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ledgerJson)))) ;
}

static async Task ExpectDenied(Func<Task> action)
{
    try { await action(); }
    catch (DirectionPlanException ex) when (ex.Status == 403) { return; }
    throw new Exception("Device crossed an ordinary account authorization boundary.");
}

sealed record OperationalState(Dictionary<string, string> TableHashes, int WorkerCount, long LoadMinutes,
    int CapacityCount, int AssignmentCount, string LedgerHash);
