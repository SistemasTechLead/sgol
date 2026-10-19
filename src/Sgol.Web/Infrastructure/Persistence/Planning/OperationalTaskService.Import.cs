using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Sgol.Planning.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed record TaskImportPreview(string Fingerprint, OperationalTask[] Additions, string[] Exclusions);

public sealed partial class OperationalTaskService
{
    public static string NormalizeImportName(string text) => Regex.Replace(new string(text.Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray())
        .Normalize(NormalizationForm.FormC).ToUpperInvariant().Trim(), @"\s+", " ");

    public async Task<TaskImportPreview> PreviewImportAsync(Guid actor, IReadOnlyList<OperationalTask> batch, CancellationToken token)
    {
        await direction.AuthorizeAsync(actor, token);
        if (batch.Count is < 1 or > 1000 || batch.Any(t => !Regex.IsMatch(t.TaskCode, @"^TAR-\d{4}$") ||
            t.ProcessId != OperationalSequence.GeneralId || t.Deleted || t.Id != Guid.Empty))
            throw new DirectionPlanException("TASK_IMPORT_INVALID", 422);
        foreach (var task in batch) task.Validate();
        if (batch.Select(t => t.TaskCode).Distinct(StringComparer.OrdinalIgnoreCase).Count() != batch.Count ||
            batch.Select(t => NormalizeImportName(t.Name)).Distinct().Count() != batch.Count)
            throw new DirectionPlanException("TASK_IMPORT_DUPLICATE", 422);
        var catalog = await db.OperationalTasks.AsNoTracking().OrderBy(t => t.Id).ToArrayAsync(token);
        var specialties = await db.Set<Sgol.Web.Infrastructure.Persistence.Organization.Specialization>().AsNoTracking().OrderBy(s => s.Id).ToArrayAsync(token);
        if (batch.Any(t => t.SpecializationId is { } id && !specialties.Any(s => s.Id == id && s.Active)))
            throw new DirectionPlanException("TASK_SPECIALIZATION_INVALID", 422);
        var additions = batch.Where(t => !catalog.Any(c => string.Equals(c.TaskCode, t.TaskCode, StringComparison.OrdinalIgnoreCase) ||
            NormalizeImportName(c.Name) == NormalizeImportName(t.Name))).OrderBy(t => t.TaskCode, StringComparer.Ordinal).ToArray();
        var excluded = batch.Except(additions).Select(t => t.TaskCode).Order().ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { catalog, specialties, batch })));
        return new(hash, additions, excluded);
    }

    // Only definitions are added. Existing definitions (including deleted ones) are immutable here.
    public async Task<string[]> ImportAsync(Guid actor, IReadOnlyList<OperationalTask> batch, string fingerprint, string sourceSha256, CancellationToken token)
    {
        await direction.AuthorizeAsync(actor, token);
        await using var tx = await db.Database.BeginTransactionAsync(token);
        await LockAsync(db, token);
        var preview = await PreviewImportAsync(actor, batch, token);
        if (preview.Fingerprint != fingerprint || !Regex.IsMatch(sourceSha256, @"^[a-fA-F0-9]{64}$"))
            throw new DirectionPlanException("TASK_IMPORT_STALE", 409);
        foreach (var input in preview.Additions)
        {
            var task = JsonSerializer.Deserialize<OperationalTask>(JsonSerializer.Serialize(input))!;
            task.Id = Guid.CreateVersion7(); task.Revision = 1;
            db.OperationalTasks.Add(task);
            Audit(actor, "OPERATIONAL_TASK_IMPORTED", task.Id, new { sourceSha256, preview.Fingerprint, Task = task });
        }
        await db.SaveChangesAsync(token);
        await tx.CommitAsync(token);
        return preview.Additions.Select(t => t.TaskCode).ToArray();
    }
}
