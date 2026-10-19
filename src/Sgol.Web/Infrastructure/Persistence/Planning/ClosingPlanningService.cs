using Microsoft.EntityFrameworkCore;
using Sgol.Identity.Contracts;
using Sgol.Planning.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed class ClosingSelectionInput
{
    public DateOnly Day { get; set; }
    public Guid PersonId { get; set; }
    public long Revision { get; set; }
    public Guid Key { get; set; } = Guid.CreateVersion7();
}
public sealed record ClosingCandidate(Guid PersonId, string Name, string Hours);
public sealed record ClosingPlanningRow(DateOnly Day, Guid? PersonId, long Revision, ClosingCandidate[] Candidates, string Hours);

public sealed partial class PersonalScheduleService
{
    public async Task<ClosingPlanningRow[]> ReadClosingAsync(Guid actor, DateOnly from, CancellationToken ct)
    {
        await direction.AuthorizeAsync(actor, ct);
        if(from.DayNumber > DateOnly.MaxValue.DayNumber-7) throw new DirectionPlanException("SCHEDULE_DATE_INVALID",422);
        var ledger = await OperationalCapacityService.LedgerAsync(db, clock.UtcNow, ct);
        var context = ledger.Temporal;
        var people = ledger.Workers;
        var absent = await db.AvailabilityDayVersions.AsNoTracking().Where(a => a.Status == Sgol.Organization.Contracts.AvailabilityVersionStatus.Current && !a.IsAvailable && a.LocalDate >= from && a.LocalDate < from.AddDays(7)).ToArrayAsync(ct);
        return Enumerable.Range(0, 7).Select(n => {
            var day = from.AddDays(n);
            var excluded = context.Rotation(day)?.ExcludedPersonId;
            var boundary = day.DayOfWeek == DayOfWeek.Sunday ? 840 : 1200;
            var candidates = people.Where(p => p.Id != excluded && p.Role != CanonicalRole.Direction &&
                    (p.EmploymentEnd == null || p.EmploymentEnd > TemporalEligibilityContext.At(day,boundary-60)) &&
                    (p.RoleEnd == null || p.RoleEnd > TemporalEligibilityContext.At(day,boundary-60)) &&
                    !absent.Any(a => a.PersonId == p.Id && a.LocalDate == day))
                .Select(p => new { Person=p, Spans=context.Day(p.Id,day,false).Intervals })
                .Where(p => p.Spans?.Any(i => i.Entry < boundary && i.Exit >= boundary-60) == true)
                .OrderBy(p=>p.Person.Name)
                .Select(p=>new ClosingCandidate(p.Person.Id,p.Person.Name,string.Join(" · ",p.Spans!.Select(i=>$"{i.Entry/60:00}:{i.Entry%60:00}–{i.Exit/60:00}:{i.Exit%60:00}")))).ToArray();
            var selected=context.ClosingPerson(day);
            var hours=selected is {} personId ? string.Join(" · ",(context.Day(personId,day).Intervals??[]).Select(i=>$"{i.Entry/60:00}:{i.Entry%60:00}–{i.Exit/60:00}:{i.Exit%60:00}")) : "—";
            return new ClosingPlanningRow(day,selected,context.Coverages.Where(c=>c.Day==day).Select(c=>c.Revision).DefaultIfEmpty().Max(),candidates,hours);
        }).ToArray();
    }

    public async Task SaveClosingAsync(Guid actor, ClosingSelectionInput input, CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        await OperationalTaskService.LockAsync(db,ct);
        await direction.AuthorizeAsync(actor,ct);
        var replay=await db.Set<ClosingCoverageVersion>().AsNoTracking().SingleOrDefaultAsync(c=>c.IntentKey==input.Key,ct);
        if(replay!=null)
        {
            if(replay.ChangedBy!=actor || replay.Day!=input.Day || replay.PersonId!=input.PersonId || replay.Revision!=input.Revision+1) throw new DirectionPlanException("SCHEDULE_STALE",409);
            await tx.CommitAsync(ct); return;
        }
        if(input.Key==Guid.Empty || input.Day<WeekContract.LocalToday(clock.UtcNow)) throw new DirectionPlanException("SCHEDULE_DATE_INVALID",422);
        var row=(await ReadClosingAsync(actor,input.Day,ct))[0];
        if(row.Revision!=input.Revision) throw new DirectionPlanException("SCHEDULE_STALE",409);
        if(!row.Candidates.Any(c=>c.PersonId==input.PersonId)) throw new DirectionPlanException("CLOSING_COVERAGE_REQUIRED",422);
        var version=new ClosingCoverageVersion { Id=Guid.CreateVersion7(),Day=input.Day,PersonId=input.PersonId,Revision=input.Revision+1,Reason="Cobertura seleccionada en Planificación; extensión de una hora en el último tramo.",ChangedBy=actor,ChangedAt=clock.UtcNow,IntentKey=input.Key };
        db.Add(version);
        Audit(actor,version.Id,input.Key,"CLOSING_COVERAGE_SAVED",new { version,PreviousPersonId=row.PersonId });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
}
