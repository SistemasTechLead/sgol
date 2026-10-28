using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.Planning.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed partial class EfDirectionPreparedPlanService
{
    public async Task<DirectionPlanArchive> DownloadArchiveAsync(Guid actor, Guid id, string fingerprint, CancellationToken token = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(token);
        await OperationalTaskService.LockAsync(db, token);
        await CheckAuthorityAsync(actor, clock.UtcNow, true, token);
        var preview = await DeletionStateAsync(id, token);
        if (preview.Fingerprint != fingerprint) throw new DirectionPlanException("PLAN_DELETE_STALE", 409);
        // Use a private temporary stream so large plans never need one byte[] for the ZIP.
        var stream = new FileStream(Path.Combine(Path.GetTempPath(), $"sgol-plan-{Guid.NewGuid():N}.zip"),
            FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 65536,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        try
        {
            var assignments = db.OperationalAssignments.IgnoreQueryFilters().AsNoTracking().Where(a => a.PlanId == id);
            var assignmentIds = assignments.Select(a => a.Id);
            var occasions = db.Set<OperationalOccasion>().AsNoTracking().Where(o => o.PlanId == id);
            var occasionIds = occasions.Select(o => o.Id);
            var runs = await db.Set<OperationalSequenceRun>().AsNoTracking().Where(r => r.PlanId == id).OrderBy(r => r.Id).ToArrayAsync(token);
            var runIds = runs.Select(r => r.Id).ToArray();
            var files = new List<object>();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                async Task Add(string folder, Guid fileId, Guid ownerId, string name, string type, byte[] bytes)
                {
                    var path = $"{folder}/{ownerId:N}/{fileId:N}-{SafeArchiveName(name)}";
                    await using var entry = zip.CreateEntry(path, CompressionLevel.Fastest).Open();
                    await entry.WriteAsync(bytes, token);
                    files.Add(new { Path = path, OriginalName = name, OwnerId = ownerId, ContentType = type,
                        Bytes = bytes.LongLength, Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)) });
                }
                // Include every stored delivery/version and photograph, including previous rounds.
                await foreach (var file in db.Set<OperationalAttachment>().AsNoTracking().Where(f => assignmentIds.Contains(f.AssignmentId))
                    .OrderBy(f => f.AssignmentId).AsAsyncEnumerable().WithCancellation(token))
                    await Add("entregas", file.AssignmentId, file.AssignmentId, file.FileName, file.ContentType, file.Content);
                await foreach (var file in db.OperationalPhotographs.AsNoTracking().Where(f => assignmentIds.Contains(f.AssignmentId))
                    .OrderBy(f => f.Id).AsAsyncEnumerable().WithCancellation(token))
                    await Add("fotografias", file.Id, file.AssignmentId, file.FileName, file.ContentType, file.Content);
                await foreach (var file in db.Set<OperationalReferenceFile>().AsNoTracking().Where(f => occasionIds.Contains(f.OccasionId))
                    .OrderBy(f => f.Id).AsAsyncEnumerable().WithCancellation(token))
                    await Add("referencias", file.Id, file.OccasionId, file.FileName, file.ContentType, file.Content);
                if (files.Count != preview.EvidenceFiles) throw new InvalidOperationException("Archive file count differs from deletion preview.");
                var manifest = new {
                    Transfers = await db.Set<OperationalTransfer>().AsNoTracking().Where(t => assignmentIds.Contains(t.AssignmentId)).OrderBy(t => t.At).ToArrayAsync(token),
                    ExportedAt = clock.UtcNow, preview.Fingerprint,
                    Plan = (await DetailsAsync([await db.DirectionPreparedPlans.AsNoTracking().SingleAsync(p => p.Id == id, token)], token))[0], Files = files,
                    Starts = await db.Set<PreparedProcessStart>().AsNoTracking().Where(s => s.PlanId == id).ToArrayAsync(token),
                    Runs = runs,
                    Stages = await db.Set<OperationalSequenceStage>().AsNoTracking().Where(s => runIds.Contains(s.RunId)).ToArrayAsync(token),
                    Attempts = await db.Set<OperationalSequenceAttempt>().AsNoTracking().Where(a => runIds.Contains(a.RunId)).ToArrayAsync(token),
                    Documents = await db.Set<OperationalSequenceDocument>().AsNoTracking().Where(d => runIds.Contains(d.RunId)).ToArrayAsync(token),
                    Events = await db.Set<OperationalSequenceEvent>().AsNoTracking().Where(e => runIds.Contains(e.RunId)).ToArrayAsync(token),
                    Occasions = await occasions.ToArrayAsync(token),
                    Progress = await db.Set<OperationalProgressEntry>().AsNoTracking().Where(p => assignmentIds.Contains(p.AssignmentId)).ToArrayAsync(token)
                };
                await using var metadata = zip.CreateEntry("plan-y-archivos.json").Open();
                await JsonSerializer.SerializeAsync(metadata, manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }, token);
            }
            await tx.CommitAsync(token);
            stream.Position = 0;
            return new(stream, $"plan-{id:D}-archivos.zip", preview.Fingerprint);
        }
        catch { await stream.DisposeAsync(); throw; }
    }

    private static string SafeArchiveName(string name)
    {
        var safe = new string(name.Select(c => char.IsControl(c) || "<>:\"/\\|?*".Contains(c) ? '_' : c).ToArray()).Trim(' ', '.');
        if (safe.Length > 200)
        {
            var extension = Path.GetExtension(safe);
            if (extension.Length > 20) extension = "";
            safe = safe[..(200 - extension.Length)] + extension;
        }
        return string.IsNullOrWhiteSpace(safe) ? "archivo" : safe;
    }
}
