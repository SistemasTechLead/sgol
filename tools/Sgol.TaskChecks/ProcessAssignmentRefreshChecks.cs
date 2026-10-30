using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Time;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.Evidence.Technical;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Planning;

internal static class ProcessAssignmentRefreshChecks
{
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); Console.WriteLine("PASS process refresh: " + message); }
    private static string Hidden(string html, string name) => WebUtility.HtmlDecode(Regex.Match(html, $"name=\"{name}\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);
    private static string OperationalState(OperationalAssignment row) => JsonSerializer.Serialize(typeof(OperationalAssignment).GetProperties()
        .Where(p => p.CanWrite && p.Name is not ("DefinitionSnapshotJson" or "Revision" or "Attachment"))
        .OrderBy(p => p.Name).ToDictionary(p => p.Name, p => p.GetValue(row)));

    public static async Task RunAsync(SgolDbContext db, Guid actor, Guid person, IClock clock, HttpClient client)
    {
        var direction = new EfDirectionPreparedPlanService(db, new AuditTransaction(db), clock, new Uuid7Generator(clock), new DirectionPlanningCatalogReader(db));
        var tasks = new OperationalTaskService(db, direction, clock, new FileTechnicalValidator(), new CheckScanner());
        var catalog = new OperationalProcessCatalogService(db, direction, clock, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Processes:OperationsEnabled"] = "true" }).Build());
        var refresh = new AssignmentRefreshService(db, direction, clock);
        var day = WeekContract.LocalToday(clock.UtcNow).AddDays(1);
        db.Add(new TemporalAssignmentState { Enabled = true });
        db.Add(new PersonalScheduleVersion { Id = Guid.CreateVersion7(), PersonId = person, From = day.AddDays(-1), Revision = 1,
            ChangedBy = actor, ChangedAt = clock.UtcNow, IntentKey = Guid.NewGuid(),
            DaysJson = JsonSerializer.Serialize(Enumerable.Range(0, 7).ToDictionary(d => d, d => new[] { new WorkInterval(8 * 60, 20 * 60) })) });
        await db.SaveChangesAsync();
        var name = "Registrar bajas autorizadas por defectos y conservar sus asignaciones históricas de prueba";
        await tasks.SaveAsync(actor, new() { Name = name, AssignedRole = "DIRECCION", Requirement = "NINGUNO", OperationalCriticality = "FLEXIBLE", LoadMinutes = 15, Revision = 0 }, default);
        var task = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.Name == name);
        var plan = DirectionPreparedPlan.ForOccasion(Guid.CreateVersion7(), BranchScope.LorettaId, actor, clock.UtcNow, "Operaciones mensuales · comprobación de procesos", day, day.AddDays(28));
        db.Add(plan);
        var policy = await OperationalSchedulingPolicyService.CurrentAsync(db, default);
        var rows = Enumerable.Range(0, 2).Select(i => {
            var row = new OperationalAssignment { Id = Guid.CreateVersion7(), PlanId = plan.Id, TaskId = task.Id, TaskCode = task.TaskCode,
                PersonId = person, PersonName = "Responsable de prueba", AssignedRole = task.AssignedRole };
            AssignmentDefinition.Adopt(row, task, null, false);
            OperationalAgenda.Capture(row, policy, task.OperationalCriticality, OperationalSchedule.Instant(day.AddDays(i)).AddHours(10));
            return row;
        }).ToArray();
        var delivered = AssignmentDefinition.Copy(rows[0]); delivered.Id = Guid.CreateVersion7(); delivered.CompletedAt = clock.UtcNow;
        delivered.CompletedBy = actor; delivered.CompletionKind = "ENTREGA"; delivered.ReviewStatus = "PENDIENTE_REVISION";
        db.AddRange(rows); db.Add(delivered);
        db.Add(new OperationalAttachment { AssignmentId = rows[0].Id, FileName = "evidencia.pdf", ContentType = "application/pdf", Content = [1, 2, 3] });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var original = JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().OrderBy(a => a.Id).ToArrayAsync());
        var deliveredBefore = JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().SingleAsync(a => a.Id == delivered.Id));
        var processId = await catalog.CreateAsync(actor, Guid.CreateVersion7(), "Auditoría de inventario · prueba", "", default);
        var process = (await catalog.ReadAsync(actor, processId, default)).Process;
        await catalog.AddTaskAsync(actor, processId, process.Revision, task.Id, task.Revision, default); db.ChangeTracker.Clear();
        Check(original == JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().OrderBy(a => a.Id).ToArrayAsync()), "real process editor membership change preserves existing assignments");
        task = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.Id == task.Id);
        var request = new AssignmentRefreshRequest(Guid.Empty, Guid.NewGuid(), clock.UtcNow.AddMinutes(3));
        var preview = await refresh.PreviewAsync(actor, request, default);
        Check(!preview.CanApply && preview.Decisions.Length == 2 && preview.Decisions.All(d => !d.Accepted), "one unchecked decision per pending assignment; delivered work excluded");
        Check(preview.Decisions.All(d => d.Problem!.Contains(name) && d.Problem.Contains(plan.Name) && d.Problem.Contains("General") && d.Problem.Contains("Auditoría") &&
            d.Problem.Contains("Responsable de prueba") && d.Problem.Contains(day.ToString("dd/MM/yyyy").Substring(3))), "decisions identify task, worker, plan, date and specific old/new process");
        var partial = await refresh.PreviewAsync(actor, request with { PreserveProcessAssignmentIds = [rows[0].Id] }, default);
        Check(!partial.CanApply && partial.Decisions.Count(d => d.Accepted) == 1, "accepting one assignment cannot authorize another");
        try { await refresh.ApplyAsync(actor, request, preview.Fingerprint, "Sin resolver", default); throw new Exception("Unresolved apply accepted"); }
        catch (Sgol.Planning.Contracts.DirectionPlanException e) when (e.Code == "REFRESH_STALE") { Check(true, "server rejects unresolved decisions"); }

        // Actual Razor forms, binding, CSRF, review and apply.
        var html = await client.GetStringAsync("/tareas/actualizar");
        Check(Regex.Matches(html, "type=\"checkbox\" name=\"PreserveProcessAssignmentIds\"").Count == 2 && !html.Contains("checked=\"checked\""), "real form renders separate unchecked resolution checkboxes");
        var review = new List<KeyValuePair<string, string>> { new("__RequestVerificationToken", Hidden(html, "__RequestVerificationToken")) };
        review.AddRange(rows.Select(r => new KeyValuePair<string, string>("PreserveProcessAssignmentIds", r.Id.ToString())));
        using var reviewed = await client.PostAsync("/tareas/actualizar?handler=Preview", new FormUrlEncodedContent(review));
        html = await reviewed.Content.ReadAsStringAsync();
        Check(reviewed.StatusCode == HttpStatusCode.OK && html.Contains("data-refresh-apply") && Regex.Matches(html, "checked=\"checked\"").Count == 2, "review retains each selected decision and enables final confirmation");
        Check(original == JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().OrderBy(a => a.Id).ToArrayAsync()), "GET and review do not persist decisions or modify assignments");
        var apply = new List<KeyValuePair<string, string>>();
        foreach (var field in new[] { "__RequestVerificationToken", "Key", "NotBefore", "Fingerprint" }) apply.Add(new(field, Hidden(html, field)));
        apply.Add(new("Reason", "Conservar los procesos originales de las asignaciones"));
        apply.AddRange(rows.Select(r => new KeyValuePair<string, string>("PreserveProcessAssignmentIds", r.Id.ToString())));
        using var applied = await client.PostAsync("/tareas/actualizar?handler=Apply", new FormUrlEncodedContent(apply));
        Check(applied.StatusCode == HttpStatusCode.Redirect, "real final confirmation persists the decisions"); db.ChangeTracker.Clear();
        foreach (var row in rows)
        {
            var saved = await db.OperationalAssignments.AsNoTracking().SingleAsync(a => a.Id == row.Id);
            var changedFields = typeof(OperationalAssignment).GetProperties().Where(p => p.CanWrite && p.Name is not ("DefinitionSnapshotJson" or "Revision" or "Attachment") &&
                JsonSerializer.Serialize(p.GetValue(saved)) != JsonSerializer.Serialize(p.GetValue(row))).Select(p => p.Name);
            Check(OperationalState(saved) == OperationalState(row) && AssignmentDefinition.Baseline(saved, task).ProcessId == OperationalSequence.GeneralId,
                "confirmation preserves original process, worker, workload, dates, progress and operational fields: " + string.Join(",", changedFields));
        }
        Check(deliveredBefore == JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().SingleAsync(a => a.Id == delivered.Id)), "delivered assignment remains byte-for-byte identical");
        Check((await db.OperationalAttachments.SingleAsync(a => a.AssignmentId == rows[0].Id)).Content.SequenceEqual(new byte[] { 1, 2, 3 }), "evidence survives process preservation");
        Check((await refresh.CountsAsync(actor, default)).Count == 0 && (await refresh.PreviewAsync(actor, request, default)).Decisions.Length == 0, "resolved process conflicts do not reappear in counts or new previews");
        var auditCount = await db.AuditEvents.CountAsync();
        using var replay = await client.PostAsync("/tareas/actualizar?handler=Apply", new FormUrlEncodedContent(apply));
        Check(replay.StatusCode == HttpStatusCode.Redirect && auditCount == await db.AuditEvents.CountAsync(), "replayed confirmation is idempotent");

        // Simultaneous metadata and agenda changes use the normal rules, retaining old process.
        task = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.Id == task.Id); task.Description = "Instrucción nueva"; task.LoadMinutes = 20;
        await tasks.SaveAsync(actor, task, default); db.ChangeTracker.Clear();
        request = request with { Key = Guid.NewGuid() };
        preview = await refresh.PreviewAsync(actor, request, default);
        Check(preview.CanApply && preview.Decisions.Length == 0 && preview.Differences.All(d => d.After!.LoadMinutes == 20 && AssignmentDefinition.Baseline(d.After, task).ProcessId == OperationalSequence.GeneralId),
            "later workload and metadata changes use normal scheduling and preserve acknowledged process");
        await refresh.ApplyAsync(actor, request, preview.Fingerprint, "Carga e instrucciones nuevas", default); db.ChangeTracker.Clear();
        Check((await refresh.CountsAsync(actor, default)).Count == 0, "later full adoption retains the process decision without hiding other differences");

        // Reverting an unpublished draft returns membership to General, then another process requires a new decision.
        process = (await catalog.ReadAsync(actor, processId, default)).Process;
        await catalog.AbandonAsync(actor, processId, process.Revision, default); db.ChangeTracker.Clear();
        var nextProcess = await catalog.CreateAsync(actor, Guid.CreateVersion7(), "Otro proceso de prueba", "", default);
        process = (await catalog.ReadAsync(actor, nextProcess, default)).Process;
        task = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.Id == task.Id);
        await catalog.AddTaskAsync(actor, nextProcess, process.Revision, task.Id, task.Revision, default); db.ChangeTracker.Clear();
        request = request with { Key = Guid.NewGuid(), PreserveProcessAssignmentIds = rows.Select(r => r.Id).ToArray() };
        preview = await refresh.PreviewAsync(actor, request, default);
        Check(preview.CanApply && preview.Decisions.Length == 2 && preview.Decisions.All(d => d.Problem!.Contains("Otro proceso")), "another process change generates fresh per-assignment decisions");
        var stale = preview.Fingerprint;
        var changed = await db.OperationalAssignments.SingleAsync(a => a.Id == rows[0].Id); changed.Revision++; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        try { await refresh.ApplyAsync(actor, request, stale, "Concurrente", default); throw new Exception("Stale proposal accepted"); }
        catch (Sgol.Planning.Contracts.DirectionPlanException e) when (e.Code == "REFRESH_STALE") { Check(true, "concurrent change invalidates preservation atomically"); }
        Check((await refresh.PreviewAsync(actor, request with { PreserveProcessAssignmentIds = null }, default)).Decisions.All(d => !d.Accepted), "new review requires explicitly selecting the new process change");

        // New executions follow the published process; existing obligations are not converted.
        await tasks.SaveAsync(actor, new() { Name = "Aceptar resultado de auditoría", AssignedRole = "DIRECCION", Requirement = "NINGUNO", OperationalCriticality = "FLEXIBLE", Revision = 0 }, default);
        var closing = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.Name == "Aceptar resultado de auditoría");
        process = (await catalog.ReadAsync(actor, nextProcess, default)).Process;
        await catalog.AddTaskAsync(actor, nextProcess, process.Revision, closing.Id, closing.Revision, default); db.ChangeTracker.Clear();
        var editor = await catalog.ReadAsync(actor, nextProcess, default);
        await catalog.SaveAsync(actor, new() { Id = nextProcess, Revision = editor.Process.Revision, Name = editor.Process.Name,
            StartHour = 10, Steps = editor.Steps.ToList() }, default); db.ChangeTracker.Clear();
        editor = await catalog.ReadAsync(actor, nextProcess, default);
        await catalog.PublishAsync(actor, nextProcess, editor.Process.Revision, default); db.ChangeTracker.Clear();
        editor = await catalog.ReadAsync(actor, nextProcess, default);
        await catalog.SetEnabledAsync(actor, nextProcess, editor.Process.Revision, true, default); db.ChangeTracker.Clear();
        var planning = new ProcessPlanningService(db, direction, clock, new FileTechnicalValidator(), new CheckScanner());
        var input = new ProcessPlanInput { Name = "Nueva ejecución", ProcessId = nextProcess, Mode="OCASION",OccasionTime=new(10,0), From = day.AddDays(3), Through = day.AddDays(3), People = new() { [1] = person, [2] = person } };
        var planned = await planning.PreviewAsync(actor, input, default); input.Fingerprint = planned.Fingerprint;
        var planId = await planning.PrepareAsync(actor, input, default); db.ChangeTracker.Clear();
        var runIds = await planning.ActivateAsync(actor, planId, default); db.ChangeTracker.Clear();
        var stage = await db.Set<OperationalSequenceStage>().AsNoTracking().SingleAsync(s => runIds.Contains(s.RunId) && s.TaskId == task.Id);
        Check(JsonSerializer.Deserialize<OperationalTask>(stage.DefinitionSnapshotJson!)!.ProcessId == nextProcess && stage.LoadMinutes == 20,
            "real process publication, planning and activation create the new execution in its new process");
        task = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.Id == task.Id); task.Description = "Nueva instrucción simultánea"; task.LoadMinutes = 25;
        await tasks.SaveAsync(actor, task, default); db.ChangeTracker.Clear();
        request = request with { Key = Guid.NewGuid() };
        preview = await refresh.PreviewAsync(actor, request, default);
        Check(preview.CanApply && preview.Decisions.Length == 2 && preview.Differences.Any(d => d.ProcessStage) &&
            preview.Differences.Where(d => d.After!.TaskId == task.Id).All(d => d.After!.Description == task.Description && d.After.LoadMinutes == 25),
            "simultaneous membership, workload and metadata changes update old obligations and the new process stage");
        await refresh.ApplyAsync(actor, request, preview.Fingerprint, "Conservar estructura y actualizar instrucciones", default); db.ChangeTracker.Clear();
        Check((await refresh.CountsAsync(actor, default)).Count == 0, "simultaneous changes resolve without moving old obligations into the new process");
        stage = await db.Set<OperationalSequenceStage>().AsNoTracking().SingleAsync(s => s.Id == stage.Id);

        // Moving an existing stage back to General is a distinct structural decision, too.
        var current = await db.OperationalTasks.SingleAsync(t => t.Id == task.Id); current.ProcessId = OperationalSequence.GeneralId; current.Revision++;
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        preview = await refresh.PreviewAsync(actor, request with { Key = Guid.NewGuid(), PreserveProcessAssignmentIds = null }, default);
        var stageDecision = preview.Decisions.Single(d => d.AssignmentId == stage.Id || preview.Differences.Any(x => x.ProcessStage && x.Before!.Id == d.AssignmentId));
        var stageRequest = request with { Key = Guid.NewGuid(), PreserveProcessAssignmentIds = [stageDecision.AssignmentId!.Value] };
        preview = await refresh.PreviewAsync(actor, stageRequest, default);
        Check(preview.CanApply && preview.Differences.Single().ProcessStage, "an existing process stage offers its own preservation decision when membership changes");
        await refresh.ApplyAsync(actor, stageRequest, preview.Fingerprint, "Conservar estructura de ejecución iniciada", default); db.ChangeTracker.Clear();
        var stageAfter = await db.Set<OperationalSequenceStage>().AsNoTracking().SingleAsync(s => s.Id == stage.Id);
        Check(stageAfter.RunId == stage.RunId && stageAfter.CurrentAttemptId == stage.CurrentAttemptId && stageAfter.Position == stage.Position &&
            stageAfter.PlannedStartsAt == stage.PlannedStartsAt && stageAfter.PlannedDueAt == stage.PlannedDueAt &&
            JsonSerializer.Deserialize<OperationalTask>(stageAfter.DefinitionSnapshotJson!)!.ProcessId == nextProcess,
            "preserving an existing stage retains original run, attempt, dependencies and dates");

        await tasks.SaveAsync(actor, new() { Name = "Vencida con cambio de proceso y carga", AssignedRole = "DIRECCION", Requirement = "NINGUNO", OperationalCriticality = "FLEXIBLE", LoadMinutes = 15, Revision = 0 }, default);
        var overdueTask = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.Name == "Vencida con cambio de proceso y carga");
        var overduePlan = DirectionPreparedPlan.ForOccasion(Guid.CreateVersion7(), BranchScope.LorettaId, actor, clock.UtcNow, "Plan histórico", day.AddDays(-5), day);
        db.Add(overduePlan);
        var overdue = new OperationalAssignment { Id = Guid.CreateVersion7(), PlanId = overduePlan.Id, TaskId = overdueTask.Id, TaskCode = overdueTask.TaskCode,
            PersonId = person, PersonName = "Responsable de prueba", AssignedRole = overdueTask.AssignedRole };
        AssignmentDefinition.Adopt(overdue, overdueTask, null, false);
        OperationalAgenda.Capture(overdue, policy, overdueTask.OperationalCriticality, OperationalSchedule.Instant(day.AddDays(-3)).AddHours(10));
        db.Add(overdue); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var overdueProcessId = await catalog.CreateAsync(actor, Guid.CreateVersion7(), "Proceso para pendiente vencido", "", default);
        var overdueProcess = await catalog.ReadAsync(actor, overdueProcessId, default);
        await catalog.AddTaskAsync(actor, overdueProcessId, overdueProcess.Process.Revision, overdueTask.Id, overdueTask.Revision, default); db.ChangeTracker.Clear();
        overdueTask = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.Id == overdueTask.Id); overdueTask.LoadMinutes = 20;
        await tasks.SaveAsync(actor, overdueTask, default); db.ChangeTracker.Clear();
        var overdueRequest = new AssignmentRefreshRequest(overdueTask.Id, Guid.NewGuid(), clock.UtcNow.AddMinutes(3), PreserveProcessAssignmentIds: [overdue.Id]);
        var overduePreview = await refresh.PreviewAsync(actor, overdueRequest, default);
        Check(overduePreview.CanApply && overduePreview.Differences.Single().PreservesExpiredAgenda, "overdue untouched work combines process preservation with normal historical agenda rules");
        await refresh.ApplyAsync(actor, overdueRequest, overduePreview.Fingerprint, "Conservar vencida y proceso original", default); db.ChangeTracker.Clear();
        Check(!(await refresh.CountsAsync(actor, default)).ContainsKey(overdueTask.Id) &&
            (await db.OperationalAssignments.AsNoTracking().SingleAsync(a => a.Id == overdue.Id)).StartsAt == overdue.StartsAt,
            "expired agenda optimization retains the process resolution without repeating its conflict");

        await tasks.SaveAsync(actor, new() { Name = "Asignación antigua sin instantánea", AssignedRole = "DIRECCION", Requirement = "NINGUNO", OperationalCriticality = "FLEXIBLE", Revision = 0 }, default);
        var legacyTask = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.Name == "Asignación antigua sin instantánea");
        var legacy = AssignmentDefinition.Copy(rows[1]); legacy.Id = Guid.CreateVersion7(); legacy.TaskId = legacyTask.Id; legacy.TaskCode = legacyTask.TaskCode;
        AssignmentDefinition.Adopt(legacy, legacyTask, null, false); legacy.DefinitionSnapshotJson = null; legacy.TaskRevision = legacyTask.Revision - 1;
        legacy.StartsAt = legacy.StartsAt.AddDays(7); legacy.DueAt = legacy.StartsAt.AddMinutes(OperationalAgenda.Duration(legacy.LoadMinutes, legacy.ClosureMarginPercent!.Value)); legacy.NominalStartsAt = legacy.StartsAt;
        var legacyClosed = AssignmentDefinition.Copy(legacy); legacyClosed.Id = Guid.CreateVersion7(); legacyClosed.ReviewStatus = "NO_SATISFECHA";
        db.Add(legacy); db.Add(legacyClosed); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var legacyClosedBefore = JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().SingleAsync(a => a.Id == legacyClosed.Id));
        overdueProcess = await catalog.ReadAsync(actor, overdueProcessId, default);
        await catalog.AddTaskAsync(actor, overdueProcessId, overdueProcess.Process.Revision, legacyTask.Id, legacyTask.Revision, default); db.ChangeTracker.Clear();
        Check(legacyClosedBefore == JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().SingleAsync(a => a.Id == legacyClosed.Id)), "capturing legacy process membership leaves closed historical assignments untouched");
        var legacyRequest = new AssignmentRefreshRequest(legacyTask.Id, Guid.NewGuid(), clock.UtcNow.AddMinutes(3), PreserveProcessAssignmentIds: [legacy.Id]);
        var legacyPreview = await refresh.PreviewAsync(actor, legacyRequest, default);
        Check(legacyPreview.CanApply && legacyPreview.Decisions.Length == 1 && legacyPreview.Decisions.Single().Problem!.Contains("General") && !legacyPreview.Decisions.Any(d => d.Code == "calendar"),
            "legacy membership is captured before moving the task and preserving process alone requires no calendar adoption");
        await refresh.ApplyAsync(actor, legacyRequest, legacyPreview.Fingerprint, "Conservar proceso de asignación antigua", default); db.ChangeTracker.Clear();
        Check(!(await refresh.CountsAsync(actor, default)).ContainsKey(legacyTask.Id), "legacy process preservation is durable");
        if (Environment.GetEnvironmentVariable("SGOL_REFRESH_UI_PREVIEW") == "1")
        {
            var visualId = await catalog.CreateAsync(actor, Guid.CreateVersion7(), "Auditoría de inventario · revisión visual", "", default);
            var visual = await catalog.ReadAsync(actor, visualId, default);
            task = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.Id == task.Id);
            await catalog.AddTaskAsync(actor, visualId, visual.Process.Revision, task.Id, task.Revision, default); db.ChangeTracker.Clear();
        }
    }
}
