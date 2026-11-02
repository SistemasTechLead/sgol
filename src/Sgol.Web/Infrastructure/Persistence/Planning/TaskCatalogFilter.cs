namespace Sgol.Web.Infrastructure.Persistence.Planning;

// Catalogue-only predicates. Never applied to assignment snapshots or history.
public sealed class TaskCatalogFilter
{
    public string? Requirement { get; set; }
    public string? Period { get; set; }
    public string? Criticality { get; set; }
    public string? Role { get; set; }
    public string? State { get; set; }
    public string? Calendar { get; set; }
    public int? Repetitions { get; set; }

    public bool IsValid =>
        (string.IsNullOrEmpty(Requirement) || OperationalSchedule.Requirements.Contains(Requirement)) &&
        (string.IsNullOrEmpty(Period) || Period == "PROCESO" || OperationalSchedule.Periods.Contains(Period)) &&
        (string.IsNullOrEmpty(Criticality) || Criticality is "CRITICA" or "IMPORTANTE" or "FLEXIBLE" or "SIN_DEFINIR") &&
        (string.IsNullOrEmpty(Role) || OperationalSchedule.Roles.Contains(Role)) &&
        (string.IsNullOrEmpty(State) || State is "ACTIVA" or "INACTIVA") &&
        (string.IsNullOrEmpty(Calendar) || Calendar is "DISTRIBUIDA" or "DIAS_FIJOS" or "PROGRESO") &&
        (Repetitions is null or >= 1 and <= 100);

    public IQueryable<OperationalTask> Apply(IQueryable<OperationalTask> query)
    {
        if (!IsValid) throw new ArgumentException("Invalid catalogue filters.");
        if (!string.IsNullOrEmpty(Requirement)) query = query.Where(t => t.Requirement == Requirement);
        if (Period == "PROCESO") query = query.Where(t => t.ProcessId != OperationalSequence.GeneralId);
        else if (!string.IsNullOrEmpty(Period)) query = query.Where(t => t.ProcessId == OperationalSequence.GeneralId && t.PeriodType == Period);
        if (Criticality == "SIN_DEFINIR") query = query.Where(t => t.OperationalCriticality == null || t.OperationalCriticality == "");
        else if (!string.IsNullOrEmpty(Criticality)) query = query.Where(t => t.OperationalCriticality == Criticality);
        if (!string.IsNullOrEmpty(Role)) query = query.Where(t => t.AssignedRole == Role);
        if (!string.IsNullOrEmpty(State)) query = query.Where(t => t.Active == (State == "ACTIVA"));
        if (!string.IsNullOrEmpty(Calendar)) query = query.Where(t => t.ProcessId == OperationalSequence.GeneralId && t.PeriodType != "OCASIONAL" && t.ScheduleMode == Calendar);
        if (Repetitions != null) query = query.Where(t => t.ProcessId == OperationalSequence.GeneralId && t.PeriodType != "OCASIONAL" && t.Repetitions == Repetitions);
        return query;
    }
}
