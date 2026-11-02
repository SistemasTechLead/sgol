using Sgol.Planning.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

/// <summary>Economic evaluation only. Never modifies operational deadlines or review states.</summary>
public static class TaskBonusCalculator
{
    public const string RulesVersion = "tasks-2026-11-02-v1";
    public static readonly DateOnly FirstWeek = new(2026, 11, 2);
    public static DateOnly Monday(DateOnly day) => day.AddDays(-((int)day.DayOfWeek + 6) % 7);
    public static decimal Points(string criticality) => criticality switch {
        "FLEXIBLE" => 1m, "IMPORTANTE" => 5m, "CRITICA" => 10m,
        _ => throw new ArgumentException("La obligación no tiene una clasificación acreditada.", nameof(criticality))
    };
    public static TaskBonusPeriod Period(DateOnly day)
    {
        var monday = Monday(day);
        return new(monday, monday.AddDays(6), OperationalSchedule.Instant(monday.AddDays(7)),
            OperationalSchedule.Instant(monday.AddDays(7), 12), monday.AddDays(12));
    }

    public static TaskBonusResult Calculate(Guid person, DateOnly week, DateTimeOffset now,
        IEnumerable<TaskBonusObligation> obligations, bool? worked)
    {
        var period = Period(week);
        if (period.StartsOn < FirstWeek)
            return new(person, period, false, "NO_VIGENTE", null, null, 0, 0, now, [], []);
        var closed = now >= period.ReviewCutoffAt;
        var at = closed ? period.ReviewCutoffAt : now;
        var details = obligations.Where(o => o.PersonId == person && Monday(o.Day) == period.StartsOn)
            .Select(o => Evaluate(o, period, at)).OrderBy(o => o.Day).ThenBy(o => o.TaskCode).ThenBy(o => o.ObligationId).ToArray();
        if (details.Select(o => o.ObligationId).Distinct().Count() != details.Length)
            throw new ArgumentException("La misma obligación no puede entrar dos veces en la base.", nameof(obligations));
        var issues = details.Where(d => d.Incidence != null).Select(d => d.TaskCode + ": " + d.Incidence).ToList();
        var basis = details.Sum(d => d.Points);
        var deducted = details.Sum(d => d.Deducted);
        if (basis == 0 && worked != false) issues.Add(worked == true
            ? "Trabajó sin tareas evaluables registradas; Dirección debe revisar la incidencia."
            : "Falta acreditar si trabajó durante este periodo sin base evaluable.");
        var state = issues.Count > 0 ? "INCIDENCIA" : basis == 0 ? "SIN_TAREAS_EVALUABLES" : "CALCULABLE";
        decimal? score = basis > 0 && issues.Count == 0 ? 100m * (basis - deducted) / basis : null;
        int? bonus = issues.Count > 0 ? null : basis == 0 ? 0 :
            100m * (basis - deducted) >= 95m * basis ? 100 : 100m * (basis - deducted) >= 85m * basis ? 50 : 0;
        var boundaries = details.SelectMany(d => new[] { OperationalSchedule.Instant(d.Day.AddDays(1)),
            OperationalSchedule.Instant(d.Day.AddDays(2)), period.ExecutionCutoffAt, period.ReviewCutoffAt }).Where(t => t > now);
        return new(person, period, closed, state, score, bonus, basis, deducted, now, details, issues.ToArray()) {
            NextChangeAt = closed ? null : boundaries.Cast<DateTimeOffset?>().Min() ?? period.ReviewCutoffAt
        };
    }

    private static TaskBonusDetail Evaluate(TaskBonusObligation o, TaskBonusPeriod period, DateTimeOffset at)
    {
        if (o.Points <= 0 && o.Incidence == null) throw new ArgumentOutOfRangeException(nameof(o), "Los puntos capturados deben ser positivos.");
        var end = OperationalSchedule.Instant(o.Day.AddDays(1));
        var recovery = new[] { OperationalSchedule.Instant(o.Day.AddDays(2)), period.ExecutionCutoffAt }.Min();
        // Events exactly at the review cutoff belong to the operational history, not to this closure.
        var events = o.Events.Where(e => e.At <= at && e.At < period.ReviewCutoffAt).OrderBy(e => e.At).ThenBy(e => e.Order).ToArray();
        DateTimeOffset? submitted = null;
        var administrativeCredit = false;
        bool returned = false, rejected = false, excluded = false, forgiven = false;
        var issue = o.Incidence;
        foreach (var e in events)
        {
            switch (e.Kind)
            {
                case "ADMIN_COMPLETION":
                case "SUBMITTED":
                    if (e.At < period.ExecutionCutoffAt) { submitted = e.At; returned = false; rejected = false; administrativeCredit = e.Kind == "ADMIN_COMPLETION"; }
                    break;
                case "RETURNED": submitted = null; returned = true; rejected = false; break;
                case "REJECTED": submitted = null; rejected = true; returned = false; break;
                case "CANCELLED":
                    // An authorized cancellation cannot silently erase an already incurred discount.
                    if (Discount(e.At, submitted, returned, rejected) == 0m && e.At < end) excluded = true;
                    break;
                case "EXCLUDED": excluded = true; break;
                case "FORGIVEN": forgiven = true; break;
            }
        }
        decimal Discount(DateTimeOffset instant, DateTimeOffset? delivery, bool hasReturn, bool hasRejection)
        {
            if (hasReturn || hasRejection) return o.Points;
            if (delivery is {} sent)
                return sent < end ? 0 : o.Flexible && sent < recovery ? o.Points / 2m : o.Points;
            if (instant < end) return 0;
            return o.Flexible && instant < recovery ? o.Points / 2m : o.Points;
        }
        var discount = excluded || forgiven ? 0 : Discount(at, submitted, returned, rejected);
        var reason = excluded ? "Exclusión autorizada: fuera de la base" : forgiven ? "Excepción de Dirección" :
            rejected ? "No satisfecha antes del cierre" : returned ? "Devuelta sin corrección enviada dentro de la semana" :
            discount == o.Points ? "Sin envío válido dentro del plazo de evaluación" : discount > 0 ?
            "Flexible: recuperación al día siguiente (50%)" : submitted != null ? administrativeCredit ? "Conclusión administrativa dentro del día evaluado" : "Envío dentro del día evaluado" : "Sin incumplimiento al calcular";
        return new(o.Id, o.AssignmentId, o.PersonId, o.TaskCode, o.Name, o.Day, o.Criticality,
            excluded ? 0 : o.Points, discount, reason, submitted, events.LastOrDefault()?.At, issue, events);
    }
}

public sealed record TaskBonusPeriod(DateOnly StartsOn, DateOnly EndsOn, DateTimeOffset ExecutionCutoffAt,
    DateTimeOffset ReviewCutoffAt, DateOnly PaymentReferenceDate);
public sealed record TaskBonusEvent(string Kind, DateTimeOffset At, long Order = 0, Guid? Actor = null, string? Reason = null);
public sealed record TaskBonusObligation(Guid Id, Guid AssignmentId, Guid PersonId, string TaskCode, string Name,
    DateOnly Day, string Criticality, decimal Points, TaskBonusEvent[] Events, string? Incidence = null)
{
    public bool Flexible => Criticality == "FLEXIBLE";
}
public sealed record TaskBonusDetail(Guid ObligationId, Guid AssignmentId, Guid PersonId, string TaskCode, string Name,
    DateOnly Day, string Criticality, decimal Points, decimal Deducted, string Reason, DateTimeOffset? SubmittedAt,
    DateTimeOffset? LastEventAt, string? Incidence, TaskBonusEvent[] Events)
{
    public string? CreditKind => SubmittedAt == null ? null : Events.LastOrDefault(e => e.At == SubmittedAt && e.Kind is "SUBMITTED" or "ADMIN_COMPLETION")?.Kind;
}
public sealed record TaskBonusResult(Guid PersonId, TaskBonusPeriod Period, bool Closed, string CalculationState,
    decimal? ScorePercent, int? BonusPercent, decimal EvaluablePoints, decimal DeductedPoints, DateTimeOffset CalculatedAt,
    TaskBonusDetail[] Details, string[] Incidences)
{
    public string ScoreDisplay => FormatScore(ScorePercent);
    public static string FormatScore(decimal? value)
    {
        if (value == null) return "—";
        var rounded = decimal.Round(value.Value, 2);
        if (value < 95 && rounded >= 95) return "<95";
        if (value < 85 && rounded >= 85) return "<85";
        return rounded.ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("es-MX"));
    }
    public string RulesVersion => TaskBonusCalculator.RulesVersion;
    public DateTimeOffset? NextChangeAt { get; init; }
    public DateTimeOffset? ClosedAt { get; init; }
    public string ResultVersion { get; init; } = "";
    public string PersonName { get; init; } = "";
}
