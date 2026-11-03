using System.Net;
using Microsoft.EntityFrameworkCore;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Planning;

public static class ProcessMembershipChecks
{
    public static async Task RunAsync(SgolDbContext db, HttpClient client, Guid id, string token, string revision)
    {
        var duplicateTask = new OperationalTask { Id = Guid.NewGuid(), ProcessId = id, Name = "Duplicada", TaskCode = "TAR-DUP" };
        var duplicate = OperationalProcessCatalogService.DiagnoseMembership(id,
            [new() { TaskId = duplicateTask.Id, Position = 1 }, new() { TaskId = duplicateTask.Id, Position = 2 }], [duplicateTask]);
        if (!duplicate.Any(c => c.Reason.Contains("más de una vez"))) throw new Exception("Missing duplicate diagnostic");
        var rulesInput = new ProcessDraftInput { Steps = [new() { TaskId = duplicateTask.Id, Position = 1, Action = "ENTREGAR", ReleaseCondition = "INICIO" }, new() { TaskId = Guid.NewGuid(), Position = 2, Action = "AUTORIZAR", ReleaseCondition = "ENTREGA" }] };
        rulesInput.Steps[1].ReturnToPosition = 2;
        rulesInput.Steps[1].MinimumStartOffsetHours = -1;
        rulesInput.Steps[1].Instructions = new string('x', 4001);
        rulesInput.Steps[0].OutputItemId = Guid.NewGuid();
        rulesInput.Steps[0].OutputTargetTaskId = Guid.NewGuid();
        var ruleIssues = OperationalProcessCatalogService.DiagnoseRules(rulesInput, true);
        foreach (var field in new[] { "ReturnToPosition", "MinimumStartOffsetHours", "Instructions", "OutputItemId", "IsFinalAudit" })
            if (!ruleIssues.Any(i => i.Field == field && i.TaskId != null)) throw new Exception("Missing located rule: " + field);
        var tasks = await db.OperationalTasks.Where(t => t.ProcessId == id).OrderBy(t => t.TaskCode).ToArrayAsync();
        var version = await db.Set<OperationalSequenceVersion>().SingleAsync(v => v.SequenceId == id && v.PublishedAt == null);
        var before = System.Text.Json.JsonSerializer.Serialize(await db.Set<OperationalSequenceStep>().AsNoTracking().Where(s => s.VersionId == version.Id).OrderBy(s => s.Position).ToArrayAsync());
        async Task<string> Reject(string expected)
        {
            var response = await client.PostAsync("/tareas/procesos?handler=Publish", new FormUrlEncodedContent(new Dictionary<string,string> {
                ["__RequestVerificationToken"] = token, ["Input.Id"] = id.ToString(), ["Input.Revision"] = revision }));
            var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
            if (response.StatusCode != HttpStatusCode.UnprocessableEntity || !html.Contains(expected)) throw new Exception("Missing membership diagnostic: " + expected);
            if (await db.Set<OperationalSequenceVersion>().AsNoTracking().AnyAsync(v => v.Id == version.Id && v.PublishedAt != null)) throw new Exception("Rejected publication changed version");
            Console.WriteLine("PASS membership HTTP: " + expected);
            return html;
        }
        tasks[0].Active = false;
        tasks[1].Active = false;
        await db.SaveChangesAsync();
        var html = await Reject("La tarea está inactiva");
        if (!html.Contains(tasks[0].TaskCode) || !html.Contains(tasks[1].TaskCode) || html.Split("process-stage--conflict").Length != 3) throw new Exception("Expected exactly two highlighted task cards");
        if (Environment.GetEnvironmentVariable("SGOL_MEMBERSHIP_UI") == "1")
        {
            File.Delete(Path.Combine(Path.GetTempPath(), "sgol-membership-ui-done"));
            Console.WriteLine($"MEMBERSHIP_UI_READY http://127.0.0.1:7258/tareas/procesos?id={id}");
            while (!File.Exists(Path.Combine(Path.GetTempPath(), "sgol-membership-ui-done"))) await Task.Delay(500);
        }
        tasks[0].Active = true; tasks[1].Active = true;
        tasks[0].Deleted = true;
        await db.SaveChangesAsync();
        await Reject("La tarea fue eliminada");
        tasks[0].Deleted = false; tasks[0].ProcessId = OperationalSequence.GeneralId;
        await db.SaveChangesAsync();
        await Reject("La tarea ya no pertenece");
        tasks[0].ProcessId = id;
        var missing = new OperationalTask { Id = Guid.CreateVersion7(), TaskCode = "TAR-9988", Name = "Tarea pendiente de añadir", ProcessId = id };
        db.Add(missing); await db.SaveChangesAsync();
        await Reject("falta en el orden de etapas");
        db.Remove(missing); await db.SaveChangesAsync();
        var firstStage = await db.Set<OperationalSequenceStep>().SingleAsync(s => s.VersionId == version.Id && s.Position == 1);
        firstStage.Action = "REVISAR";
        var secondStage = await db.Set<OperationalSequenceStep>().SingleAsync(s => s.VersionId == version.Id && s.Position == 2);
        secondStage.ReleaseCondition = "APROBACION";
        await db.SaveChangesAsync();
        html = await Reject("La primera tarea debe usar Entregar");
        if (!html.Contains("process-stage--conflict") || !html.Contains("Input.Steps[0].Action")) throw new Exception("Boundary error is not located");
        firstStage.Action = "ENTREGAR"; secondStage.ReleaseCondition = "ENTREGA";
        tasks[1].OperationalCriticality = "CRITICA";
        await db.SaveChangesAsync();
        html = await Reject("hora mínima explícita");
        if (!html.Contains("process-stage--conflict") || !html.Contains("Input.Steps[1].MinimumStartOffsetHours")) throw new Exception("Critical anchor is not located");
        tasks[1].OperationalCriticality = "FLEXIBLE";
        version.StartKind = "SEMESTRAL"; version.StartHourConfirmed = false;
        await db.SaveChangesAsync();
        html = await Reject("Indica una hora de inicio entre 0 y 23");
        if (!html.Contains("data-process-error-field=\"Input.StartHour\"") || html.Contains("class=\"process-stage process-stage--conflict")) throw new Exception("Calendar conflict incorrectly attributed to a task");
        version.StartKind = "OCASIONAL"; version.StartHourConfirmed = true;
        await db.SaveChangesAsync();
        var after = System.Text.Json.JsonSerializer.Serialize(await db.Set<OperationalSequenceStep>().AsNoTracking().Where(s => s.VersionId == version.Id).OrderBy(s => s.Position).ToArrayAsync());
        if (before != after) throw new Exception("Rejected publications changed draft stages");
        Console.WriteLine("PASS membership rejection preserves draft stages and publication state");
    }
}
