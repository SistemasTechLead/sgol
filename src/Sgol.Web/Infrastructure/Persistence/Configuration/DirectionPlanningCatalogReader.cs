using Sgol.Configuration.Contracts;
using Microsoft.EntityFrameworkCore;
using Sgol.Web.Infrastructure.Persistence.Planning;

namespace Sgol.Web.Infrastructure.Persistence.Configuration;

public sealed class DirectionPlanningCatalogReader(SgolDbContext db) : IDirectionPlanningCatalogReader
{
    public async Task<IReadOnlyList<DirectionPlanningTaskOption>> ReadTaskOptionsAsync(CancellationToken token = default) =>
        await db.OperationalTasks.AsNoTracking().Where(t => t.ProcessId == OperationalSequence.GeneralId && t.Active && !t.Deleted && t.PeriodType != "OCASIONAL").OrderBy(t => t.Name)
            .Select(t => new DirectionPlanningTaskOption(t.TaskCode, t.Name, t.PeriodType, t.AssignedRole, t.IsWeeklyOperationsTask)).ToArrayAsync(token);
    public async Task<IReadOnlyList<DirectionPlanningTaskOption>> ReadPredefinedTasksAsync(string periodType, CancellationToken token = default) =>
        await db.OperationalTasks.AsNoTracking().Where(t => t.ProcessId == OperationalSequence.GeneralId && t.Active && !t.Deleted && t.PeriodType != "OCASIONAL" && t.PeriodType == periodType).OrderBy(t => t.Name)
            .Select(t => new DirectionPlanningTaskOption(t.TaskCode, t.Name, t.PeriodType, t.AssignedRole)).ToArrayAsync(token);
}
