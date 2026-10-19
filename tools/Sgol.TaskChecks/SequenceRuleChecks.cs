using Sgol.Web.Infrastructure.Persistence.Planning;
using Sgol.Planning.Contracts;

public static class SequenceRuleChecks
{
    public static void Run()
    {
        var version = new OperationalSequenceVersion();
        Check(OperationalSequenceRules.InventoryPilot.Select(s => s.DeadlineHours).SequenceEqual([6, 1, 1, 168]), "Pilot deadlines implement Floor 6h, Subcoordination 1h, Administration 1h, Direction one week");
        Check(OperationalSequenceRules.InventoryPilot.Select(s => s.ReturnToPosition).SequenceEqual(new int?[] { null, 1, 2, 3 }), "Pilot returns descend one role at a time");
        var dates = OperationalSequenceRules.Dates(new(2026, 1, 1), new(2026, 12, 31), version).ToArray();
        Check(dates.SequenceEqual([new DateOnly(2026, 2, 23), new DateOnly(2026, 8, 24)]), "2026 inventory chooses Mondays in February and August");
        Check(OperationalSequenceRules.Dates(new(2027, 1, 1), new(2027, 12, 31), version)
            .SequenceEqual([new DateOnly(2027, 2, 22), new DateOnly(2027, 8, 23)]), "2027 inventory cadence remains every six months");
        for (var year = 2020; year <= 2040; year++)
        {
            var selected = OperationalSequenceRules.Dates(new(year, 1, 1), new(year, 12, 31), version).ToArray();
            Check(selected.Length == 2 && selected.All(d => d.Day is >= 20 and <= 25 && d.Month is 2 or 8), $"Calendar bounds in {year}");
            foreach (var date in selected)
            {
                var window = Enumerable.Range(20, 6).Select(d => new DateOnly(year, date.Month, d)).ToArray();
                Check(window.Any(d => d.DayOfWeek == DayOfWeek.Monday) ? date.DayOfWeek == DayOfWeek.Monday : date.Day == 20,
                    $"Monday preference or day-20 fallback in {year}-{date.Month}");
            }
        }
        var one = dates[0];
        Check(OperationalSequenceRules.Dates(one, one, version).Single() == one, "Partial plan includes its selected date once");
        Check(!OperationalSequenceRules.Dates(one.AddDays(1), new(2026, 7, 31), version).Any(), "Partial plan never invents a replacement inventory date");
        Check(!OperationalSequenceRules.Dates(new(9999, 1, 1), DateOnly.MaxValue, new() { StartKind = "OCASIONAL" }).Any(), "Occasional sequence has no independent calendar");
        Check(OperationalSequenceRules.Dates(new(9999, 1, 1), DateOnly.MaxValue, version).Count() == 2, "Calendar supports final DateOnly year");
        var dependency = new DateTimeOffset(2026, 2, 23, 15, 0, 0, TimeSpan.Zero);
        var minimum = dependency.AddHours(2);
        var before = OperationalSequenceRules.Enable(dependency, minimum, "RELATIVO", 1, null, true, dependency);
        Check(before.Status == "PROGRAMADA" && before.EnabledAt == null && before.DueAt == null && before.EligibleAt == minimum,
            "Dependency received before minimum does not enable or invent an effective deadline");
        var deferred = OperationalSequenceRules.Enable(dependency, minimum, "RELATIVO", 1, null, true, minimum.AddHours(8));
        Check(deferred.EnabledAt == minimum && deferred.DueAt == minimum.AddHours(1), "Late processing preserves the scheduled effective enablement");
        Check(deferred == OperationalSequenceRules.Enable(dependency, minimum, "RELATIVO", 1, null, true, minimum.AddHours(9)),
            "Repeated reconciliation does not move the effective dates");
        var lateDependency = minimum.AddHours(4);
        var late = OperationalSequenceRules.Enable(lateDependency, minimum, "RELATIVO", 6, null, true, lateDependency);
        Check(late.EnabledAt == lateDependency && late.DueAt == lateDependency.AddHours(6), "Late delivery starts a relative deadline from actual availability");
        var fixedDue = dependency.AddHours(1);
        var impossibleFuture = OperationalSequenceRules.Enable(dependency, minimum, "FIJO", 1, fixedDue, true, dependency);
        Check(impossibleFuture.Status == "PROGRAMADA" && impossibleFuture.EnabledAt == null && impossibleFuture.DueAt == null,
            "Impossible future fixed interval is identified without declaring an unexpired deadline overdue");
        var expired = OperationalSequenceRules.Enable(dependency, minimum, "FIJO", 1, fixedDue, true, minimum);
        Check(expired.Status == "FUERA_DE_PLAZO" && expired.EnabledAt == null && expired.DueAt == null, "Expired fixed limit is blocked without an invalid assignment range");
        var historical = OperationalSequenceRules.Enable(dependency, null, "FIJO", 1, fixedDue, true, fixedDue.AddHours(3));
        Check(historical.EnabledAt == dependency && historical.DueAt == fixedDue, "Timer delay does not shift a valid historical fixed interval");
        var waiting = OperationalSequenceRules.Enable(null, null, "RELATIVO", 1, null, true, dependency);
        Check(waiting.Status == "EN_ESPERA" && waiting.EligibleAt == null && waiting.DueAt == null, "Unknown dependency retains unknown effective dates");
        var missing = OperationalSequenceRules.Enable(dependency, null, "RELATIVO", 1, null, false, dependency);
        Check(missing.Status == "PENDIENTE_ASIGNACION" && missing.EnabledAt == null && missing.EligibleAt == dependency, "No eligible worker preserves the event and waits for assignment");
        var recovered = OperationalSequenceRules.Enable(dependency, null, "RELATIVO", 1, null, true, minimum, minimum);
        Check(recovered.EnabledAt == minimum && recovered.DueAt == minimum.AddHours(1), "Restored assignment receives the full relative interval");
        var recoveredLate = OperationalSequenceRules.Enable(dependency, null, "FIJO", 1, fixedDue, true, minimum, minimum);
        Check(recoveredLate.Status == "FUERA_DE_PLAZO", "Restoring an assignee cannot postpone an expired fixed limit");
        Check(OperationalSequenceRules.Enable(dependency, null, "RELATIVO", 168, null, true, dependency).DueAt == dependency.AddDays(7), "Direction deadline is one elapsed week");
        Check(!OperationalSequenceRules.Satisfies("ENTREGA", "ENTREGA", "SUPERVISION", true, true), "Supervision closure never simulates a delivery");
        Check(!OperationalSequenceRules.Satisfies("ENTREGA", "ENTREGA", "HISTORICO", true, true), "Legacy closure does not release a new sequence");
        Check(OperationalSequenceRules.Satisfies("ENTREGA", "ENTREGA", "ENTREGA", true, true), "Actual current delivery satisfies delivery dependency");
        Check(!OperationalSequenceRules.Satisfies("APROBACION", "ENTREGA", "ENTREGA", true, true), "Delivery alone cannot replace approval");
        Check(OperationalSequenceRules.Satisfies("APROBACION", "APROBACION", null, true, true), "Explicit approval satisfies approval dependency");
        Check(!OperationalSequenceRules.Satisfies("APROBACION", "APROBACION", null, false, true), "Superseded attempt cannot release the next stage");
        Check(!OperationalSequenceRules.Satisfies("APROBACION", "APROBACION", null, true, false), "Previous round cannot release the next stage");
        OperationalSequenceRules.RequireReturn("EN_CURSO", 3, 3, 2, 2, "Se encontró información de las diferencias", true);
        Check(true, "Active Administration stage may return to Subcoordination with reason");
        Reject(() => OperationalSequenceRules.RequireReturn("CONCLUIDA", 4, 4, 3, 3, "Tardío", true), "SEQUENCE_CONFLICT");
        Reject(() => OperationalSequenceRules.RequireReturn("EN_CURSO", 4, 3, 2, 2, "Tardío", true), "SEQUENCE_CONFLICT");
        Reject(() => OperationalSequenceRules.RequireReturn("EN_CURSO", 3, 3, 2, 2, "Anterior", false), "SEQUENCE_CONFLICT");
        Reject(() => OperationalSequenceRules.RequireReturn("EN_CURSO", 3, 3, 1, 2, "Salto", true), "SEQUENCE_RETURN_TARGET_INVALID");
        Reject(() => OperationalSequenceRules.RequireReturn("EN_CURSO", 3, 3, 2, 2, " ", true), "SEQUENCE_RETURN_REASON_REQUIRED");
        Reject(() => OperationalSequenceRules.Enable(dependency, null, "FIJO", 1, null, true, dependency), "SEQUENCE_DEADLINE_INVALID");
        Reject(() => OperationalSequenceRules.Enable(DateTimeOffset.MaxValue, null, "RELATIVO", 168, null, true, DateTimeOffset.MaxValue), "SEQUENCE_DEADLINE_INVALID");
        Console.WriteLine("Sequence calendar, timing, dependency and return rule checks passed.");
    }

    private static void Check(bool value, string label)
    {
        if (!value) throw new Exception("FAILED: " + label);
        Console.WriteLine("PASS: " + label);
    }
    private static void Reject(Action action, string code)
    {
        try { action(); throw new Exception("Expected " + code); }
        catch (DirectionPlanException error) when (error.Code == code) { Console.WriteLine("PASS: Rejected " + code); }
    }
}
