using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.Planning.Contracts;
namespace Sgol.Web.Infrastructure.Persistence.Planning;

internal sealed record WeeklyProcessDefinition(OperationalSequence Root, OperationalSequenceVersion Version, OperationalSequenceStep[] Steps)
{
    public OperationalTask[] Tasks => Steps.Select(s => JsonSerializer.Deserialize<OperationalTask>(s.TaskSnapshot!)!).ToArray();
    public string Code => Tasks[0].TaskCode;
}
internal static class WeeklyProcessPlanning
{
    internal static async Task<WeeklyProcessDefinition[]> ReadAsync(SgolDbContext db, CancellationToken token)
    {
        var published = await (from r in db.Set<OperationalSequence>().AsNoTracking()
            join v in db.Set<OperationalSequenceVersion>().AsNoTracking() on r.PublishedVersionId equals v.Id
            where r.Enabled && r.DeletedAt == null && !r.IsGeneral && v.PublishedAt != null && v.StartKind == "SEMANAL"
            orderby r.Id select new { Root = r, Version = v }).ToArrayAsync(token);
        var selected = published.Where(p => ProcessStartSchedule.Read(p.Version.StartScheduleJson).WeeklyOperations).ToArray();
        var ids = selected.Select(p => p.Version.Id).ToArray();
        var steps = await db.Set<OperationalSequenceStep>().AsNoTracking().Where(s => ids.Contains(s.VersionId)).OrderBy(s => s.Position).ToArrayAsync(token);
        return selected.Select(p => new WeeklyProcessDefinition(p.Root, p.Version, steps.Where(s => s.VersionId == p.Version.Id).ToArray())).ToArray();
    }
    internal static async Task<PreparedProcessStart[]> PreviewAsync(SgolDbContext db, WeeklyProcessDefinition[] definitions,
        DateOnly from, DateOnly through, CapacityLedger ledger, OperationalSchedulingPolicy policy, DateTimeOffset now, CancellationToken token)
    {
        var result = new List<PreparedProcessStart>();
        foreach (var definition in definitions)
        {
            var tasks = definition.Tasks;
            var taskIds = tasks.Select(t => t.Id).ToArray();
            if (await db.OperationalTasks.AnyAsync(t => taskIds.Contains(t.Id) && (!t.Active || t.Deleted || t.ProcessId != definition.Root.Id), token))
                throw new DirectionPlanException("PROCESS_MEMBERSHIP_INVALID", 409);
            var schedule = ProcessStartSchedule.Read(definition.Version.StartScheduleJson).Calendar("SEMANAL");
            var starts = OperationalSchedule.Dates(from, through, schedule).Where(d => d >= ledger.Today).Select(schedule.StartOn).ToArray();
            foreach (var start in starts)
            {
                var key = $"C:{definition.Root.Id:N}:{WeekContract.LocalToday(start):yyyyMMdd}";
                if (await db.Set<PreparedProcessStart>().AnyAsync(s => s.StartKey == key, token) || await db.Set<OperationalSequenceRun>().AnyAsync(r => r.StartKey == key, token))
                    throw new DirectionPlanException("PLAN_OCCURRENCES_RESERVED", 422) { Data = { ["ProcessName"] = definition.Version.Name, ["ProcessDay"] = WeekContract.LocalToday(start) } };
                var input = new ProcessPlanInput { Key = definition.Version.Id, People = definition.Steps.ToDictionary(s => s.Position, _ => Guid.Empty) };
                var notBefore = OperationalSchedule.Instant(ledger.Today);
                var people = ProcessPlanningService.ResolvePeople(input, definition.Version.Id, definition.Steps, tasks, [start], ledger, policy, notBefore, token);
                var agenda = ProcessPlanningService.Reservations(definition.Version.Id, definition.Version.Id, start, people, definition.Steps, policy, ledger, notBefore).ToArray();
                foreach (var assignment in agenda) {
                    ProcessPlanningService.RequireReservation(ledger, assignment, definition.Steps.Single(s => s.TaskId == assignment.TaskId));
                    assignment.PersonName = ledger.Workers.Single(w => w.Id == assignment.PersonId).Name;
                    ledger = ledger.WithAdded(assignment);
                }
                result.Add(new PreparedProcessStart { Id = Guid.CreateVersion7(), ProcessId = definition.Root.Id, VersionId = definition.Version.Id,
                    StartKey = key, StartsAt = start, Participants = JsonSerializer.Serialize(people), AgendaJson = JsonSerializer.Serialize(agenda),
                    ItemRatesJson = JsonSerializer.Serialize(await ProcessItemRules.CaptureAsync(db, definition.Steps, token)) });
            }
        }
        return result.ToArray();
    }
}
