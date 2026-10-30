using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Sgol.BuildingBlocks.Time;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.Evidence.Technical;
using Sgol.Identity.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence.Planning;

public static class ProcessCatalogDeletionChecks
{
    public static async Task RunAsync(SgolDbContext db, Guid actor, Guid person, IClock clock,
        Action<DateTimeOffset> advance, HttpClient client, IConfiguration config, Sgol.Evidence.Contracts.IFileMalwareScanner scanner)
    {
        var direction = new EfDirectionPreparedPlanService(db, new AuditTransaction(db), clock, new Uuid7Generator(clock), new DirectionPlanningCatalogReader(db));
        var catalog = new OperationalProcessCatalogService(db, direction, clock, config);
        var tasks = new OperationalTaskService(db, direction, clock, new FileTechnicalValidator(), scanner);
        var planning = new ProcessPlanningService(db, direction, clock, new FileTechnicalValidator(), scanner);
        var engine = new OperationalProcessService(db, direction, clock, new FileTechnicalValidator(), scanner);
        var id = Guid.CreateVersion7();
        await catalog.CreateAsync(actor, id, "Proceso para retirar y reemplazar tareas", "", default); db.ChangeTracker.Clear();
        for (var i = 0; i < 3; i++)
        {
            await tasks.SaveAsync(actor, new() { Name = $"Retirada etapa {i}", Revision = 0, ProcessId = id,
                Requirement = "NINGUNO", AssignedRole = CanonicalRole.Direction, LoadMinutes = 1, OperationalCriticality = "FLEXIBLE" }, default);
            db.ChangeTracker.Clear();
        }
        var editor = await catalog.ReadAsync(actor, id, default);
        var members = editor.Tasks.OrderBy(t => t.Name).ToArray();
        var input = new ProcessDraftInput { Id = id, Revision = editor.Process.Revision, Name = editor.Process.Name, StartHour = 12,
            Steps = members.Select((t, i) => new OperationalSequenceStep { TaskId = t.Id, Position = i + 1,
                Action = i == 2 ? "AUTORIZAR" : "ENTREGAR", ReleaseCondition = i == 0 ? "INICIO" : "ENTREGA", DeadlineHours = 1 }).ToList() };
        await catalog.SaveAsync(actor, input, default); db.ChangeTracker.Clear(); editor = await catalog.ReadAsync(actor, id, default);
        await catalog.PublishAsync(actor, id, editor.Process.Revision, default); db.ChangeTracker.Clear(); editor = await catalog.ReadAsync(actor, id, default);
        var publishedId = editor.Process.PublishedVersionId;
        var frozen = JsonSerializer.Serialize(await db.Set<OperationalSequenceStep>().AsNoTracking().Where(s => s.VersionId == publishedId).OrderBy(s => s.Position).ToArrayAsync());
        await catalog.SetEnabledAsync(actor, id, editor.Process.Revision, true, default); db.ChangeTracker.Clear();
        var day = WeekContract.LocalToday(clock.UtcNow);
        var planInput = new ProcessPlanInput { ProcessId = id, Name = "Reservas de proceso a retirar", Mode="OCASION",OccasionTime=new(12,0), From = day, Through = day,
            People = new() { [1] = person, [2] = person, [3] = person } };
        planInput.Fingerprint = (await planning.PreviewAsync(actor, planInput, default)).Fingerprint;
        var planId = await planning.PrepareAsync(actor, planInput, default); db.ChangeTracker.Clear();
        editor = await catalog.ReadAsync(actor, id, default);
        await Reject(() => catalog.DeleteAsync(actor, id, editor.Process.Revision, default), "PROCESS_PREPARED_PLANS_EXIST"); db.ChangeTracker.Clear();
        input.Revision = editor.Process.Revision; input.Steps.RemoveAt(1); input.Steps[1].Position = 2;
        await Reject(() => catalog.SaveAsync(actor, input, default), "PROCESS_PREPARED_PLANS_EXIST"); db.ChangeTracker.Clear();
        Check(await db.Set<PreparedProcessStart>().AnyAsync(s => s.PlanId == planId && s.Status == "PREPARADO") &&
            await db.OperationalTasks.AllAsync(t => !members.Select(m => m.Id).Contains(t.Id) || t.ProcessId == id), "Prepared plans block deletion and removal without releasing reservations or membership");
        var blockedPage = await client.GetStringAsync($"/tareas/procesos?id={id}");
        var blockedDelete = await client.PostAsync("/tareas/procesos?handler=Delete", new FormUrlEncodedContent(new Dictionary<string,string> {
            ["__RequestVerificationToken"] = Hidden(blockedPage,"__RequestVerificationToken"), ["Input.Id"] = id.ToString(),
            ["Input.Revision"] = Hidden(blockedPage,"Input.Revision"), ["confirmed"] = "true" }));
        var blockedBody = await blockedDelete.Content.ReadAsStringAsync();
        Check((int)blockedDelete.StatusCode == 409 && blockedBody.Contains(planInput.Name) && blockedBody.Contains($"/planificacion/procesos?planId={planId}"), "Conflict identifies the reserved plan and links to its review screen");
        var runId = (await planning.ActivateAsync(actor, planId, default)).Single(); db.ChangeTracker.Clear();
        var before = await History(db);
        var page = await client.GetStringAsync($"/tareas/procesos?id={id}");
        Check(page.Contains("data-process-remove") && page.Contains("Eliminar proceso"), "Actual editor exposes removal and process deletion");
        var oldRevision = Hidden(page, "Input.Revision");
        var fields = new Dictionary<string, string> { ["__RequestVerificationToken"] = Hidden(page, "__RequestVerificationToken"),
            ["Input.Id"] = id.ToString(), ["Input.Revision"] = oldRevision, ["Input.Name"] = input.Name, ["Input.Description"] = "", ["Input.StartKind"] = "OCASIONAL", ["Input.StartHour"] = "12" };
        foreach (var s in input.Steps)
        {
            var prefix = $"Input.Steps[{s.Position - 1}].";
            fields[prefix + "TaskId"] = s.TaskId.ToString(); fields[prefix + "Position"] = s.Position.ToString();
            fields[prefix + "Action"] = s.Action; fields[prefix + "ReleaseCondition"] = s.ReleaseCondition;
            fields[prefix + "DeadlineKind"] = "RELATIVO"; fields[prefix + "DeadlineHours"] = "1";
        }
        var saved = await client.PostAsync("/tareas/procesos?handler=Save", new FormUrlEncodedContent(fields));
        Check(saved.StatusCode == HttpStatusCode.Redirect, "Actual Save form removes a stage atomically: " + (saved.StatusCode == HttpStatusCode.Redirect ? "" : await saved.Content.ReadAsStringAsync())); db.ChangeTracker.Clear();
        editor = await catalog.ReadAsync(actor, id, default);
        Check(editor.Steps.Length == 2 && !editor.Process.Enabled && (await db.OperationalTasks.SingleAsync(t => t.Id == members[1].Id)).ProcessId == OperationalSequence.GeneralId,
            "Removal returns task to General and disables new starts"); db.ChangeTracker.Clear();
        Check(before == await History(db), "Removal preserves every existing execution, assignment and evidence");
        await Reject(() => catalog.SetEnabledAsync(actor, id, editor.Process.Revision, true, default), "PROCESS_MEMBERSHIP_INVALID"); db.ChangeTracker.Clear();
        await Reject(() => catalog.DeleteAsync(actor, id, long.Parse(oldRevision), default), "PROCESS_CONFLICT"); db.ChangeTracker.Clear();
        var other = Guid.CreateVersion7(); await catalog.CreateAsync(actor, other, "Otro proceso receptor", "", default); db.ChangeTracker.Clear();
        var removedTask = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.Id == members[1].Id);
        await catalog.AddTaskAsync(actor, other, 1, removedTask.Id, removedTask.Revision, default); db.ChangeTracker.Clear();
        await Reject(() => catalog.AbandonAsync(actor, id, editor.Process.Revision, default), "PROCESS_RESTORE_MEMBERSHIP_CONFLICT"); db.ChangeTracker.Clear();
        Check((await db.OperationalTasks.SingleAsync(t => t.Id == removedTask.Id)).ProcessId == other && (await catalog.ReadAsync(actor,id,default)).Process.Revision == editor.Process.Revision,
            "Abandon conflict does not steal a task or partially change the source draft"); db.ChangeTracker.Clear();
        var otherEditor = await catalog.ReadAsync(actor,other,default);
        await catalog.DeleteAsync(actor,other,otherEditor.Process.Revision,default); db.ChangeTracker.Clear();
        await catalog.AbandonAsync(actor, id, editor.Process.Revision, default); db.ChangeTracker.Clear();
        editor = await catalog.ReadAsync(actor, id, default);
        Check(editor.Steps.Length == 3 && (await db.OperationalTasks.SingleAsync(t => t.Id == members[1].Id)).ProcessId == id, "Abandon restores removed published task from General"); db.ChangeTracker.Clear();
        // Remove again, replace with a new task, and publish a coherent new version.
        input.Revision = editor.Process.Revision;
        await catalog.SaveAsync(actor, input, default); db.ChangeTracker.Clear();
        await tasks.SaveAsync(actor, new() { Name = "Reemplazo independiente", Revision = 0, Requirement = "NINGUNO", AssignedRole = CanonicalRole.Direction,
            LoadMinutes = 1, OperationalCriticality = "FLEXIBLE" }, default); db.ChangeTracker.Clear();
        var replacement = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.Name == "Reemplazo independiente");
        editor = await catalog.ReadAsync(actor, id, default);
        await catalog.AddTaskAsync(actor, id, editor.Process.Revision, replacement.Id, replacement.Revision, default); db.ChangeTracker.Clear();
        editor = await catalog.ReadAsync(actor, id, default);
        await catalog.PublishAsync(actor, id, editor.Process.Revision, default); db.ChangeTracker.Clear();
        Check(frozen == JsonSerializer.Serialize(await db.Set<OperationalSequenceStep>().AsNoTracking().Where(s => s.VersionId == publishedId).OrderBy(s => s.Position).ToArrayAsync()), "Replacement publication preserves old frozen rules");
        page = await client.GetStringAsync($"/tareas/procesos?id={id}");
        var deletion = new Dictionary<string, string> { ["Input.Id"] = id.ToString(), ["Input.Revision"] = Hidden(page, "Input.Revision"), ["confirmed"] = "true" };
        var rejected = await client.PostAsync("/tareas/procesos?handler=Delete", new FormUrlEncodedContent(deletion));
        Check(rejected.StatusCode == HttpStatusCode.BadRequest, "Delete rejects missing CSRF token");
        deletion["__RequestVerificationToken"] = Hidden(page, "__RequestVerificationToken"); deletion["confirmed"] = "false";
        rejected = await client.PostAsync("/tareas/procesos?handler=Delete", new FormUrlEncodedContent(deletion));
        Check((int)rejected.StatusCode == 422, "Delete requires explicit confirmation on server");
        deletion["confirmed"] = "true";
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE audit_event ADD CONSTRAINT test_catalog_delete_audit_failure CHECK (action <> 'PROCESS_DELETED') NOT VALID");
        try
        {
            var current = await catalog.ReadAsync(actor,id,default);
            try { await catalog.DeleteAsync(actor,id,current.Process.Revision,default); throw new Exception("Expected audit failure"); }
            catch (DbUpdateException) { }
            db.ChangeTracker.Clear();
            Check((await catalog.ReadAsync(actor,id,default)).Process.Revision == current.Process.Revision &&
                await db.OperationalTasks.AnyAsync(t=>t.ProcessId==id), "Audit failure rolls back deletion and task membership atomically");
        }
        finally { await db.Database.ExecuteSqlRawAsync("ALTER TABLE audit_event DROP CONSTRAINT test_catalog_delete_audit_failure"); db.ChangeTracker.Clear(); }
        var deleted = await client.PostAsync("/tareas/procesos?handler=Delete", new FormUrlEncodedContent(deletion));
        Check(deleted.StatusCode == HttpStatusCode.Redirect && !deleted.Headers.Location!.OriginalString.Contains("id="), "Confirmed Delete returns to catalogue"); db.ChangeTracker.Clear();
        var retryDelete = await client.PostAsync("/tareas/procesos?handler=Delete", new FormUrlEncodedContent(deletion));
        Check(retryDelete.StatusCode == HttpStatusCode.NotFound && (await retryDelete.Content.ReadAsStringAsync()).Contains("El proceso ya no"), "Deletion retry shows a handled not-found result instead of failing to reload the removed editor");
        Check(!(await catalog.ListAsync(actor, default)).Any(p => p.Id == id) && !await db.OperationalTasks.AnyAsync(t => t.ProcessId == id), "Deleted process is hidden and all tasks return to General");
        Check(before == await History(db), "Deleting published process preserves all current and historical operational data");
        await Reject(() => catalog.ReadAsync(actor, id, default), "PROCESS_NOT_FOUND"); db.ChangeTracker.Clear();
        await Reject(() => catalog.DeleteAsync(actor, OperationalSequence.GeneralId, 1, default), "PROCESS_GENERAL_RESERVED"); db.ChangeTracker.Clear();
        await Reject(() => catalog.DeleteAsync(Guid.Empty, id, 1, default), "ACCESO_DENEGADO"); db.ChangeTracker.Clear();
        // Existing active execution must finish using the old three stages, including the removed one.
        for (var position = 1; position <= 3; position++)
        {
            var view = await engine.ReadAsync(actor, runId, default); var stage = view.Stages.Single(s => s.Position == position);
            if (stage.Status != "DISPONIBLE") { advance(new[] { clock.UtcNow, stage.PlannedStartsAt }.Max()); await engine.TickAsync(default); db.ChangeTracker.Clear(); view = await engine.ReadAsync(actor, runId, default); stage = view.Stages.Single(s => s.Position == position); }
            var attempt = view.Attempts.Single(a => a.Id == stage.CurrentAttemptId);
            await engine.DecideAsync(actor, runId, attempt.Id, attempt.Revision, Guid.CreateVersion7(), position == 3 ? "SIN_DIFERENCIAS" : "ENTREGA", "Continuación tras eliminación", null, null, true, default); db.ChangeTracker.Clear();
        }
        Check((await engine.ReadAsync(actor, runId, default)).Run.Status == "CONCLUIDA", "Execution started before removal and deletion completes with its original stages");
        Check(await db.AuditEvents.AnyAsync(e => e.Action == "PROCESS_DELETED" && e.ResourceId == id), "Deletion is audited");
        Check(!db.Database.HasPendingModelChanges(), "Deletion migration matches EF model");
        replacement = await db.OperationalTasks.AsNoTracking().SingleAsync(t=>t.Id==replacement.Id);
        await tasks.ChangeAsync(actor,replacement.Id,replacement.Revision,false,default); db.ChangeTracker.Clear();
        Check(!await db.OperationalTasks.Where(t=>t.Id==replacement.Id).Select(t=>t.Active).SingleAsync(), "Historical process references allow administering a task returned to General");
        var empty = Guid.CreateVersion7(); await catalog.CreateAsync(actor,empty,"Borrador con tarea sin configurar","",default); db.ChangeTracker.Clear();
        await tasks.SaveAsync(actor,new() { Name="Pendiente de añadir como etapa",Revision=0,ProcessId=empty,Requirement="NINGUNO",AssignedRole=CanonicalRole.Direction,OperationalCriticality="FLEXIBLE" },default); db.ChangeTracker.Clear();
        var emptyEditor = await catalog.ReadAsync(actor,empty,default);
        await catalog.SaveAsync(actor,new() { Id=empty,Revision=emptyEditor.Process.Revision,Name=emptyEditor.Process.Name },default); db.ChangeTracker.Clear();
        Check(await db.OperationalTasks.AnyAsync(t=>t.ProcessId==empty), "Saving does not remove a task still waiting to be configured as a stage");
        emptyEditor = await catalog.ReadAsync(actor,empty,default);
        await catalog.DeleteAsync(actor,empty,emptyEditor.Process.Revision,default); db.ChangeTracker.Clear();
        Check(!await db.OperationalTasks.AnyAsync(t=>t.ProcessId==empty), "Deleting an unpublished draft returns even unconfigured members to General");
        if (Environment.GetEnvironmentVariable("SGOL_DELETION_UI_PREVIEW") == "1")
        {
            var previewId = Guid.CreateVersion7(); await catalog.CreateAsync(actor, previewId, "Proceso para verificar eliminación en pantalla", "", default); db.ChangeTracker.Clear();
            await tasks.SaveAsync(actor, new() { Name = "Una tarea con nombre largo para comprobar la lectura y los controles en móvil", Revision = 0, ProcessId = previewId, Requirement = "NINGUNO", AssignedRole = CanonicalRole.Direction, OperationalCriticality = "FLEXIBLE" }, default);
            Console.WriteLine($"DELETION_UI_READY https://localhost:7259/tareas/procesos?id={previewId}");
        }
    }
    private static async Task<string> History(SgolDbContext db) => JsonSerializer.Serialize(new {
        Assignments = await db.OperationalAssignments.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(),
        Runs = await db.Set<OperationalSequenceRun>().AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(),
        Stages = await db.Set<OperationalSequenceStage>().AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(),
        Attempts = await db.Set<OperationalSequenceAttempt>().AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(),
        Documents = await db.Set<OperationalSequenceDocument>().AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(),
        Attachments = await db.Set<OperationalAttachment>().AsNoTracking().OrderBy(x => x.AssignmentId).ToArrayAsync(),
        Photographs = await db.OperationalPhotographs.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync()
    });
    private static string Hidden(string html, string name) => WebUtility.HtmlDecode(Regex.Match(html, $"name=\"{Regex.Escape(name)}\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS: " + message); }
    private static async Task Reject(Func<Task> action, string code) { try { await action(); throw new Exception("Expected " + code); } catch (DirectionPlanException e) when (e.Code == code) { Console.WriteLine("PASS: Rejected " + code); } }
}
