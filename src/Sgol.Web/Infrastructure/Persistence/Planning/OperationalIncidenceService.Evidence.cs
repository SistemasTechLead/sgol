using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed record IncidenceEvidence(Guid AssignmentId, Guid? PhotoId, string FileName,
    DateTimeOffset CreatedAt, Guid? ActorId, bool Delivered)
{
    public string? ActorName { get; init; }
}

public sealed partial class OperationalIncidenceService
{
    async Task<(OperationalIncidence Receipt, Guid[] Assignments)> EvidenceScope(Guid actor, DateOnly day, Guid receiptId, CancellationToken ct)
    {
        // History applies both supervisory role and person/change scope. Never authorize via the
        // assignment's current owner: it may have been transferred or retired since this receipt.
        var receipt = (await HistoryAsync(actor, day, ct)).SingleOrDefault(r => r.Id == receiptId)
            ?? throw Error("ACCESO_DENEGADO", 403);
        var saved = JsonSerializer.Deserialize<IncidencePreview>(receipt.ReceiptJson)!;
        return (receipt, saved.Changes.Select(c => c.Before.Id).Distinct().ToArray());
    }

    public async Task<IncidenceEvidence[]> EvidenceAsync(Guid actor, DateOnly day, Guid receiptId, CancellationToken ct)
    {
        var (receipt, ids) = await EvidenceScope(actor, day, receiptId, ct);
        var photos = await (from p in db.OperationalPhotographs.AsNoTracking()
                            join s in db.Set<OperationalCaptureSession>() on p.CaptureSessionId equals s.Id into sessions
                            from s in sessions.DefaultIfEmpty()
                            where ids.Contains(p.AssignmentId) && p.CreatedAt <= receipt.At
                            orderby p.CreatedAt, p.Id
                            select new IncidenceEvidence(p.AssignmentId, p.Id, p.FileName, p.CreatedAt,
                                s == null ? null : (Guid?)s.ActorId, p.AttachedAt != null && p.AttachedAt <= receipt.At)).ToArrayAsync(ct);
        var files = await db.Set<OperationalAttachment>().AsNoTracking()
            .Where(a => ids.Contains(a.AssignmentId) && a.CreatedAt <= receipt.At)
            .Select(a => new IncidenceEvidence(a.AssignmentId, null, a.FileName, a.CreatedAt, null, true)).ToArrayAsync(ct);
        var actors = photos.Where(p => p.ActorId != null).Select(p => p.ActorId!.Value).ToArray();
        var names = await (from u in db.AppUsers join p in db.People on u.PersonId equals p.Id
                           where actors.Contains(u.Id) select new { u.Id, p.DisplayName }).ToDictionaryAsync(p => p.Id, p => p.DisplayName, ct);
        return [..photos.Select(p => p with { ActorName = p.ActorId is {} id ? names.GetValueOrDefault(id) : null }), ..files];
    }

    public async Task<(byte[] Content, string ContentType, string FileName)> EvidenceFileAsync(
        Guid actor, DateOnly day, Guid receiptId, Guid assignmentId, Guid? photoId, CancellationToken ct)
    {
        var (receipt, ids) = await EvidenceScope(actor, day, receiptId, ct);
        if (!ids.Contains(assignmentId)) throw Error("ACCESO_DENEGADO", 403);
        if (photoId != null)
        {
            var photo = await db.OperationalPhotographs.AsNoTracking().SingleOrDefaultAsync(p => p.Id == photoId &&
                p.AssignmentId == assignmentId && p.CreatedAt <= receipt.At, ct) ?? throw Error("ARCHIVO_NO_DISPONIBLE", 404);
            return (photo.Content, photo.ContentType, photo.FileName);
        }
        var file = await db.Set<OperationalAttachment>().AsNoTracking().SingleOrDefaultAsync(a => a.AssignmentId == assignmentId &&
            a.CreatedAt <= receipt.At, ct) ?? throw Error("ARCHIVO_NO_DISPONIBLE", 404);
        return (file.Content, file.ContentType, file.FileName);
    }
}
