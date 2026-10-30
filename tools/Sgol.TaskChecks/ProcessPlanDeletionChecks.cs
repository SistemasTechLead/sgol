using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Planning;

public static class ProcessPlanDeletionChecks
{
    public static async Task PreparedAsync(SgolDbContext db, IDirectionPreparedPlanService direction, ProcessPlanningService planning,
        Guid[] users, ProcessPlanInput original)
    {
        var input = new ProcessPlanInput { Key = Guid.CreateVersion7(), Name = "Prepared deletion fixture", ProcessId = original.ProcessId,
            Mode="OCASION",OccasionTime=new(11,0), From = original.From.AddDays(10), Through = original.From.AddDays(10), People = original.People };
        input.Fingerprint = (await planning.PreviewAsync(users[3], input, default)).Fingerprint;
        var id = await planning.PrepareAsync(users[3], input, default); db.ChangeTracker.Clear();
        var preview = await direction.PreviewDeletionAsync(users[3], id);
        await Reject(() => direction.PreviewDeletionAsync(users[0], id), "ACCESO_DENEGADO");
        await Reject(() => direction.DownloadArchiveAsync(users[2], id, preview.Fingerprint), "ACCESO_DENEGADO");
        var archive = await direction.DownloadArchiveAsync(users[3], id, preview.Fingerprint);
        await using (archive.Content)
        using (var zip = new ZipArchive(archive.Content, ZipArchiveMode.Read))
            Check(zip.Entries.Count == 1 && zip.GetEntry("plan-y-archivos.json") != null, "Empty prepared plan exports its index without fabricated files");
        await ProcessPlanDeletionHttpChecks.RunAsync(db, users[3], id);
        Check(!await db.Set<PreparedProcessStart>().AnyAsync(s => s.PlanId == id) && await db.DirectionPreparedPlans.AnyAsync(p => p.Id == original.Key),
            "Prepared deletion releases only its own reservations and preserves another plan");
        input.Key = Guid.CreateVersion7();
        input.Fingerprint = (await planning.PreviewAsync(users[3], input, default)).Fingerprint;
        var skipId = await planning.PrepareAsync(users[3], input, default); db.ChangeTracker.Clear();
        await ProcessPlanDeletionHttpChecks.RunAsync(db, users[3], skipId, skipDownload: true);
    }

    public static async Task ActiveAsync(SgolDbContext db, IDirectionPreparedPlanService direction, Guid[] users, Guid runId)
    {
        var id = await db.Set<OperationalSequenceRun>().Where(r => r.Id == runId).Select(r => r.PlanId).SingleAsync();
        var preview = await direction.PreviewDeletionAsync(users[3], id);
        var attempt = await db.Set<OperationalSequenceAttempt>().SingleAsync(a => a.RunId == runId && a.Status == "ACTIVO");
        attempt.Comment = "Changed since preview"; attempt.Revision++;
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        await Reject(() => direction.DeleteAsync(users[3], id, Guid.CreateVersion7(), Guid.CreateVersion7(), preview.Fingerprint), "PLAN_DELETE_STALE");
        await Reject(() => direction.DownloadArchiveAsync(users[3], id, preview.Fingerprint), "PLAN_DELETE_STALE");
        preview = await direction.PreviewDeletionAsync(users[3], id);
        var before = await Graph(db, id);
        var previousMigration = db.Database.GetMigrations().Single(m => m.EndsWith("_AddPreparedProcessPlans"));
        await db.GetService<IMigrator>().MigrateAsync(previousMigration);
        Check(before == await Graph(db, id), "Deletion rule migration downgrades without altering history");
        await db.Database.MigrateAsync();
        Check(before == await Graph(db, id) && !db.Database.HasPendingModelChanges(), "Deletion rule migration reapplies preserving history and EF model");
        await RejectHistoryWrite(db, id, runId, false);
        await RejectHistoryWrite(db, id, runId, true);
        Check(before == await Graph(db, id), "Individual history edits/deletions remain blocked, including partial deletes with a scoped flag");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE audit_event ADD CONSTRAINT test_process_delete_audit_failure CHECK (action <> 'DIRECTION_PLAN_DELETE') NOT VALID");
        try
        {
            try { await direction.DeleteAsync(users[3], id, Guid.CreateVersion7(), Guid.CreateVersion7(), preview.Fingerprint); throw new Exception("Expected audit failure"); }
            catch (DbUpdateException e) when (e.InnerException is PostgresException { ConstraintName: "test_process_delete_audit_failure" }) { }
            Check(before == await Graph(db, id), "Audit failure restores active run, stages, documents, events, assignments and original references");
        }
        finally { await db.Database.ExecuteSqlRawAsync("ALTER TABLE audit_event DROP CONSTRAINT test_process_delete_audit_failure"); }
    }

    private static async Task RejectHistoryWrite(SgolDbContext db, Guid id, Guid runId, bool scoped)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            if (scoped)
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT set_config('sgol.plan_delete_id', {id.ToString("D")}, true), set_config('sgol.plan_delete_key', {Guid.CreateVersion7().ToString("D")}, true)");
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE operational_sequence_stage SET \"DependencyEventId\" = NULL, \"DependencySatisfiedAt\" = NULL WHERE \"RunId\" = {runId}");
                await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM operational_sequence_event WHERE \"RunId\" = {runId}");
                await tx.CommitAsync();
            }
            else await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE operational_sequence_document SET \"Sha256\" = repeat('a',64) WHERE \"RunId\" = {runId}");
            throw new Exception("Expected history guard rejection");
        }
        catch (PostgresException e) when (e.SqlState == "23514") { if (!scoped) await tx.RollbackAsync(); }
    }

    public static async Task CompletedAsync(SgolDbContext db, IDirectionPreparedPlanService direction, Guid[] users, Guid runId)
    {
        var id = await db.Set<OperationalSequenceRun>().Where(r => r.Id == runId).Select(r => r.PlanId).SingleAsync();
        var assignment = await db.OperationalAssignments.Where(a => a.PlanId == id && a.Attachment != null).Select(a => a.Id).FirstAsync();
        // Both a document and a photograph on one assignment must be exported, with
        // distinct paths even when the original names collide or contain traversal.
        var photoBytes = new byte[] { 1, 2, 3, 4 };
        db.OperationalPhotographs.Add(new() { Id = Guid.CreateVersion7(), AssignmentId = assignment, FileName = "../../v1.pdf",
            Content = photoBytes, ReceivedBytes = photoBytes.Length, ReceivedSha256 = Convert.ToHexStringLower(SHA256.HashData(photoBytes)),
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(photoBytes)), CreatedAt = DateTimeOffset.UtcNow, AttachedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var preview = await direction.PreviewDeletionAsync(users[3], id);
        var expected = await db.Set<OperationalAttachment>().Where(f => db.OperationalAssignments.Any(a => a.PlanId == id && a.Id == f.AssignmentId)).Select(f => f.Content).ToListAsync();
        expected.Add(photoBytes);
        var archive = await direction.DownloadArchiveAsync(users[3], id, preview.Fingerprint);
        await using (archive.Content)
        using (var zip = new ZipArchive(archive.Content, ZipArchiveMode.Read))
        {
            Check(zip.Entries.Count == expected.Count + 1 && preview.EvidenceFiles == expected.Count, "ZIP includes both previous document versions and every photograph exactly once");
            var actual = new List<string>();
            foreach (var entry in zip.Entries.Where(e => e.FullName != "plan-y-archivos.json"))
            {
                Check(!entry.FullName.Split('/').Any(segment => segment is ".." or "."), "ZIP names cannot escape their folders");
                using var content = entry.Open(); using var bytes = new MemoryStream(); await content.CopyToAsync(bytes);
                actual.Add(Convert.ToHexString(SHA256.HashData(bytes.ToArray())));
            }
            Check(actual.Order().SequenceEqual(expected.Select(b => Convert.ToHexString(SHA256.HashData(b))).Order()), "Every archived file preserves its original bytes");
            using var metadata = zip.GetEntry("plan-y-archivos.json")!.Open();
            using var manifest = await JsonDocument.ParseAsync(metadata);
            Check(manifest.RootElement.GetProperty("documents").GetArrayLength() == 2 && manifest.RootElement.GetProperty("attempts").GetArrayLength() >= 6,
                "Archive index preserves document versions and earlier rounds");
        }
        var catalogCount = await db.OperationalTasks.CountAsync();
        var audits = await db.AuditEvents.CountAsync();
        var key = Guid.CreateVersion7();
        await direction.DeleteAsync(users[3], id, key, Guid.CreateVersion7(), preview.Fingerprint);
        Check(!await db.DirectionPreparedPlans.AnyAsync(p => p.Id == id) && !await db.Set<OperationalSequenceRun>().AnyAsync(r => r.PlanId == id)
            && !await db.Set<OperationalSequenceStage>().AnyAsync(s => s.RunId == runId) && !await db.Set<OperationalSequenceAttempt>().AnyAsync(a => a.RunId == runId)
            && !await db.Set<OperationalSequenceDocument>().AnyAsync(d => d.RunId == runId) && !await db.Set<OperationalSequenceEvent>().AnyAsync(e => e.RunId == runId)
            && !await db.OperationalAssignments.AnyAsync(a => a.PlanId == id) && !await db.Set<OperationalAttachment>().AnyAsync()
            && !await db.OperationalPhotographs.AnyAsync() && !await db.Set<PreparedProcessStart>().AnyAsync(), "Completed process deletion removes all execution and evidence without foreign key failures");
        Check(await db.OperationalTasks.CountAsync() == catalogCount && await db.AuditEvents.CountAsync() > audits,
            "Process catalogue and historical audit survive deletion");
        Check((await direction.DeleteAsync(users[3], id, key, Guid.CreateVersion7(), preview.Fingerprint)).Result == "DELETED", "Deletion retry confirms the same result");
    }

    private static async Task<string> Graph(SgolDbContext db, Guid id)
    {
        var runs = await db.Set<OperationalSequenceRun>().AsNoTracking().Where(r => r.PlanId == id).OrderBy(r => r.Id).ToArrayAsync();
        var ids = runs.Select(r => r.Id).ToArray();
        return JsonSerializer.Serialize(new { runs,
            stages = await db.Set<OperationalSequenceStage>().AsNoTracking().Where(s => ids.Contains(s.RunId)).OrderBy(s => s.Id).ToArrayAsync(),
            attempts = await db.Set<OperationalSequenceAttempt>().AsNoTracking().Where(a => a.PlanId == id).OrderBy(a => a.Id).ToArrayAsync(),
            documents = await db.Set<OperationalSequenceDocument>().AsNoTracking().Where(d => ids.Contains(d.RunId)).OrderBy(d => d.Id).ToArrayAsync(),
            events = await db.Set<OperationalSequenceEvent>().AsNoTracking().Where(e => ids.Contains(e.RunId)).OrderBy(e => e.Id).ToArrayAsync(),
            assignments = await db.OperationalAssignments.AsNoTracking().Where(a => a.PlanId == id).OrderBy(a => a.Id).ToArrayAsync() });
    }
    private static void Check(bool value, string text) { if (!value) throw new Exception("FAILED: " + text); Console.WriteLine("PASS: " + text); }
    private static async Task Reject(Func<Task> action, string code) { try { await action(); throw new Exception("Expected " + code); } catch (DirectionPlanException e) when (e.Code == code) { Console.WriteLine("PASS: Rejected " + code); } }
}
