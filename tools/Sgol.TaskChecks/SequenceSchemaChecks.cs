using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Planning;

public static class SequenceSchemaChecks
{
    public static async Task RunAsync(SgolDbContext db)
    {
        if (!await db.People.AnyAsync() || !await db.DirectionPreparedPlans.AnyAsync())
            throw new Exception("Sequence schema checks require a restored local backup in the dedicated test database.");
        var before = await Snapshot(db);
        Check(!db.Database.HasPendingModelChanges(), "EF model matches its migration snapshot");
        var previous = db.Database.GetMigrations().TakeWhile(x => !x.EndsWith("_AddOperationalSequences")).Last();
        await db.GetService<IMigrator>().MigrateAsync(previous);
        Check(before == await Snapshot(db), "Additive schema rollback preserves every existing operational row and evidence byte");
        await db.Database.MigrateAsync();
        Check(before == await Snapshot(db), "Reapplying sequences preserves every existing operational row and evidence byte");
        db.ChangeTracker.Clear();

        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            var actor = await db.AppUsers.Select(x => x.Id).FirstAsync();
            var person = await db.AppUsers.Where(x => x.Id == actor).Select(x => x.PersonId).SingleAsync();
            var plan = await db.DirectionPreparedPlans.Select(x => x.Id).FirstAsync();
            var task = await db.OperationalTasks.FirstAsync();
            var now = new DateTimeOffset(2026, 10, 13, 18, 0, 0, TimeSpan.Zero);
            var root = new OperationalSequence { Id = Guid.CreateVersion7(), Name = "Schema check" };
            var version = new OperationalSequenceVersion { Id = Guid.CreateVersion7(), SequenceId = root.Id, Number = 1, CreatedBy = actor, CreatedAt = now };
            db.Add(root); db.Add(version);
            db.Add(new OperationalSequenceStep { VersionId = version.Id, Position = 1, TaskId = task.Id, ReleaseCondition = "INICIO", DeadlineHours = 6 });
            db.Add(new OperationalSequenceStep { VersionId = version.Id, Position = 2, TaskId = task.Id, ReleaseCondition = "ENTREGA", Action = "REVISAR", DeadlineHours = 1, ReturnToPosition = 1 });
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var left = AddRun("LEFT"); var right = AddRun("RIGHT");
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            // A stage and its not-yet-assigned attempt can be committed together.
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE operational_sequence_stage SET \"CurrentAttemptId\" = {left.Attempt.Id} WHERE \"Id\" = {left.Stage.Id}");
            db.Add(left.Attempt); db.Add(right.Attempt);
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            await db.Database.ExecuteSqlRawAsync("SET CONSTRAINTS ALL IMMEDIATE");
            Check(await db.Set<OperationalSequenceStage>().Where(x => x.Id == left.Stage.Id).Select(x => x.CurrentAttemptId).SingleAsync() == left.Attempt.Id,
                "Deferred stage-attempt cycle permits atomic insertion with an unassigned prepared attempt");
            await db.Database.ExecuteSqlRawAsync("SET CONSTRAINTS ALL DEFERRED");

            await Reject(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE operational_sequence_stage SET \"CurrentAttemptId\" = {right.Attempt.Id} WHERE \"Id\" = {left.Stage.Id}"), "23503", "Current attempt cannot cross executions");
            await Reject(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE operational_sequence_stage SET \"Position\" = 2 WHERE \"Id\" = {left.Stage.Id}"), "23505", "Stage positions cannot repeat in an execution");
            await Reject(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE operational_sequence_stage SET \"EnabledAt\" = {now}, \"EligibleAt\" = {now} WHERE \"Id\" = {left.Stage.Id}"), "23514", "Known enablement cannot have an unknown effective deadline");
            await Reject(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE operational_sequence_stage SET \"EnabledAt\" = {now}, \"EligibleAt\" = {now}, \"EffectiveDueAt\" = {now.AddHours(-1)} WHERE \"Id\" = {left.Stage.Id}"), "23514", "Effective range cannot end before enablement");
            await Reject(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE operational_sequence_stage SET \"Status\" = 'DISPONIBLE' WHERE \"Id\" = {right.Stage.Id}"), "23514", "Stage without current attempt and dates cannot be made available");
            await Reject(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE operational_sequence_attempt SET \"Status\" = 'ACTIVO' WHERE \"Id\" = {left.Attempt.Id}"), "23514", "Active attempt requires a real assignment");
            await Reject(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE operational_sequence_version SET \"StartHour\" = 10 WHERE \"Id\" = {version.Id}"), "23514", "Used calendar version is immutable");
            await Reject(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE operational_sequence_step SET \"DeadlineHours\" = 12 WHERE \"VersionId\" = {version.Id} AND \"Position\" = 1"), "23514", "Used stage rule is immutable");
            await Reject(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE operational_sequence_run SET \"Status\" = 'CANCELADA', \"ClosedAt\" = {now}, \"ClosedBy\" = {actor} WHERE \"Id\" = {left.Run.Id}"), "23514", "Closure requires explicit outcome and nonempty comment");

            var assignmentA = AddAssignment("A"); var assignmentB = AddAssignment("B");
            db.Add(assignmentA); db.Add(assignmentB);
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE operational_sequence_attempt SET \"AssignmentId\" = {assignmentA.Id} WHERE \"Id\" = {left.Attempt.Id}");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE operational_sequence_attempt SET \"AssignmentId\" = {assignmentB.Id} WHERE \"Id\" = {right.Attempt.Id}");
            var secondStage = await db.Set<OperationalSequenceStage>().Where(x => x.RunId == left.Run.Id && x.Position == 2).Select(x => x.Id).SingleAsync();
            var assignmentC = AddAssignment("C"); db.Add(assignmentC); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var secondAttempt = new OperationalSequenceAttempt { Id = Guid.CreateVersion7(), RunId = left.Run.Id, PlanId = plan, StageId = secondStage, Number = 1, Round = 1, AssignmentId = assignmentC.Id, Status = "ACTIVO", CreatedAt = now };
            db.Add(secondAttempt); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE operational_sequence_attempt SET \"Status\" = 'ACTIVO' WHERE \"Id\" = {left.Attempt.Id}");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE operational_sequence_stage SET \"Status\" = 'DISPONIBLE', \"EligibleAt\" = {now}, \"EnabledAt\" = {now}, \"EffectiveDueAt\" = {now.AddHours(6)} WHERE \"Id\" = {left.Stage.Id}");
            await Reject(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE operational_sequence_stage SET \"Status\" = 'DISPONIBLE', \"EligibleAt\" = {now}, \"EnabledAt\" = {now}, \"EffectiveDueAt\" = {now.AddHours(1)}, \"CurrentAttemptId\" = {secondAttempt.Id} WHERE \"Id\" = {secondStage}"), "23P01", "An execution cannot expose two actionable stages simultaneously");
            await Reject(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM operational_assignment WHERE \"Id\" = {assignmentA.Id}"), "23503", "A sequence assignment cannot be deleted while its attempt references it");
            var documentA = Guid.CreateVersion7();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO operational_sequence_document (\"Id\",\"RunId\",\"ProducerAttemptId\",\"AssignmentId\",\"Number\",\"CreatedAt\",\"CreatedBy\",\"Sha256\") VALUES ({documentA},{left.Run.Id},{left.Attempt.Id},{assignmentA.Id},1,{now},{actor},{new string('a',64)})");
            await db.Database.ExecuteSqlRawAsync("SET CONSTRAINTS ALL IMMEDIATE");
            await db.Database.ExecuteSqlRawAsync("SET CONSTRAINTS ALL DEFERRED");
            await Reject(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE operational_sequence_attempt SET \"InputDocumentId\" = {documentA} WHERE \"Id\" = {right.Attempt.Id}"), "23503", "A received document cannot come from another execution");
            await Reject(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE operational_sequence_document SET \"Sha256\" = {new string('f',64)} WHERE \"Id\" = {documentA}"), "23514", "Document versions cannot be silently rewritten");
            await Reject(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE operational_sequence_attempt SET \"AssignmentId\" = {assignmentB.Id} WHERE \"Id\" = {left.Attempt.Id}"), "23514", "Reassignment requires another attempt instead of changing its evidence producer");
            await Reject(() => db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO operational_sequence_document (\"Id\",\"RunId\",\"ProducerAttemptId\",\"AssignmentId\",\"Number\",\"CreatedAt\",\"CreatedBy\",\"Sha256\") VALUES ({Guid.CreateVersion7()},{left.Run.Id},{left.Attempt.Id},{assignmentB.Id},2,{now},{actor},{new string('b',64)})"), "23503", "Document producer must own the evidence assignment");
            await Reject(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE operational_sequence_run SET \"Status\" = 'CONCLUIDA', \"ClosedAt\" = {now}, \"ClosedBy\" = {actor}, \"AuthorizedDocumentId\" = {documentA}, \"ClosureOutcome\" = 'ACEPTADA_CON_DIFERENCIAS' WHERE \"Id\" = {left.Run.Id}"), "23514", "Final acceptance with differences still requires a conclusion");
            await Reject(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM direction_prepared_plan WHERE id = {plan}"), "23503", "Sequence execution restricts deletion of its plan");
            await tx.RollbackAsync(); db.ChangeTracker.Clear();

            (OperationalSequenceRun Run, OperationalSequenceStage Stage, OperationalSequenceAttempt Attempt) AddRun(string label)
            {
                var run = new OperationalSequenceRun { Id = Guid.CreateVersion7(), PlanId = plan, VersionId = version.Id, StartKey = Guid.NewGuid().ToString(), Name = label, CreatedAt = now, CreatedBy = actor, PlannedStartsAt = now };
                var stage = Stage(run, 1); var otherStage = Stage(run, 2);
                var attempt = new OperationalSequenceAttempt { Id = Guid.CreateVersion7(), RunId = run.Id, PlanId = plan, StageId = stage.Id, Number = 1, Round = 1, CreatedAt = now };
                db.Add(run); db.Add(stage); db.Add(otherStage);
                return (run, stage, attempt);
            }
            OperationalSequenceStage Stage(OperationalSequenceRun run, int position) => new() {
                Id = Guid.CreateVersion7(), RunId = run.Id, VersionId = version.Id, Position = position, TaskId = task.Id, TaskRevision = task.Revision,
                TaskCode = task.TaskCode, Name = task.Name, Role = task.AssignedRole, Requirement = task.Requirement, LoadMinutes = task.LoadMinutes,
                PlannedStartsAt = now, PlannedDueAt = now.AddHours(6) };
            OperationalAssignment AddAssignment(string label) => new() {
                Id = Guid.CreateVersion7(), PlanId = plan, TaskId = task.Id, TaskCode = task.TaskCode, TaskRevision = task.Revision,
                Name = label, PersonId = person, PersonName = label, AssignedRole = task.AssignedRole, Requirement = "DOCUMENTO", LoadMinutes = 1,
                StartsAt = now, DueAt = now.AddHours(6), Attachment = new() { Content = [1], ContentType = "application/pdf", FileName = "schema-only.pdf", CreatedAt = now } };
            async Task Reject(Func<Task> action, string code, string label)
            {
                await tx.CreateSavepointAsync("reject_change");
                var rejected = false;
                try { await action(); await db.Database.ExecuteSqlRawAsync("SET CONSTRAINTS ALL IMMEDIATE"); }
                catch (PostgresException error) when (error.SqlState == code || code == "23503" && error.SqlState == "23001") { rejected = true; }
                finally { await tx.RollbackToSavepointAsync("reject_change"); }
                Check(rejected, label);
            }
        }
        Check(before == await Snapshot(db), "Constraint fixtures roll back without changing original data or evidence");
        var guardActor = await db.AppUsers.Select(x => x.Id).FirstAsync();
        var guardPlan = await db.DirectionPreparedPlans.Select(x => x.Id).FirstAsync();
        var guardRoot = new OperationalSequence { Id = Guid.CreateVersion7(), Name = "Downgrade guard fixture" };
        var guardVersion = new OperationalSequenceVersion { Id = Guid.CreateVersion7(), SequenceId = guardRoot.Id, Number = 1, CreatedBy = guardActor, CreatedAt = DateTimeOffset.UtcNow };
        var guardRun = new OperationalSequenceRun { Id = Guid.CreateVersion7(), VersionId = guardVersion.Id, PlanId = guardPlan, StartKey = Guid.NewGuid().ToString(), Name = "Downgrade guard fixture", CreatedBy = guardActor, CreatedAt = DateTimeOffset.UtcNow, PlannedStartsAt = DateTimeOffset.UtcNow };
        db.Add(guardRoot); db.Add(guardVersion); db.Add(guardRun);
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var downgradeBlocked = false;
        try { await db.GetService<IMigrator>().MigrateAsync(previous); }
        catch (PostgresException error) when (error.SqlState == "P0001" && error.MessageText.StartsWith("Sequence executions exist.")) { downgradeBlocked = true; }
        Check(downgradeBlocked && await db.Set<OperationalSequenceRun>().AnyAsync(x => x.Id == guardRun.Id), "Schema rollback refuses to discard an existing sequence execution");
        // Only these three synthetic metadata rows are removed in the dedicated test copy.
        await db.Set<OperationalSequenceRun>().Where(x => x.Id == guardRun.Id).ExecuteDeleteAsync();
        await db.Set<OperationalSequenceVersion>().Where(x => x.Id == guardVersion.Id).ExecuteDeleteAsync();
        await db.Set<OperationalSequence>().Where(x => x.Id == guardRoot.Id).ExecuteDeleteAsync();
        Check(before == await Snapshot(db), "Blocked downgrade leaves historical data and evidence intact");
        Directory.CreateDirectory("outputs/secuencias");
        await File.WriteAllTextAsync("outputs/secuencias/historical-after-checks.json", await Snapshot(db));
        await File.WriteAllTextAsync("outputs/secuencias/migracion.sql", db.GetService<IMigrator>().GenerateScript(previous, db.Database.GetMigrations().Last()));
        Console.WriteLine("Sequence schema migration and integrity checks passed.");
    }

    public static async Task<string> Snapshot(SgolDbContext db)
    {
        var result = new SortedDictionary<string, string>();
        foreach (var table in new[] { "operational_assignment", "operational_submission", "operational_review", "operational_attachment", "operational_photograph", "operational_reference_file", "operational_occasion", "operational_progress_entry", "operational_task", "operational_capacity", "direction_prepared_plan", "direction_prepared_plan_item" })
        {
            // Fixed internal table identifiers. Only aggregate hashes leave PostgreSQL.
            var row = table == "operational_task" ? "(to_jsonb(t) - 'ProcessId')::text" : "to_jsonb(t)::text";
            var sql = "SELECT md5(COALESCE(string_agg(md5(" + row + "), '' ORDER BY md5(" + row + ")), '')) AS \"Value\" FROM " + table + " t";
            result[table] = await db.Database.SqlQueryRaw<string>(sql).SingleAsync();
        }
        return JsonSerializer.Serialize(result);
    }
    private static void Check(bool value, string label)
    {
        if (!value) throw new Exception("FAILED: " + label);
        Console.WriteLine("PASS: " + label);
    }
}
