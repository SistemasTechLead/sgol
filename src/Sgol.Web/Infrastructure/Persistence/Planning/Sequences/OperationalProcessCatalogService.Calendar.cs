using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed partial class OperationalProcessCatalogService
{
    // Runs under the catalog transaction and capacity lock. Existing dates, including
    // manual plans and older published versions, keep their captured definition.
    private async Task CreateAutomaticCalendarAsync(Guid actor, OperationalSequence root, CancellationToken token)
    {
        var version = await db.Set<OperationalSequenceVersion>().SingleAsync(v => v.Id == root.PublishedVersionId, token);
        if (version.StartKind == "OCASIONAL" || ProcessStartSchedule.Read(version.StartScheduleJson).WeeklyOperations) return;
        var from = WeekContract.LocalToday(clock.UtcNow);
        var through = from.AddYears(1).AddDays(-1);
        var existing = (await db.Set<PreparedProcessStart>().Where(s => s.ProcessId == root.Id).Select(s => s.StartKey).ToArrayAsync(token))
            .Concat(await db.Set<OperationalSequenceRun>().Where(r => r.StartKey.StartsWith($"C:{root.Id:N}:"))
                .Select(r => r.StartKey).ToArrayAsync(token)).ToHashSet();
        foreach (var start in await db.Set<PreparedProcessStart>().Where(s => s.ProcessId == root.Id).Select(s => s.StartsAt).ToArrayAsync(token))
            existing.Add($"C:{root.Id:N}:{WeekContract.LocalToday(start):yyyyMMdd}");
        var dates = OperationalSequenceRules.Dates(from, through, version)
            .Where(d => !existing.Contains($"C:{root.Id:N}:{d:yyyyMMdd}")).ToArray();
        if (dates.Length == 0) return;
        var steps = await db.Set<OperationalSequenceStep>().AsNoTracking().Where(s => s.VersionId == version.Id).OrderBy(s => s.Position).ToArrayAsync(token);
        var name = $"Calendario anual · {version.Name}";
        var plan = DirectionPreparedPlan.ForProcess(Guid.CreateVersion7(), BranchScope.LorettaId, actor, clock.UtcNow,
            name.Length > 120 ? name[..120] : name, from, through);
        if (version.AgendaVersion != null) plan.CaptureSchedulingPolicy((await OperationalSchedulingPolicyService.CurrentAsync(db, token)).Id);
        plan.MarkAssigned();
        db.Add(plan);
        foreach (var step in steps) {
            var task = JsonSerializer.Deserialize<OperationalTask>(step.TaskSnapshot!)!;
            db.Add(new DirectionPreparedPlanItem(plan.Id, task.TaskCode, task.Name, step.Position - 1));
        }
        var rates = JsonSerializer.Serialize(await ProcessItemRules.CaptureAsync(db, steps, token));
        var sources = JsonSerializer.Serialize(version.CountSemanticsVersion >= ProcessCountSources.CurrentVersion
            ? ProcessCountSources.FromSteps(steps) : new Dictionary<Guid, Guid>());
        foreach (var day in dates) db.Add(new PreparedProcessStart {
            Id = Guid.CreateVersion7(), PlanId = plan.Id, ProcessId = root.Id, VersionId = version.Id,
            StartKey = $"C:{root.Id:N}:{day:yyyyMMdd}", Automatic = true, Status = "PENDIENTE",
            StartsAt = ProcessStartSchedule.Recurring(version.StartKind)
                ? ProcessStartSchedule.Read(version.StartScheduleJson).Calendar(version.StartKind).StartOn(day)
                : OperationalSchedule.Instant(day, version.StartHour),
            ItemRatesJson = rates, CountSourcesJson = sources,
            PendingReason = "Pendiente de comprobar responsables y capacidad. La asignación se intentará automáticamente."
        });
        Audit(actor, "PROCESS_ANNUAL_CALENDAR_CREATED", root.Id, new { PlanId = plan.Id, From = from, Through = through, Dates = dates.Length });
    }
}
