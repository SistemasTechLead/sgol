using Sgol.Web.Infrastructure.Persistence.Planning;
using Sgol.Web.Pages.Validations;

public static class CapacityRuleChecks
{
    public static void Run()
    {
        var person = Guid.CreateVersion7();
        var day = new DateOnly(2026, 10, 14);
        var task = new OperationalAssignment { Id = Guid.CreateVersion7(), PersonId = person,
            StartsAt = OperationalSchedule.Instant(day), DueAt = OperationalSchedule.Instant(day.AddDays(1)),
            LoadMinutes = 200 };
        CapacityWorker[] workers = [new(person, "Persona", "P", "PISO_VENTAS", null, null)];
        var ledger = new CapacityLedger(workers, [], [], [(person, day.AddDays(1))], [], day);
        Check(ledger.DayCapacity(person, day) == 240, "Normal day provides 240 minutes");
        Check(ledger.CanFit(person, task), "A 200-minute task now fits");
        task.LoadMinutes = 241;
        Check(!ledger.CanFit(person, task), "A task above 240 minutes is still blocked");
        Check(ledger.DayCapacity(person, day.AddDays(1)) == 0, "Unavailable day stays at zero");
        ledger.Override = new(person, day, day, 50, 240, "Media capacidad");
        Check(ledger.DayCapacity(person, day) == 120 && ledger.DayCapacity(person, day.AddDays(2)) == 240,
            "Percentage reduction expires back to 240");
        var saved = new CapacityLedger(workers, [], [new() { PersonId = person, From = day, Through = day, DailyMinutes = 100, Percent = 75 }], [], [], day);
        Check(saved.DayCapacity(person, day) == 75, "Explicit individual setting is preserved");
        Check(new OperationalCapacity().DailyMinutes == 240 && new CapacityForm().DailyMinutes == 240,
            "New settings and supervision forms start at 240");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        Console.WriteLine($"PASS: {message}");
    }
}
