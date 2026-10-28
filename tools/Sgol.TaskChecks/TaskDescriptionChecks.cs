using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Planning;

internal static class TaskDescriptionChecks
{
    public static void Run()
    {
        foreach (var period in OperationalSchedule.Periods)
        foreach (var requirement in OperationalSchedule.Requirements)
        {
            var task = new OperationalTask { Name = "Descripción", PeriodType = period, Requirement = requirement };
            if (requirement == "CHECKLIST") task.ChecklistJson = System.Text.Json.JsonSerializer.Serialize(new[] { new ChecklistPoint(Guid.NewGuid(), "Comprobar") });
            foreach (var description in new string?[] { null, "", " \r\n ", " Paso 1.\r\nPaso 2.\tDetalle ", new('x', 4000) })
            {
                task.Description = description;
                task.Validate();
                var expected = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
                if (task.Description != expected) throw new Exception("Task description normalization failed.");
            }
            foreach (var description in new[] { new string('x', 4001), "Texto\0oculto" })
            {
                task.Description = description;
                try { task.Validate(); }
                catch (DirectionPlanException e) when (e.Code == "TASK_DESCRIPTION_INVALID") { continue; }
                throw new Exception("Invalid task description accepted.");
            }
        }
        Console.WriteLine("PASS Optional multiline description for all periods and evidence types, length and control validation");
    }
}
