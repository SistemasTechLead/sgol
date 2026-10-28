using Microsoft.EntityFrameworkCore;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed record OperationalLifecycleEntry(string Kind, DateTimeOffset? At, Guid? ActorId,
    string? Comment, string? Conclusion, string? Decision, string? ActorName = null);
public sealed record OperationalContribution(Guid PersonId, string PersonName, int Units);

public sealed partial class OperationalTaskService
{
    private async Task AuthorizeHistoryAsync(Guid actor, Guid id, Guid person, CancellationToken token)
    {
        var row = await db.OperationalAssignments.IgnoreQueryFilters().AsNoTracking().Where(a => a.Id == id).Select(a => new { a.PersonId, a.RetiredAt }).SingleOrDefaultAsync(token);
        if (row?.PersonId == person) return;
        if (row?.RetiredAt != null) await direction.AuthorizeAsync(actor, token);
        else await new OperationalSupervisionService(db, clock, direction).AuthorizeAssignmentAsync(actor, id, token);
    }
    public async Task<IReadOnlyList<OperationalLifecycleEntry>> LifecycleHistoryAsync(Guid actor, Guid id, CancellationToken token)
    {
        var person = await PersonAsync(actor, token);
        await AuthorizeHistoryAsync(actor, id, person, token);
        var state = await db.OperationalAssignments.IgnoreQueryFilters().AsNoTracking().Where(a => a.Id == id)
            .Select(a => new { a.CompletedAt, a.CompletedBy, a.CompletionKind, a.Comment, a.Conclusion,
                a.ReviewStatus, a.ReviewedAt, a.ReviewedBy, a.ReviewComment, a.ReviewDecision,
                a.ReassignedFromId, a.ReassignmentComment, a.StartsAt }).SingleAsync(token);
        var events = new List<OperationalLifecycleEntry>();
        var transfers = await db.Set<OperationalTransfer>().AsNoTracking().Where(t => t.AssignmentId == id).OrderBy(t => t.At).ToArrayAsync(token);
        foreach (var t in transfers)
            events.Add(new("TRANSFERENCIA", t.At, t.ActorId,
                $"{t.FromName} → {t.ToName} · {t.ProgressUnits} unidades conservadas · {t.RemainingMinutes} min pendientes · Plazo anterior {TimeZoneInfo.ConvertTime(t.PreviousDueAt, OperationalSchedule.Zone):dd/MM HH:mm}; nuevo {TimeZoneInfo.ConvertTime(t.NewDueAt, OperationalSchedule.Zone):dd/MM HH:mm} · {t.Reason}", null, null));
        if (state.ReassignedFromId is not null)
            events.Add(new("REASIGNACION", state.StartsAt, null, state.ReassignmentComment, null, null));
        if (state.CompletedAt is not null)
            events.Add(new(state.CompletionKind ?? "HISTORICO", state.CompletedAt, state.CompletedBy, state.Comment, state.Conclusion, null));
        if (state.ReviewedAt is not null || state.ReviewStatus is "CONCLUIDA" or "NO_SATISFECHA" or "REASIGNADA")
            events.Add(new("REVISION", state.ReviewedAt, state.ReviewedBy, state.ReviewComment, null, state.ReviewDecision ?? "HISTORICO"));
        var redistributions = await db.AuditEvents.FromSqlInterpolated($"SELECT * FROM audit_event WHERE action = 'CAPACITY_REDISTRIBUTED' AND after_data -> 'Moves' @> jsonb_build_array(jsonb_build_object('TaskId', {id.ToString("D")}))").AsNoTracking().ToArrayAsync(token);
        foreach (var change in redistributions) {
            var data = change.AfterData!.RootElement;
            var move = data.GetProperty("Moves").EnumerateArray().First(m => m.GetProperty("TaskId").GetGuid() == id);
            var start = TimeZoneInfo.ConvertTime(move.GetProperty("StartsAt").GetDateTimeOffset(), OperationalSchedule.Zone);
            var due = TimeZoneInfo.ConvertTime(move.GetProperty("DueAt").GetDateTimeOffset(), OperationalSchedule.Zone);
            var reason = data.GetProperty("Change").GetProperty("Reason").GetString();
            events.Add(new("REDISTRIBUCION", change.OccurredAt, change.ActorUserId, $"{reason} · Horario confirmado {start:dd/MM HH:mm}–{due:dd/MM HH:mm}", null, null));
        }
        var adopted = await db.AuditEvents.Where(a => a.ResourceId == id && a.Action == "OPERATIONAL_ASSIGNMENT_AGENDA_ADOPTED").AsNoTracking().OrderBy(a => a.OccurredAt).ToArrayAsync(token);
        foreach (var change in adopted)
        {
            var after = change.AfterData!.RootElement;
            var start = TimeZoneInfo.ConvertTime(after.GetProperty("StartsAt").GetDateTimeOffset(), OperationalSchedule.Zone);
            var due = TimeZoneInfo.ConvertTime(after.GetProperty("DueAt").GetDateTimeOffset(), OperationalSchedule.Zone);
            events.Add(new("REACOMODO", change.OccurredAt, change.ActorUserId, $"Agenda por criticidad confirmada: {start:dd/MM HH:mm}–{due:dd/MM HH:mm}", null, null));
        }
        var refreshes = await db.AuditEvents.AsNoTracking().Where(e => e.ResourceId == id && e.Action == "OPERATIONAL_ASSIGNMENT_REFRESHED").OrderBy(e => e.OccurredAt).ToArrayAsync(token);
        foreach (var change in refreshes)
        {
            var after = change.AfterData!.RootElement;
            events.Add(new("ACTUALIZACION", change.OccurredAt, change.ActorUserId,
                "Parámetros actualizados: " + string.Join(", ", after.GetProperty("Fields").EnumerateArray().Select(f => f.GetString())) + " · " + after.GetProperty("Reason").GetString(), null, null));
        }
        var carryovers = await db.Set<OperationalCarryoverRequest>().AsNoTracking().Where(r => r.State == "APROBADA").OrderBy(r => r.DecidedAt).ToArrayAsync(token);
        foreach (var request in carryovers.Where(r => r.Items().Any(i => i.AssignmentId == id)))
            events.Add(new("ARRASTRE", request.DecidedAt, request.DecidedBy, $"{request.CauseDescription} · {request.Minutes} min · {request.DecisionComment} · Solicitud {request.Id}", null, null));
        var retiredEvents = await db.AuditEvents.AsNoTracking().Where(e => e.ResourceId == id && e.Action == "OPERATIONAL_ASSIGNMENT_RETIRED").ToArrayAsync(token);
        foreach (var change in retiredEvents)
            events.Add(new("RETIRO", change.OccurredAt, change.ActorUserId, change.AfterData!.RootElement.GetProperty("Reason").GetString(), null, null));
        var actors = events.Where(e => e.ActorId != null).Select(e => e.ActorId!.Value).Distinct().ToArray();
        var names = await (from u in db.AppUsers.AsNoTracking() join p in db.People.AsNoTracking() on u.PersonId equals p.Id
            where actors.Contains(u.Id) select new { u.Id, p.DisplayName }).ToDictionaryAsync(u => u.Id, u => u.DisplayName, token);
        return events.Select(e => e with { ActorName = e.ActorId is { } uid ? names.GetValueOrDefault(uid) : null })
            .OrderBy(e => e.At ?? DateTimeOffset.MaxValue).ToArray();
    }

    public async Task<IReadOnlyList<OperationalTransfer>> MyTransfersAsync(Guid actor, CancellationToken token)
    {
        var person = await PersonAsync(actor, token);
        return await db.Set<OperationalTransfer>().AsNoTracking().Where(t => t.FromPersonId == person || t.ToPersonId == person)
            .OrderByDescending(t => t.At).Take(100).ToArrayAsync(token);
    }

    public async Task<IReadOnlyList<OperationalContribution>> ContributionsAsync(Guid actor, Guid id, CancellationToken token)
    {
        var person = await PersonAsync(actor, token);
        await AuthorizeHistoryAsync(actor, id, person, token);
        return await (from e in db.OperationalProgressEntries.AsNoTracking()
            join u in db.AppUsers.AsNoTracking() on e.ActorId equals u.Id
            join p in db.People.AsNoTracking() on u.PersonId equals p.Id
            where e.AssignmentId == id
            group e by new { p.Id, p.DisplayName } into g
            orderby g.Key.DisplayName
            select new OperationalContribution(g.Key.Id, g.Key.DisplayName, g.Sum(x => x.Units))).ToArrayAsync(token);
    }
}

