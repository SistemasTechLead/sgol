namespace Sgol.Configuration.Contracts;

public sealed record DirectionPlanningTaskOption(string TaskCode, string Name, string? PeriodType = null, string? AssignedRole = null,
    bool IsWeeklyOperationsTask = false);

/// <summary>Configuration owns the preparation catalog; selection does not activate a task.</summary>
public interface IDirectionPlanningCatalogReader
{
    Task<IReadOnlyList<DirectionPlanningTaskOption>> ReadTaskOptionsAsync(CancellationToken token = default);
    Task<IReadOnlyList<DirectionPlanningTaskOption>> ReadPredefinedTasksAsync(string periodType, CancellationToken token = default);
}
