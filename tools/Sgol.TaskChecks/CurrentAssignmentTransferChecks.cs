using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Time;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence.Planning;

// No invented assignments, no rewritten shifts, no reset of operational state.
// All successful handoffs commit only in the runner's guarded restored database.
internal static class CurrentAssignmentTransferChecks
{
    private sealed class TestClock : IClock { public DateTimeOffset UtcNow { get; set; } }
    private sealed record Result(Guid AssignmentId, string Code, string Task, string OriginalPerson, string State,
        string Recipient, string Outcome, string Reason, DateTimeOffset At, DateTimeOffset? Due = null,
        int? Minutes = null, bool Overload = false, int Overlaps = 0);
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }

    public static async Task Run(SgolDbContext db)
    {
        var output = Environment.GetEnvironmentVariable("SGOL_TRANSFER_CHECKS_OUTPUT") ?? "outputs/transferencias-actuales-20261024";
        Directory.CreateDirectory(output);
        var captured = DateTimeOffset.UtcNow;
        // PostgreSQL persists microseconds; use that precision for exact time assertions.
        var instant = new DateTimeOffset(captured.UtcTicks - captured.UtcTicks % 10, TimeSpan.Zero);
        var clock = new TestClock { UtcNow = instant };
        var direction = new EfDirectionPreparedPlanService(db, new AuditTransaction(db), clock, new Uuid7Generator(clock), new DirectionPlanningCatalogReader(db));
        var service = new OperationalSupervisionService(db, clock, direction);
        var tasks = new OperationalTaskService(db, direction, clock, new Sgol.Evidence.Technical.FileTechnicalValidator(), new CheckScanner());
        var actors = await db.RoleAssignmentVersions.Where(r => r.RoleCode == "DIRECCION" && r.ValidTo == null).Select(r => r.UserId).ToArrayAsync();
        var actor = actors[0]; await service.AuthorizeAsync(actor, default);
        var original = await Assignments(db);
        var all = await service.ListAsync(actor, default);
        Require(all.Count == original.Length, "Director's supervision must include all current assignments");
        var tableNames = await Tables(db);
        var pending = original.Where(OperationalSupervisionService.Transferable).ToArray();
        var excluded = original.Where(a => !OperationalSupervisionService.Transferable(a)).ToArray();
        var results = new List<Result>();
        var initialHashes = await Hashes(db, tableNames);
        var perCaseTables = tableNames.Except(new[] { "operational_photograph", "operational_attachment", "operational_reference_file" }).ToArray();
        var initialTransferCount = await db.Set<OperationalTransfer>().CountAsync();
        var initialProcessAttempts = await db.Set<OperationalSequenceAttempt>().AsNoTracking().ToArrayAsync();
        await File.WriteAllTextAsync(Path.Combine(output, "inventory.json"), JsonSerializer.Serialize(new {
            SnapshotUtc = instant, Database = db.Database.GetDbConnection().Database, Total = original.Length,
            Pending = pending.Length, Excluded = excluded.Length, ProcessAssignments = initialProcessAttempts.Count(a => a.AssignmentId != null),
            Assignments = original.Select(a => new { a.Id, a.TaskCode, a.Name, a.PersonName, State = State(a, instant),
                a.Requirement, a.SpecializationName, a.GoalUnits, a.ProgressUnits, a.RemainingMinutes, a.StartsAt, a.DueAt })
        }, Json));
        Console.WriteLine($"INVENTORY {original.Length} real assignments; {pending.Length} pending; {excluded.Length} excluded; {initialProcessAttempts.Count(a => a.AssignmentId != null)} linked to processes.");

        // Every terminal/submitted record must reject before any recipient/schedule exception.
        foreach (var a in excluded)
        {
            var request = new TransferRequest(a.Id, a.Revision, Guid.NewGuid(), 5, instant.AddHours(1), "Prueba aislada de estado excluido");
            try { await service.PreviewTransferAsync(actor, request, default); throw new Exception($"Excluded state accepted: {a.Id}"); }
            catch (DirectionPlanException e) { Require(e.Code == "TRANSFER_STATE", $"Unexpected excluded-state result: {a.Id} {e.Code}"); }
            results.Add(new(a.Id, a.TaskCode, a.Name, a.PersonName, State(a, instant), "", "EXCLUIDA_CORRECTAMENTE", "TRANSFER_STATE", instant));
        }
        Require(Equal(initialHashes, await Hashes(db, tableNames)), "Rejected terminal/submitted tasks changed stored operational state");
        Console.WriteLine($"PASS {excluded.Length} actual submitted/final tasks rejected without writes.");

        foreach (var initial in pending)
        {
            clock.UtcNow = instant; db.ChangeTracker.Clear();
            var recipients = await service.TransferRecipientsAsync(actor, initial.Id, default);
            var qualified = (await OperationalCapacityService.WorkersAsync(db, instant, default)).GroupBy(w => w.Id).Where(g => g.Count() == 1).Select(g => g.Single())
                .Where(w => w.Id != initial.PersonId && w.EligibleFor(initial.AssignedRole, initial.SpecializationId)).OrderBy(w => w.Id).Select(w => w.Id).ToArray();
            Require(qualified.SequenceEqual(recipients.OrderBy(w => w.Id).Select(w => w.Id)), "Recipient selector dropped/added a qualified real worker: " + initial.Id);
            if (recipients.Length == 0)
            {
                var before = await Hashes(db, perCaseTables);
                try { await service.PreviewTransferAsync(actor, new(initial.Id, initial.Revision, Guid.NewGuid(), 5, instant.AddHours(1), "Prueba aislada sin receptor compatible"), default); throw new Exception("Invalid recipient accepted"); }
                catch (DirectionPlanException e) { Require(e.Code == "TRANSFER_RECIPIENT", "Unexpected no-recipient result: " + e.Code); }
                Require(Equal(before, await Hashes(db, perCaseTables)), "No-recipient rejection wrote operational data");
                results.Add(new(initial.Id, initial.TaskCode, initial.Name, initial.PersonName, State(initial, instant), "", "BLOQUEADA_POR_REGLA", "SIN_OTRO_COLABORADOR_CON_ROL_O_ESPECIALIDAD", instant));
                Console.WriteLine($"RULE {initial.TaskCode} / {initial.PersonName}: no other qualified real recipient.");
                continue;
            }
            foreach (var recipient in recipients)
            {
                db.ChangeTracker.Clear(); clock.UtcNow = instant;
                var current = await db.OperationalAssignments.AsNoTracking().SingleAsync(a => a.Id == initial.Id);
                // Each qualified alternative is tested on the same real task, with original progress/evidence.
                // A candidate who is now its owner has already received this task in an earlier matrix entry.
                if (current.PersonId == recipient.Id) continue;
                var pendingMinutes = Math.Max(1, initial.RemainingMinutes);
                var windows = await Windows(db, recipient.Id, clock, instant);
                var reasons = new HashSet<string>();
                TransferPreview? preview = null; TransferRequest? request = null;
                var currentDue = windows.FirstOrDefault(w => w.Start <= instant && instant < w.End).End;
                if (currentDue == default) currentDue = windows.FirstOrDefault().End;
                if (currentDue == default) currentDue = instant.AddHours(1);
                var nowRequest = new TransferRequest(current.Id, current.Revision, recipient.Id, pendingMinutes, currentDue, "Prueba aislada de transferencia de asignación actual");
                var beforeNow = await Hashes(db, perCaseTables);
                try { preview = await service.PreviewTransferAsync(actor, nowRequest, default); request = nowRequest;
                    results.Add(new(current.Id, current.TaskCode, current.Name, initial.PersonName, State(initial, instant), recipient.Name, "VISTA_PREVIA_AHORA", "ADMISIBLE", instant, currentDue, pendingMinutes)); }
                catch (DirectionPlanException e) { Require(AllowedRule(e.Code), "Unexpected current-time failure: " + e.Code); reasons.Add(e.Code);
                    results.Add(new(current.Id, current.TaskCode, current.Name, initial.PersonName, State(initial, instant), recipient.Name, "RECHAZADA_AHORA", e.Code, instant, currentDue, pendingMinutes)); }
                Require(Equal(beforeNow, await Hashes(db, perCaseTables)), "Preview changed operational state");
                if (preview == null)
                {
                    foreach (var window in windows.Where(w => w.End > instant))
                    {
                        clock.UtcNow = window.Start > instant ? window.Start.AddMinutes(1) : instant;
                        if (window.End <= clock.UtcNow) continue;
                        request = nowRequest with { DueAt = window.End };
                        try { preview = await service.PreviewTransferAsync(actor, request, default); break; }
                        catch (DirectionPlanException e) { Require(AllowedRule(e.Code), "Unexpected legal-window failure: " + e.Code); reasons.Add(e.Code); }
                    }
                }
                if (preview == null || request == null)
                {
                    Require(Equal(beforeNow, await Hashes(db, perCaseTables)), "Failed schedule probes changed operational state");
                    results.Add(new(current.Id, current.TaskCode, current.Name, initial.PersonName, State(initial, instant), recipient.Name, "BLOQUEADA_POR_REGLA", string.Join(",", reasons.Order()), instant));
                    Console.WriteLine($"RULE {initial.TaskCode} -> {recipient.Name}: {string.Join(',', reasons)}");
                    continue;
                }
                await ApplyAndVerify(db, service, tasks, actor, request, preview, clock, perCaseTables);
                results.Add(new(current.Id, current.TaskCode, current.Name, initial.PersonName, State(initial, instant), recipient.Name,
                    clock.UtcNow == instant ? "TRANSFERIDA_AHORA" : "TRANSFERIDA_EN_TURNO_VALIDO", "INVARIANTES_OK", clock.UtcNow, request.DueAt,
                    request.RemainingMinutes, preview.LoadAfter > preview.Available, preview.Overlaps.Length));
                Console.WriteLine($"PASS {initial.TaskCode} / {initial.PersonName} -> {recipient.Name}; {initial.ProgressUnits}/{initial.GoalUnits}; {request.RemainingMinutes} min; {clock.UtcNow:O}");
                await File.WriteAllTextAsync(Path.Combine(output, "matrix.json"), JsonSerializer.Serialize(results, Json));
            }
        }
        var successes = results.Where(r => r.Outcome.StartsWith("TRANSFERIDA_")).ToArray();
        var finalHashes = await Hashes(db, tableNames);
        foreach (var table in tableNames.Except(new[] { "operational_assignment", "operational_transfer", "operational_sequence_stage", "operational_sequence_attempt", "operational_sequence_run", "operational_sequence_event" }))
            Require(initialHashes[table] == finalHashes[table], "Full matrix changed protected table/evidence bytes: " + table);
        var blocked = pending.Where(a => !successes.Any(r => r.AssignmentId == a.Id)).ToArray();
        var summary = new { SnapshotUtc = instant, Total = original.Length, Pending = pending.Length, Excluded = excluded.Length,
            TasksTransferred = successes.Select(r => r.AssignmentId).Distinct().Count(), SuccessfulTransfers = successes.Length,
            AtSnapshot = successes.Count(r => r.Outcome == "TRANSFERIDA_AHORA"), AtLaterValidShift = successes.Count(r => r.Outcome == "TRANSFERIDA_EN_TURNO_VALIDO"),
            BlockedTasks = blocked.Select(a => new { a.Id, a.TaskCode, a.Name, a.PersonName, a.SpecializationName,
                Reasons = results.Where(r => r.AssignmentId == a.Id && r.Outcome == "BLOQUEADA_POR_REGLA").Select(r => r.Reason).Distinct().ToArray() }),
            StatesTransferred = successes.GroupBy(r => r.State).ToDictionary(g => g.Key, g => g.Select(r => r.AssignmentId).Distinct().Count()),
            RequirementsTransferred = pending.Where(a => successes.Any(s => s.AssignmentId == a.Id)).GroupBy(a => a.Requirement).ToDictionary(g => g.Key, g => g.Count()),
            InitialProcessAssignments = initialProcessAttempts.Count(a => a.AssignmentId != null),
            ReceiptCount = await db.Set<OperationalTransfer>().CountAsync() - initialTransferCount,
            UnmodifiedSourceConnection = "Runner rejects connections outside localhost/sgol_task_checks_*"
        };
        Require(await db.Set<OperationalTransfer>().CountAsync() - initialTransferCount == successes.Length, "Receipt count differs from committed handoffs");
        await File.WriteAllTextAsync(Path.Combine(output, "matrix.json"), JsonSerializer.Serialize(results, Json));
        await File.WriteAllTextAsync(Path.Combine(output, "summary.json"), JsonSerializer.Serialize(summary, Json));
        Console.WriteLine(JsonSerializer.Serialize(summary, Json));
    }

    private static async Task ApplyAndVerify(SgolDbContext db, OperationalSupervisionService service, OperationalTaskService tasks,
        Guid actor, TransferRequest request, TransferPreview preview, TestClock clock, string[] tables)
    {
        var before = await Assignments(db); var source = before.Single(a => a.Id == request.AssignmentId);
        var hashBefore = await Hashes(db, tables); var receipts = await db.Set<OperationalTransfer>().CountAsync();
        var audits = await db.AuditEvents.CountAsync();
        var stages = await db.Set<OperationalSequenceStage>().AsNoTracking().OrderBy(s => s.Id).ToArrayAsync();
        var attempts = await db.Set<OperationalSequenceAttempt>().AsNoTracking().OrderBy(s => s.Id).ToArrayAsync();
        var runs = await db.Set<OperationalSequenceRun>().AsNoTracking().OrderBy(s => s.Id).ToArrayAsync();
        var activeAttempt = attempts.SingleOrDefault(a => a.AssignmentId == source.Id);
        await service.TransferAsync(actor, request, preview.Fingerprint, default); db.ChangeTracker.Clear();
        var after = await Assignments(db); var moved = after.Single(a => a.Id == source.Id);
        Require(after.Length == before.Length && Equal(before.Where(a => a.Id != source.Id), after.Where(a => a.Id != source.Id)), "Unrelated assignment changed");
        var allowed = new[] { "PersonId", "PersonName", "AssignedRole", "StartsAt", "DueAt", "NominalStartsAt", "TransferRemainingMinutes", "TransferProgressUnits", "PreviousLoadMinutes", "Revision", "TemporalDecisionJson", "RemainingMinutes" };
        var oldState = JsonSerializer.SerializeToNode(source)!.AsObject(); var newState = JsonSerializer.SerializeToNode(moved)!.AsObject();
        foreach (var key in allowed) { oldState.Remove(key); newState.Remove(key); }
        Require(oldState.ToJsonString() == newState.ToJsonString(), "Transfer rewrote progress, evidence contract, delivery or review");
        Require(moved.PersonId == request.RecipientId && moved.Revision == source.Revision + 1 && moved.StartsAt == clock.UtcNow && moved.DueAt == request.DueAt && moved.RemainingMinutes == request.RemainingMinutes, "Incorrect handoff owner/time/load");
        var receipt = await db.Set<OperationalTransfer>().AsNoTracking().SingleAsync(t => t.AssignmentId == source.Id && t.At == clock.UtcNow && t.FromPersonId == source.PersonId && t.ToPersonId == request.RecipientId);
        Require(System.Text.Json.Nodes.JsonNode.DeepEquals(System.Text.Json.Nodes.JsonNode.Parse(receipt.SnapshotJson), JsonSerializer.SerializeToNode(source)) && receipt.PreviousDueAt == source.DueAt && receipt.PreviousStartsAt == source.StartsAt && receipt.ProgressUnits == source.ProgressUnits && receipt.ActorId == actor, "Invalid original-state receipt");
        Require(await db.Set<OperationalTransfer>().CountAsync() == receipts + 1 && await db.AuditEvents.CountAsync() == audits + 1, "Handoff did not create exactly one receipt and audit");
        var tableExceptions = new[] { "operational_assignment", "operational_transfer", "operational_sequence_stage", "operational_sequence_attempt", "operational_sequence_run", "operational_sequence_event" };
        var hashAfter = await Hashes(db, tables);
        foreach (var table in tables.Except(tableExceptions)) Require(hashBefore[table] == hashAfter[table], "Unrelated table changed: " + table);
        if (activeAttempt == null)
            foreach (var table in tableExceptions.Where(t => t.StartsWith("operational_sequence_"))) Require(hashBefore[table] == hashAfter[table], "Independent task changed process state");
        else {
            Require(Equal(stages.Where(s => s.Id != activeAttempt.StageId), (await db.Set<OperationalSequenceStage>().AsNoTracking().OrderBy(s => s.Id).ToArrayAsync()).Where(s => s.Id != activeAttempt.StageId)), "Other process stage changed");
            Require(Equal(attempts.Where(a => a.Id != activeAttempt.Id), (await db.Set<OperationalSequenceAttempt>().AsNoTracking().OrderBy(s => s.Id).ToArrayAsync()).Where(a => a.Id != activeAttempt.Id)), "Other attempt changed");
            Require(Equal(runs.Where(r => r.Id != activeAttempt.RunId), (await db.Set<OperationalSequenceRun>().AsNoTracking().OrderBy(s => s.Id).ToArrayAsync()).Where(r => r.Id != activeAttempt.RunId)), "Other process changed");
        }
        var ledger = await OperationalCapacityService.LedgerAsync(db, clock.UtcNow, default); var day = WeekContract.LocalToday(clock.UtcNow);
        Require(ledger.Load(moved.PersonId, day, day) == preview.LoadAfter, "Committed recipient load differs from preview");
        var fromUser = await db.AppUsers.Where(u => u.PersonId == source.PersonId).Select(u => u.Id).SingleAsync();
        var toUser = await db.AppUsers.Where(u => u.PersonId == moved.PersonId).Select(u => u.Id).SingleAsync();
        Require(!(await tasks.MineAsync(fromUser, default)).Any(a => a.Id == source.Id) && (await tasks.MineAsync(toUser, default)).Any(a => a.Id == source.Id && a.ProgressUnits == source.ProgressUnits), "Mi trabajo ownership/progress incorrect");
        Require((await tasks.MyTransfersAsync(fromUser, default)).Any(t => t.Id == receipt.Id) && (await tasks.MyTransfersAsync(toUser, default)).Any(t => t.Id == receipt.Id), "Handoff missing from a participant's history");
        await tasks.ContributionsAsync(actor, source.Id, default);
        var beforeRepeat = await Hashes(db, tables);
        try { await service.TransferAsync(actor, request, preview.Fingerprint, default); throw new Exception("Duplicate handoff accepted"); }
        catch (DirectionPlanException e) { Require(e.Code == "TRANSFER_STALE", "Duplicate should fail stale revision"); }
        Require(Equal(beforeRepeat, await Hashes(db, tables)) && await db.AuditEvents.CountAsync() == audits + 1, "Duplicate attempt changed data");
    }

    private static string State(OperationalAssignment a, DateTimeOffset now) => !OperationalSupervisionService.Transferable(a)
        ? a.CompletedAt != null && a.ReviewStatus is null or "PENDIENTE_REVISION" ? "PENDIENTE_REVISION" : a.ReviewStatus ?? "ENTREGADA"
        : a.StartsAt > now ? "PROGRAMADA" : a.DueAt < now ? a.ProgressUnits > 0 ? "VENCIDA_CON_AVANCE" : "VENCIDA" : a.ProgressUnits > 0 ? "ACTIVA_CON_AVANCE" : "ACTIVA";
    private static bool AllowedRule(string code) => code is "SCHEDULE_NOT_PRESENT_AT_ACTIVATION" or "SCHEDULE_NOT_PRESENT_AT_RECEPTION" or "SCHEDULE_ABSENT" or "SCHEDULE_MISSING" or "CLOSING_COVERAGE_REQUIRED" or "TRANSFER_DEADLINE" or "TRANSFER_PROCESS_STATE" or "SCHEDULE_EMPLOYMENT_END" or "TRANSFER_RECIPIENT";
    private static bool Equal<T>(T left, T right) => JsonSerializer.Serialize(left) == JsonSerializer.Serialize(right);
    private static Task<OperationalAssignment[]> Assignments(SgolDbContext db) => db.OperationalAssignments.AsNoTracking().OrderBy(a => a.Id).ToArrayAsync();
    private static async Task<(DateTimeOffset Start, DateTimeOffset End)[]> Windows(SgolDbContext db, Guid person, TestClock clock, DateTimeOffset now)
    {
        var ledger = await OperationalCapacityService.LedgerAsync(db, now, default); var first = WeekContract.LocalToday(now);
        return CapacityLedger.Days(first, first.AddDays(7)).Where(d => !ledger.Unavailable.Contains((person, d)))
            .SelectMany(d => (ledger.Temporal.Day(person, d, false).Intervals ?? []).Select(i => (Start: TemporalEligibilityContext.At(d, i.Entry), End: TemporalEligibilityContext.At(d, i.Exit))))
            .Where(w => w.End > now).OrderBy(w => w.Start).ToArray();
    }
    private static async Task<string[]> Tables(SgolDbContext db)
    {
        await db.Database.OpenConnectionAsync();
        await using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = "SELECT tablename FROM pg_tables WHERE schemaname='public' AND (tablename LIKE 'operational_%' OR tablename LIKE 'personal_schedule_%' OR tablename LIKE 'closing_%' OR tablename LIKE 'direction_prepared_%' OR tablename='availability_day_version') ORDER BY tablename";
        await using var reader = await cmd.ExecuteReaderAsync(); var tables = new List<string>();
        while (await reader.ReadAsync()) tables.Add(reader.GetString(0));
        return tables.ToArray();
    }
    private static async Task<Dictionary<string, string>> Hashes(SgolDbContext db, string[] tables)
    {
        await using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = string.Join(" UNION ALL ", tables.Select(t => $"SELECT '{t}',count(*)::text || ':' || md5(coalesce(string_agg(to_jsonb(t)::text,'' ORDER BY to_jsonb(t)::text),'')) FROM \"{t}\" t"));
        await using var reader = await cmd.ExecuteReaderAsync(); var result = new Dictionary<string, string>();
        while (await reader.ReadAsync()) result[reader.GetString(0)] = reader.GetString(1);
        return result;
    }
}
