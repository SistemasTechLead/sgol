using Sgol.Planning.Contracts;
using Microsoft.EntityFrameworkCore;
using Sgol.Identity.Contracts;
using Sgol.Web.Presentation.Search;
namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed record SupervisionPage(SupervisedTask[] Items, SupervisedTask[] Options, int Total, int Filtered, int Page);
public sealed partial class OperationalSupervisionService
{
    public async Task<SupervisedTask[]> FindManyAsync(Guid actor, Guid[] ids, CancellationToken token) =>
        await Query(await AuthorizeAsync(actor, token), actor).Where(t => ids.Contains(t.Assignment.Id)).ToArrayAsync(token);
    public async Task<Guid[]> VisibleIdsAsync(Guid actor, CancellationToken token) =>
        await Query(await AuthorizeAsync(actor, token), actor).Select(t => t.Assignment.Id).ToArrayAsync(token);
    public async Task<SupervisedTask?> FindAsync(Guid actor, Guid id, CancellationToken token) =>
        await Query(await AuthorizeAsync(actor, token), actor).SingleOrDefaultAsync(t => t.Assignment.Id == id, token);
    public async Task<SupervisionPage> PageAsync(Guid actor, string section, IReadOnlyDictionary<string, string> filter, int page, CancellationToken token)
    {
        var role = await AuthorizeAsync(actor, token);
        var now = clock.UtcNow;
        var query = Query(role, actor);
        if (section == "pending") query = query.Where(t => t.Assignment.CompletedAt != null && (t.Assignment.ReviewStatus == null || t.Assignment.ReviewStatus == "PENDIENTE_REVISION") && (role != CanonicalRole.Direction || t.Role == CanonicalRole.Subcoordination || t.Assignment.SpecializationId != null));
        else query = query.Where(t => t.Role == section);
        var total = await query.CountAsync(token);
        var persons = await query.Select(t => new { t.Assignment.PersonId, t.Assignment.PersonName }).Distinct().ToArrayAsync(token);
        var plans = await query.Select(t => new { t.Assignment.PlanId, t.PlanName }).Distinct().ToArrayAsync(token);
        var options = persons.Select(p => new SupervisedTask { Assignment = new() { PersonId = p.PersonId, PersonName = p.PersonName }, Role = "", PlanName = "", Period = "" })
            .Concat(plans.Select(p => new SupervisedTask { Assignment = new() { PlanId = p.PlanId }, Role = "", PlanName = p.PlanName, Period = "" })).ToArray();
        string Value(string key) => filter.GetValueOrDefault(key) ?? "";
        var today = WeekContract.LocalToday(now);
        var range = Value("range");
        DateOnly? start = range switch {
            "SEMANAL" => today.AddDays(-(((int)today.DayOfWeek + 6) % 7)), "MENSUAL" => new(today.Year, today.Month, 1),
            "TRIMESTRAL" => new(today.Year, (today.Month - 1) / 3 * 3 + 1, 1), "SEMESTRAL" => new(today.Year, (today.Month - 1) / 6 * 6 + 1, 1), "ANUAL" => new(today.Year, 1, 1), _ => null };
        DateOnly? end = start is {} from ? range switch { "SEMANAL" => from.AddDays(6), "MENSUAL" => from.AddMonths(1).AddDays(-1), "TRIMESTRAL" => from.AddMonths(3).AddDays(-1), "SEMESTRAL" => from.AddMonths(6).AddDays(-1), _ => new DateOnly(from.Year, 12, 31) } : null;
        if (range == "CUSTOM") { start = DateOnly.TryParse(Value("from"), out var f) ? f : null; end = DateOnly.TryParse(Value("to"), out var e) ? e : null; }
        if (start is {} first) { var instant = OperationalSchedule.Instant(first); query = query.Where(t => t.Assignment.StartsAt >= instant); }
        if (end is {} last) { var instant = OperationalSchedule.Instant(last.AddDays(1)); query = query.Where(t => t.Assignment.StartsAt < instant); }
        var search = SearchText.Normalize(Value("search").Trim());
        if (search.Length > 0) query = query.Where(t => SearchText.SqlNormalize(t.Assignment.Name).Contains(search) || SearchText.SqlNormalize(t.Assignment.TaskCode).Contains(search));
        var period = Value("period"); if (period.Length > 0) query = query.Where(t => t.Period == period);
        var requirement = Value("requirement"); if (requirement.Length > 0) query = query.Where(t => t.Assignment.Requirement == requirement);
        var person = Value("person"); if (person.Length > 0) query = query.Where(t => t.Assignment.PersonId.ToString() == person);
        var plan = Value("plan"); if (plan.Length > 0) query = query.Where(t => t.Assignment.PlanId.ToString() == plan);
        if (int.TryParse(Value("repetitions"), out var repeats)) query = query.Where(t => t.Assignment.Repetitions == repeats);
        if (int.TryParse(Value("minutes"), out var minutes)) query = query.Where(t => t.Assignment.LoadMinutes == minutes);
        if (int.TryParse(Value("hours"), out var hours)) query = query.Where(t => (t.Assignment.DueAt - t.Assignment.StartsAt).TotalHours == hours);
        var status = Value("status");
        if (status.Length > 0) query = query.Where(t => (t.Assignment.ReviewStatus == "CONCLUIDA" || t.Assignment.ReviewStatus == "NO_SATISFECHA" || t.Assignment.ReviewStatus == "REASIGNADA" ? t.Assignment.ReviewStatus : t.Assignment.CompletedAt != null ? "PENDIENTE_REVISION" : t.Assignment.StartsAt > now ? "PROGRAMADA" : t.Assignment.DueAt < now ? "VENCIDA" : "ASIGNADA") == status);
        var filtered = await query.CountAsync(token);
        page = Math.Clamp(page, 1, Math.Max(1, (filtered + 49) / 50));
        var ordered = section == "pending" ? query.OrderByDescending(t => t.Assignment.CompletedAt).ThenBy(t => t.Assignment.Id) : query.OrderBy(t => t.Assignment.StartsAt).ThenBy(t => t.Assignment.Id);
        return new(await ordered.Skip((page - 1) * 50).Take(50).ToArrayAsync(token), options, total, filtered, page);
    }
}
