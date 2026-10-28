using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Planning;

internal static class WeeklyReflowChecks
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    internal static async Task<OperationalAssignment[]> Generate(SgolDbContext db, DirectionPreparedPlan plan, string[] codes, DateTimeOffset now, CapacityLedger ledger)
    {
        var method = typeof(OperationalAgenda).Assembly.GetType("Sgol.Web.Infrastructure.Persistence.Planning.OperationalPlanGenerator")!.GetMethod("GenerateAsync", BindingFlags.Public | BindingFlags.Static)!;
        try { return await (Task<OperationalAssignment[]>)method.Invoke(null, [db, plan, codes, now, CancellationToken.None, true, ledger])!; }
        catch (TargetInvocationException error) { throw error.InnerException!; }
    }
    private static OperationalAssignment Copy(OperationalAssignment row) => JsonSerializer.Deserialize<OperationalAssignment>(JsonSerializer.Serialize(row))!;
    private static CapacityLedger State(CapacityLedger source, IEnumerable<OperationalAssignment> tasks) => new(source.Workers, tasks.ToArray(), source.Capacities, source.Unavailable, source.Legacy, source.Today) { Temporal = source.Temporal };
    internal static void Validate(CapacityLedger source, OperationalAssignment[] rows)
    {
        foreach (var row in rows.Where(t => t.GoalUnits == 0))
        {
            var spans = source.Temporal.Day(row.PersonId, WeekContract.LocalToday(row.StartsAt), false).Intervals ?? [];
            Require(spans.Any(i => row.StartsAt >= TemporalEligibilityContext.At(WeekContract.LocalToday(row.StartsAt), i.Entry) && row.DueAt <= TemporalEligibilityContext.At(WeekContract.LocalToday(row.StartsAt), i.Exit)), "Outside base schedule: " + row.TaskCode);
            Require(row.DueAt == row.StartsAt.AddMinutes(OperationalAgenda.Duration(row.LoadMinutes, row.ClosureMarginPercent!.Value)), "Full margin: " + row.TaskCode);
            Require(rows.All(p => p.Id == row.Id || p.PersonId != row.PersonId || p.GoalUnits > 0 || row.DueAt <= p.StartsAt || p.DueAt <= row.StartsAt), "Overlap: " + row.TaskCode);
            Require(OperationalAgenda.CircuitFits(rows, row, row.StartsAt, row.DueAt), "Cash circuit: " + row.TaskCode);
        }
    }
    public static async Task Run(SgolDbContext db)
    {
        var repository = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (!File.Exists(Path.Combine(repository.FullName, "Directory.Build.props"))) repository = repository.Parent ?? throw new Exception("Repository root not found");
        var directory = Path.Combine(repository.FullName, "outputs/reacomodo-semanal-20261021");
        Directory.CreateDirectory(directory);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
        var all = await db.OperationalAssignments.AsNoTracking().OrderBy(a => a.Id).ToArrayAsync();
        var from = new DateOnly(2026, 10, 22); var through = new DateOnly(2026, 10, 25);
        var start = OperationalSchedule.Instant(from); var end = OperationalSchedule.Instant(through.AddDays(1));
        var original = all.Where(a => a.StartsAt >= start && a.StartsAt < end).ToArray();
        var codes = original.Select(a => a.TaskCode).Distinct().Order(StringComparer.Ordinal).ToArray();
        var definitions = await db.OperationalTasks.Where(t => codes.Contains(t.TaskCode)).ToArrayAsync();
        var beforeCatalog = definitions.Select(t => JsonSerializer.SerializeToElement(t)).ToArray();
        var important = new[] { "TAR-0004", "TAR-0032", "TAR-0222", "TAR-0325", "TAR-0330", "TAR-0344", "TAR-0221", "TAR-0402" };
        foreach (var task in definitions)
        {
            if (important.Contains(task.TaskCode)) task.OperationalCriticality = "IMPORTANTE";
            switch (task.TaskCode)
            {
                case "TAR-0004": task.StartHour = 18; task.StartMinute = 0; task.SundayStartMinute = 772; task.WeekDays |= 1; task.Repetitions = 7; task.Name = "Elaborar el precorte de ventas"; break;
                case "TAR-0032": task.LoadMinutes = 20; task.SundayStartMinute = 802; break;
                case "TAR-0200": task.StartHour = 19; task.StartMinute = 52; task.SundayStartMinute = 832; break;
                case "TAR-0201": task.StartHour = 17; task.StartMinute = 30; break;
                case "TAR-0063": task.Active = false; break;
            }
        }
        await db.SaveChangesAsync();
        var policy = await OperationalSchedulingPolicyService.CurrentAsync(db, default);
        Require(policy.AgendaVersion == 2, "Agenda migration must be applied to the isolated copy");
        var source = await OperationalCapacityService.LedgerAsync(db, start, default);
        var protectedEarlier = source.Tasks.Where(t => t.StartsAt < start && t.GoalUnits == 0 && t.CompletedAt == null).ToArray();
        var retire = original.Where(t => t.TaskCode == "TAR-0063").ToArray();
        var kept = original.Except(retire).Select(Copy).ToList();
        foreach (var row in kept)
        {
            var definition = definitions.Single(t => t.Id == row.TaskId);
            row.LoadMinutes = definition.LoadMinutes; row.TaskRevision = definition.Revision;
            OperationalAgenda.Capture(row, policy, definition.OperationalCriticality, definition.StartOn(WeekContract.LocalToday(row.StartsAt)));
        }
        if (!kept.Any(t => t.TaskCode == "TAR-0004" && WeekContract.LocalToday(t.StartsAt) == through))
        {
            var precorte = Copy(kept.First(t => t.TaskCode == "TAR-0004"));
            var closing = kept.Single(t => t.TaskCode == "TAR-0200" && WeekContract.LocalToday(t.StartsAt) == through);
            precorte.Id = Guid.CreateVersion7(); precorte.PersonId = closing.PersonId; precorte.PersonName = closing.PersonName; precorte.AssignedRole = closing.AssignedRole;
            precorte.ScheduleKey = $"{precorte.TaskId:N}:{through:yyyyMMdd}:0";
            OperationalAgenda.Capture(precorte, policy, "IMPORTANTE", definitions.Single(t => t.TaskCode == "TAR-0004").StartOn(through));
            kept.Add(precorte);
        }
        var removedIds = original.Select(t => t.Id).ToHashSet();
        var baseline = State(source, source.Tasks.Where(t => !removedIds.Contains(t.Id)));
        var outcomes = new List<object>();
        OperationalAssignment[]? repair = null;
        var watch = Stopwatch.StartNew();
        try { repair = OperationalAgendaSearch.SolveFixed(baseline, kept, start, default); Validate(baseline, repair); outcomes.Add(new { Case = "RepairActual", Success = true, Count = repair.Length, Milliseconds = watch.ElapsedMilliseconds }); }
        catch (DirectionPlanException e) { outcomes.Add(new { Case = "RepairActual", Success = false, Error = e.Code, Milliseconds = watch.ElapsedMilliseconds }); }
        // This is a labeled diagnostic only; never deletes or completes earlier commitments.
        var withoutEarlier = State(baseline, baseline.Tasks.Where(t => t.StartsAt >= start || t.GoalUnits > 0 || t.CompletedAt != null));
        watch.Restart();
        try
        {
            var diagnostic = OperationalAgendaSearch.SolveFixed(withoutEarlier, kept, start, default);
            Validate(withoutEarlier, diagnostic);
            await File.WriteAllTextAsync(Path.Combine(directory, "agenda-condicionada.json"), JsonSerializer.Serialize(diagnostic, Json));
            outcomes.Add(new { Case = "RepairIfEarlierResolved", Success = true, Count = diagnostic.Length, Milliseconds = watch.ElapsedMilliseconds });
        }
        catch (DirectionPlanException e) { outcomes.Add(new { Case = "RepairIfEarlierResolved", Success = false, Error = e.Code, Milliseconds = watch.ElapsedMilliseconds }); }
        // Remove future rows only within this rolled-back test transaction, after capturing identity.
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM operational_assignment WHERE \"StartsAt\">={start} AND \"StartsAt\"<{end}");
        var actor = await db.RoleAssignmentVersions.Where(r => r.RoleCode == "DIRECCION" && r.ValidTo == null).Select(r => r.UserId).FirstAsync();
        foreach (var day in new[] { from, new DateOnly(2026, 10, 26) })
        {
            var now = OperationalSchedule.Instant(day);
            var week = day == from ? 43 : 44;
            var input = new DirectionPlanInput("Reflow check", "Isolated", "SEMANAL", 2026, week, codes, IsWeeklyOperationsPlan: true, SchedulingNotBefore: now);
            var plan = new DirectionPreparedPlan(Guid.CreateVersion7(), Sgol.Organization.Contracts.BranchScope.LorettaId, actor, now, input); plan.CaptureSchedulingPolicy(policy.Id);
            var captured = await OperationalCapacityService.LedgerAsync(db, now, default);
            var diagnosticLedger = State(captured, captured.Tasks.Where(t => t.StartsAt >= now || t.GoalUnits > 0 || t.CompletedAt != null));
            watch.Restart();
            try
            {
                var generated = await Generate(db, plan, codes, now, diagnosticLedger);
                Validate(diagnosticLedger, generated);
                Require(generated.All(t => t.TaskCode != "TAR-0063"), "Inactive definition generated");
                await File.WriteAllTextAsync(Path.Combine(directory, $"generacion-semana-{week}.json"), JsonSerializer.Serialize(generated, Json));
                outcomes.Add(new { Case = $"GenerateWeek{week}IfEarlierResolved", Success = true, Count = generated.Length, Milliseconds = watch.ElapsedMilliseconds });
            }
            catch (DirectionPlanException e) { outcomes.Add(new { Case = $"GenerateWeek{week}IfEarlierResolved", Success = false, Error = e.Code, Milliseconds = watch.ElapsedMilliseconds, Detail = e.Data.Cast<System.Collections.DictionaryEntry>().Select(d => new { Key = d.Key.ToString(), d.Value }).ToArray() }); }
        }
        await tx.RollbackAsync(); db.ChangeTracker.Clear();
        Require(JsonSerializer.Serialize(all) == JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().OrderBy(t => t.Id).ToArrayAsync()), "All original rows preserved after isolated transaction");
        await File.WriteAllTextAsync(Path.Combine(directory, "verificacion.json"), JsonSerializer.Serialize(new { CapturedAt = DateTimeOffset.UtcNow, Original = original, ProtectedEarlier = protectedEarlier, Retire = retire.Select(t => t.Id), BeforeCatalog = beforeCatalog, ProposedCatalog = definitions, Outcomes = outcomes, Repair = repair, Preserved = true }, Json));
        Console.WriteLine(JsonSerializer.Serialize(outcomes, Json));
    }
}
