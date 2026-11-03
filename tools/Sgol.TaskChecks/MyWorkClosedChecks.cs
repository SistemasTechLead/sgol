using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Planning;

internal static class MyWorkClosedChecks
{
    public static void Run()
    {
        var now = OperationalSchedule.Instant(new(2026, 11, 3)).AddHours(12);
        var day = new DateOnly(2026, 11, 3);
        var task = new OperationalAssignmentDetails(Guid.NewGuid(), "TEST", "Tarea", "COMENTARIO", 5,
            Guid.NewGuid(), "Persona", now.AddHours(-2), now.AddHours(-1), null, 0, null, null);
        var rejected = task with { Id = Guid.NewGuid(), ReviewStatus = "NO_SATISFECHA" };
        var delivered = task with { Id = Guid.NewGuid(), CompletedAt = now };
        var scheduled = task with { Id = Guid.NewGuid(), StartsAt = now.AddHours(1), DueAt = now.AddHours(2) };
        var page = new Sgol.Web.Pages.MyWork.IndexModel(null!, null!, null!, null!, null!);
        foreach (var closed in new[] { rejected, rejected with { CompletedAt = now, CompletionKind = "SUPERVISION" }, rejected with { CompletedAt = now, CompletionKind = "ENTREGA" } })
        {
            var items = new[] { closed, delivered, scheduled, task };
            if (OperationalWorkPlan.OrderedTasks(items, day, now).Last().Id != closed.Id ||
                OperationalWorkPlan.Group(items).Single().Tasks.Last().Id != closed.Id ||
                OperationalWorkPlan.TodayTasks(items, day, now).Any(t => t.Id == closed.Id) ||
                page.OperationalColor(closed) != "closed" || page.OperationalTiming(closed) != "Cerrada · No satisfecha" ||
                !Sgol.Web.Pages.MyWork.IndexModel.OperationalSubmitted(closed))
                throw new Exception("Closed work must remain last, neutral, non-actionable and outside today's work.");
        }
        Console.WriteLine("PASS rejected work: historical/no timestamp, supervision closure and delivery; ordering, neutral color, closed label and evidence action.");
    }
}
