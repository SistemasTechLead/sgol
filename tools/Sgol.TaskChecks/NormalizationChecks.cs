using Microsoft.EntityFrameworkCore;
using Npgsql;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Planning;

public static class NormalizationChecks
{
    public static async Task VerifyUpgradeAsync(SgolDbContext db)
    {
        // Dedicated test fixtures ensure the migration exercises photographs and existing JSON-based ownership.
        var planId = await db.DirectionPreparedPlans.Select(p => p.Id).FirstAsync();
        var userId = await db.AppUsers.Select(u => u.Id).FirstAsync();
        var capacity = await db.OperationalCapacities.AsNoTracking().FirstAsync();
        var adjustmentId = Guid.CreateVersion7(); var at = DateTimeOffset.UtcNow;
        var previousCapacity = new OperationalCapacity { Id = Guid.CreateVersion7(), PersonId = capacity.PersonId,
            From = capacity.From, Through = capacity.Through, Percent = 80, DailyMinutes = 100, Reason = "Prior test capacity", Revision = 2 };
        db.PlanCapacityAdjustments.Add(StoredPlanCapacityAdjustment.Capture(adjustmentId, planId, at, capacity, [previousCapacity]));
        db.AuditEvents.Add(new() { Id = adjustmentId, OccurredAt = at, ActorUserId = userId, ActorType = "USER",
            BranchId = Sgol.Organization.Contracts.BranchScope.LorettaId, Action = "PLAN_CAPACITY_ADJUSTED",
            ResourceType = "OPERATIONAL_CAPACITY", ResourceId = capacity.Id, Outcome = "SUCCESS", CorrelationId = Guid.CreateVersion7(),
            BeforeData = System.Text.Json.JsonSerializer.SerializeToDocument(new[] { previousCapacity }),
            AfterData = System.Text.Json.JsonSerializer.SerializeToDocument(new { Id = planId, setting = capacity }) });
        var task = await db.OperationalTasks.FirstAsync();
        var assignmentId = Guid.CreateVersion7(); var sessionId = Guid.CreateVersion7();
        db.OperationalAssignments.Add(new() { Id = assignmentId, PlanId = planId, TaskId = task.Id, TaskCode = task.TaskCode,
            PersonId = capacity.PersonId, PersonName = "Migration fixture", Name = "Migration fixture", Requirement = "FOTOGRAFIA",
            LoadMinutes = 10, StartsAt = at, DueAt = at.AddDays(1), CompletedAt = at });
        db.OperationalCaptureSessions.Add(new() { Id = sessionId, AssignmentId = assignmentId, ActorId = userId, CreatedAt = at, ExpiresAt = at.AddHours(1) });
        db.OperationalPhotographs.Add(new() { Id = Guid.CreateVersion7(), AssignmentId = assignmentId, CaptureSessionId = sessionId,
            FileName = new string('n', 150) + ".jpg", Content = [255], ReceivedBytes = 1, ReceivedSha256 = new('0',64), Sha256 = new('0',64), CreatedAt = at, AttachedAt = at });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var files = await db.OperationalAttachments.AsNoTracking().OrderBy(f => f.AssignmentId).ToArrayAsync();
        var photos = await db.OperationalPhotographs.AsNoTracking().OrderBy(f => f.Id).ToArrayAsync();
        var adjustments = await db.PlanCapacityAdjustments.AsNoTracking().Include(a => a.PreviousSegments).OrderBy(a => a.Id).ToArrayAsync();
        var previous = db.Database.GetMigrations().TakeWhile(m => !m.EndsWith("_NormalizeOperationalStorage")).Last();
        await db.GetService<IMigrator>().MigrateAsync(previous);
        await db.Database.MigrateAsync(); db.ChangeTracker.Clear();
        var migratedFiles = await db.OperationalAttachments.AsNoTracking().OrderBy(f => f.AssignmentId).ToArrayAsync();
        var migratedPhotos = await db.OperationalPhotographs.AsNoTracking().OrderBy(f => f.Id).ToArrayAsync();
        var migratedAdjustments = await db.PlanCapacityAdjustments.AsNoTracking().Include(a => a.PreviousSegments).OrderBy(a => a.Id).ToArrayAsync();
        var fileBefore = files.Select(f => (f.AssignmentId, f.FileName, f.ContentType, Hash(f.Content))).ToArray();
        var fileAfter = migratedFiles.Select(f => (f.AssignmentId, f.FileName, f.ContentType, Hash(f.Content))).ToArray();
        var photoBefore = photos.Select(f => (f.Id, f.FileName, f.AttachedAt, Hash(f.Content))).ToArray();
        var photoAfter = migratedPhotos.Select(f => (f.Id, f.FileName, f.AttachedAt, Hash(f.Content))).ToArray();
        if (!fileBefore.SequenceEqual(fileAfter) || !photoBefore.SequenceEqual(photoAfter)) throw new Exception("Upgrade changed evidence bytes or names.");
        if (!adjustments.Select(Fingerprint).SequenceEqual(migratedAdjustments.Select(Fingerprint))) throw new Exception("Historical capacity ownership was not backfilled exactly.");
        Console.WriteLine($"PASS: Downgrade/upgrade preserves {files.Length} attachments, {photos.Length} photographs and {adjustments.Length} typed capacity adjustments");
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
    private static string Fingerprint(StoredPlanCapacityAdjustment a) => System.Text.Json.JsonSerializer.Serialize(new {
        a.Id, a.PlanId, a.SourceCapacityId, a.PersonId, a.CreatedAt, a.From, a.Through,
        a.Percent, a.DailyMinutes, a.Reason, a.SourceRevision, Previous = a.PreviousSegments.OrderBy(s => s.Position)
    });

    public static async Task RunAsync(SgolDbContext db)
    {
        await RejectAsync(db, """
            UPDATE operational_assignment SET "DueAt" = "StartsAt"
            WHERE "Id" = (SELECT "Id" FROM operational_assignment LIMIT 1)
            """, PostgresErrorCodes.CheckViolation, "Invalid assignment dates rejected by SQL");
        await RejectAsync(db, """
            UPDATE operational_assignment SET "ProgressUnits" = "GoalUnits" + 1
            WHERE "Id" = (SELECT "Id" FROM operational_assignment LIMIT 1)
            """, PostgresErrorCodes.CheckViolation, "Progress cannot exceed goal through SQL");
        await RejectAsync(db, """
            UPDATE operational_review SET "ReviewedBy" = gen_random_uuid(), "ReviewedAt" = now()
            WHERE "AssignmentId" = (SELECT "Id" FROM operational_assignment LIMIT 1)
            """, PostgresErrorCodes.ForeignKeyViolation, "Reviewer must be a real user");
        await RejectAsync(db, """
            UPDATE direction_prepared_plan_item SET task_code = 'NO-SUCH-TASK'
            WHERE (plan_id, task_code) = (SELECT plan_id, task_code FROM direction_prepared_plan_item LIMIT 1)
            """, PostgresErrorCodes.ForeignKeyViolation, "Plan item must reference an operational task");
        await RejectAsync(db, """
            INSERT INTO operational_capacity ("Id", "PersonId", "From", "Through", "Percent", "DailyMinutes", "Reason", "Revision")
            SELECT gen_random_uuid(), "PersonId", "From", "Through", "Percent", "DailyMinutes", "Reason", 1
            FROM operational_capacity LIMIT 1
            """, PostgresErrorCodes.ExclusionViolation, "Overlapping capacity rejected at commit");
        await RejectAsync(db, """
            UPDATE operational_task SET "ScheduleMode" = 'UNSUPPORTED'
            WHERE "Id" = (SELECT "Id" FROM operational_task LIMIT 1)
            """, PostgresErrorCodes.CheckViolation, "Invalid calendar mode rejected by SQL");
        await RejectAsync(db, """
            WITH s AS (
                INSERT INTO operational_capture_session ("Id", "AssignmentId", "ActorId", "CreatedAt", "ExpiresAt", "UploadAttempts")
                SELECT gen_random_uuid(), (SELECT "Id" FROM operational_assignment LIMIT 1),
                    (SELECT id FROM app_user LIMIT 1), now(), now() + interval '1 hour', 0
                RETURNING "Id", "AssignmentId"
            )
            INSERT INTO operational_photograph
                ("Id", "AssignmentId", "CaptureSessionId", "Position", "FileName", "ContentType", "Content", "ReceivedBytes", "ReceivedSha256", "Sha256", "CreatedAt", "AttachedAt")
            SELECT gen_random_uuid(), (SELECT "Id" FROM operational_assignment a WHERE a."Id" <> s."AssignmentId" LIMIT 1),
                s."Id", 0, 'fixture.jpg', 'image/jpeg', decode('ff','hex'), 1, repeat('0',64), repeat('0',64), now(), NULL
            FROM s
            """, PostgresErrorCodes.ForeignKeyViolation, "Photograph must belong to its session assignment");
        await RejectAsync(db, """
            WITH o AS (
                INSERT INTO operational_occasion ("Id", "PlanId", "CreatedBy", "CreatedAt", "Name", "Instructions", "PublicationText", "Channels", "RequestHash")
                SELECT gen_random_uuid(), (SELECT id FROM direction_prepared_plan LIMIT 1),
                    (SELECT id FROM app_user LIMIT 1), now(), 'fixture', 'fixture', '', '', repeat('0',64)
                RETURNING "Id", "PlanId"
            )
            UPDATE operational_assignment a SET "OccasionId" = o."Id"
            FROM o WHERE a."PlanId" <> o."PlanId"
            """, PostgresErrorCodes.ForeignKeyViolation, "Occasion and assignment must share a plan");

        var daily = new DateOnly(2026, 10, 12); var person = Guid.NewGuid(); var second = Guid.NewGuid();
        var workers = new[] { new CapacityWorker(person, "A", "A", "PISO_VENTAS", null, null), new CapacityWorker(second, "B", "B", "PISO_VENTAS", null, null) };
        var tasks = Enumerable.Range(0, 150).Select(i => new OperationalAssignment {
            Id = Guid.NewGuid(), PersonId = i % 2 == 0 ? person : second, StartsAt = OperationalSchedule.Instant(daily.AddDays(-2)),
            DueAt = OperationalSchedule.Instant(daily.AddDays(7)), LoadMinutes = 25, GoalUnits = 20, ProgressUnits = 5
        }).ToArray();
        var ledger = new CapacityLedger(workers, tasks, [], [(person, daily.AddDays(1))], new() { [person] = 30 }, daily);
        AssertEquivalent(ledger, daily);
        ledger.Override = new(person, daily, daily.AddDays(2), 0, 120, "Ausente");
        AssertEquivalent(ledger, daily);
        tasks[0].PersonId = second; ledger.InvalidateLoads();
        AssertEquivalent(ledger, daily);
        Console.WriteLine("PASS: Cached daily loads preserve distribution, absences, legacy compatibility and transfers");
        var watch = System.Diagnostics.Stopwatch.StartNew(); decimal referenceTotal = 0;
        for (var i = 0; i < 20; i++)
            foreach (var worker in workers)
                foreach (var day in CapacityLedger.Days(daily, daily.AddDays(8)))
                    referenceTotal += ledger.Tasks.Where(t => t.PersonId == worker.Id)
                        .Sum(t => ledger.Footprint(t).Where(f => f.Day == day).Sum(f => f.Minutes)) +
                        (day == daily ? ledger.Legacy.GetValueOrDefault(worker.Id) : 0);
        var referenceMs = watch.Elapsed.TotalMilliseconds;
        watch.Restart(); decimal cachedTotal = 0;
        for (var i = 0; i < 20; i++)
            foreach (var worker in workers)
                foreach (var day in CapacityLedger.Days(daily, daily.AddDays(8)))
                    cachedTotal += ledger.DayLoad(worker.Id, day);
        if (referenceTotal != cachedTotal) throw new Exception("Capacity benchmark results differ.");
        Console.WriteLine($"CAPACITY_BENCHMARK tasks=150 repeated_queries=360 reference_ms={referenceMs:F2} cached_ms={watch.Elapsed.TotalMilliseconds:F2}");
    }

    private static void AssertEquivalent(CapacityLedger ledger, DateOnly from)
    {
        foreach (var person in ledger.Workers.Select(w => w.Id))
            foreach (var day in CapacityLedger.Days(from, from.AddDays(8)))
            {
                var reference = ledger.Tasks.Where(t => t.PersonId == person)
                    .Sum(t => ledger.Footprint(t).Where(f => f.Day == day).Sum(f => f.Minutes)) +
                    (day == ledger.Today ? ledger.Legacy.GetValueOrDefault(person) : 0);
                if (ledger.DayLoad(person, day) != reference) throw new Exception("Cached capacity calculation differs from original semantics.");
            }
    }

    private static async Task RejectAsync(SgolDbContext db, string sql, string state, string label)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var atCommit = false;
        try
        {
            var changed = await db.Database.ExecuteSqlRawAsync(sql);
            if (changed == 0) throw new Exception("No fixture for: " + label);
            atCommit = true;
            await tx.CommitAsync();
        }
        catch (PostgresException e) when (e.SqlState == state)
        {
            // PostgreSQL ends the transaction when a deferred constraint rejects COMMIT.
            if (!atCommit) await tx.RollbackAsync();
            Console.WriteLine("PASS: " + label); return;
        }
        throw new Exception("SQL accepted invalid data: " + label);
    }
}
