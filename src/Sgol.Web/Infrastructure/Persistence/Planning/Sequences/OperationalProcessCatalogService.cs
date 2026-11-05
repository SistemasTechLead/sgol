using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Time;
using Sgol.Planning.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed class ProcessDraftInput
{
    public Guid Id { get; set; }
    public long Revision { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public ProcessStartSchedule Schedule { get; set; } = new();
    public string StartKind { get; set; } = "OCASIONAL";
    public int FirstMonth { get; set; } = 2;
    public int WindowFromDay { get; set; } = 20;
    public int WindowThroughDay { get; set; } = 25;
    public int PreferredWeekDay { get; set; } = 1;
    public int? StartHour { get; set; }
    public List<OperationalSequenceStep> Steps { get; set; } = [];
}

public sealed record ProcessEditor(OperationalSequence Process, OperationalSequenceVersion? Draft,
    OperationalSequenceStep[] Steps, OperationalTask[] Tasks)
{
    public OperationalSequenceVersion? Configuration { get; init; }
}

// Catalog writes share the assignment/capacity lock. Membership and draft changes
// commit together; a published configuration is never edited in place.
public sealed partial class OperationalProcessCatalogService(SgolDbContext db, IDirectionPreparedPlanService direction,
    IClock clock, IConfiguration configuration)
{
    public Task<ProcessItemEntry[]> ItemsAsync(Guid actor, CancellationToken ct) => new ProcessItemCatalogService(db, direction, clock).ListAsync(actor, ct);
    public Task SaveItemAsync(Guid actor, Guid id, long revision, string name, int minutes, bool active, CancellationToken ct) => new ProcessItemCatalogService(db, direction, clock).SaveAsync(actor, id, revision, name, minutes, active, ct);
    public async Task<OperationalSequence[]> ListAsync(Guid actor, CancellationToken token)
    {
        await TaskCatalogAuthorization.RequireReadAsync(db, actor, clock.UtcNow, token);
        return await db.Set<OperationalSequence>().AsNoTracking().Where(p => p.DeletedAt == null).OrderByDescending(p => p.IsGeneral).ThenBy(p => p.Name).ToArrayAsync(token);
    }

    public async Task<ProcessEditor> ReadAsync(Guid actor, Guid id, CancellationToken token)
    {
        await TaskCatalogAuthorization.RequireReadAsync(db, actor, clock.UtcNow, token);
        var root = await db.Set<OperationalSequence>().AsNoTracking().SingleOrDefaultAsync(p => p.Id == id && p.DeletedAt == null, token)
            ?? throw Error("PROCESS_NOT_FOUND", 404);
        var draft = await db.Set<OperationalSequenceVersion>().AsNoTracking().SingleOrDefaultAsync(v => v.SequenceId == id && v.PublishedAt == null, token);
        var version = draft?.Id ?? root.PublishedVersionId;
        var steps = version == null ? [] : await db.Set<OperationalSequenceStep>().AsNoTracking().Where(s => s.VersionId == version).OrderBy(s => s.Position).ToArrayAsync(token);
        var tasks = await db.OperationalTasks.AsNoTracking().Where(t => t.ProcessId == id && !t.Deleted).OrderBy(t => t.Name).ToArrayAsync(token);
        ProcessCountSources.ConvertLegacy(steps);
        return new(root, draft, steps, tasks) { Configuration = draft ?? (root.PublishedVersionId == null ? null :
            await db.Set<OperationalSequenceVersion>().AsNoTracking().SingleAsync(v => v.Id == root.PublishedVersionId, token)) };
    }

    public async Task<Guid> CreateAsync(Guid actor, Guid key, string name, string description, CancellationToken token)
    {
        description ??= "";
        ValidateIdentity(name, description);
        if (key == Guid.Empty || key == OperationalSequence.GeneralId) throw Error("PROCESS_INVALID");
        await direction.AuthorizeAsync(actor, token);
        await using var tx = await db.Database.BeginTransactionAsync(token);
        await OperationalTaskService.LockAsync(db, token); await direction.AuthorizeAsync(actor, token);
        var existing = await db.Set<OperationalSequence>().SingleOrDefaultAsync(p => p.Id == key, token);
        if (existing != null)
        {
            if (existing.DeletedAt != null) throw Error("PROCESS_NOT_FOUND", 404);
            var first = await db.Set<OperationalSequenceVersion>().SingleOrDefaultAsync(v => v.SequenceId == key && v.Number == 1, token);
            if (first?.CreatedBy != actor || existing.Name != name.Trim() || existing.Description != description.Trim()) throw Error("PROCESS_CONFLICT", 409);
            await tx.CommitAsync(token); return key;
        }
        db.Add(new OperationalSequence { Id = key, Name = name.Trim(), Description = description.Trim() });
        db.Add(new OperationalSequenceVersion { Id = Guid.CreateVersion7(), SequenceId = key, Number = 1,
            Name = name.Trim(), Description = description.Trim(), StartKind = "OCASIONAL", CreatedBy = actor, CreatedAt = clock.UtcNow });
        Audit(actor, "PROCESS_CREATED", key); await db.SaveChangesAsync(token); await tx.CommitAsync(token); return key;
    }
    public async Task<ProcessEditor> ReadPublishedAsync(Guid actor,Guid id,CancellationToken token)
    {
        await TaskCatalogAuthorization.RequireReadAsync(db,actor,clock.UtcNow,token);
        var root=await db.Set<OperationalSequence>().AsNoTracking().SingleOrDefaultAsync(p=>p.Id==id && p.DeletedAt==null && !p.IsGeneral && p.PublishedVersionId!=null,token) ?? throw Error("PROCESS_NOT_FOUND",404);
        var version=await db.Set<OperationalSequenceVersion>().AsNoTracking().SingleAsync(v=>v.Id==root.PublishedVersionId && v.PublishedAt!=null,token);
        var steps=await db.Set<OperationalSequenceStep>().AsNoTracking().Where(s=>s.VersionId==version.Id).OrderBy(s=>s.Position).ToArrayAsync(token);
        root.Name=version.Name; root.Description=version.Description;
        return new(root,null,steps,steps.Select(s=>JsonSerializer.Deserialize<OperationalTask>(s.TaskSnapshot!)!).ToArray()) { Configuration=version };
    }

    public async Task SaveAsync(Guid actor, ProcessDraftInput input, CancellationToken token)
    {
        input.Description ??= "";
        foreach (var step in input.Steps) { step.Instructions ??= ""; step.OutputTargetTaskId = null; }
        ValidateIdentity(input.Name, input.Description); ValidateRules(input, false);
        await direction.AuthorizeAsync(actor, token);
        await using var tx = await db.Database.BeginTransactionAsync(token);
        await OperationalTaskService.LockAsync(db, token); await direction.AuthorizeAsync(actor, token);
        var root = await RequireRoot(input.Id, input.Revision, token);
        var members = await db.OperationalTasks.Where(t => t.ProcessId == root.Id && !t.Deleted).ToArrayAsync(token);
        if (input.Steps.Any(s => members.All(t => t.Id != s.TaskId))) throw LocatedError("PROCESS_MEMBERSHIP_INVALID", input.Steps.Where(s => members.All(t => t.Id != s.TaskId)).Select(s => s.TaskId));
        var draft = await EnsureDraft(root, actor, token);
        var previousTaskIds = await db.Set<OperationalSequenceStep>().Where(s => s.VersionId == draft.Id).Select(s => s.TaskId).ToArrayAsync(token);
        // A task just created in this process may still be waiting in the picker.
        // Only omission of an existing stage represents an explicit removal.
        var removed = members.Where(t => previousTaskIds.Contains(t.Id) && input.Steps.All(s => s.TaskId != t.Id)).ToArray();
        if (removed.Length > 0) await RequireNoPreparedPlans(root.Id, token);
        await ValidateItems(input.Steps.ToArray(), draft.Id, token);
        foreach (var task in removed) await ReturnToGeneral(task, token);
        if (removed.Length > 0)
        {
            root.Enabled = false;
            Audit(actor, "PROCESS_TASKS_REMOVED", root.Id, new { TaskIds = removed.Select(t => t.Id).ToArray() });
        }
        // Delete only mutable draft rows, before reinserting possibly reordered keys.
        await db.Set<OperationalSequenceStep>().Where(s => s.VersionId == draft.Id).ExecuteDeleteAsync(token);
        DetachDraftSteps(draft.Id);
        draft.Name = input.Name.Trim(); draft.Description = input.Description.Trim();
        draft.StartScheduleJson = input.Schedule.Capture(input.StartKind);
        draft.StartKind = input.StartKind; draft.FirstMonth = input.FirstMonth; draft.WindowFromDay = input.WindowFromDay;
        draft.WindowThroughDay = input.WindowThroughDay; draft.PreferredWeekDay = input.PreferredWeekDay;
        draft.StartHour = input.StartHour ?? 0; draft.StartHourConfirmed = input.StartHour != null; draft.Revision++;
        root.Name = draft.Name; root.Description = draft.Description; root.Revision++;
        foreach (var step in input.Steps.OrderBy(s => s.Position)) db.Add(CopyStep(step, draft.Id));
        Audit(actor, "PROCESS_DRAFT_SAVED", root.Id);
        await db.SaveChangesAsync(token); await tx.CommitAsync(token);
    }

    public async Task AddTaskAsync(Guid actor, Guid id, long revision, Guid taskId, long taskRevision, CancellationToken token)
    {
        await direction.AuthorizeAsync(actor, token);
        await using var tx = await db.Database.BeginTransactionAsync(token);
        await OperationalTaskService.LockAsync(db, token); await direction.AuthorizeAsync(actor, token);
        var root = await RequireRoot(id, revision, token);
        var task = await db.OperationalTasks.SingleOrDefaultAsync(t => t.Id == taskId && !t.Deleted, token) ?? throw Error("TASK_NOT_FOUND", 404);
        if (task.Revision != taskRevision) throw LocatedError("TASK_CONFLICT", [taskId], status: 409);
        if (task.ProcessId != OperationalSequence.GeneralId && task.ProcessId != id) throw LocatedError("PROCESS_TRANSFER_REQUIRES_PUBLICATION", [taskId], status: 409);
        var draft = await EnsureDraft(root, actor, token);
        if (await db.Set<OperationalSequenceStep>().AnyAsync(s => s.VersionId == draft.Id && s.TaskId == taskId, token)) { await tx.CommitAsync(token); return; }
        var previous = await db.Set<OperationalSequenceStep>().Where(s => s.VersionId == draft.Id)
            .OrderByDescending(s => s.Position).FirstOrDefaultAsync(token);
        var position = (previous?.Position ?? 0) + 1;
        if (position > 32) throw Error("PROCESS_INVALID");
        if (task.IsWeeklyOperationsTask && id != OperationalSequence.GeneralId) throw LocatedError("WEEKLY_OPERATIONS_TASK_INVALID", [taskId]);
        // Move the default acceptance to the new final stage, preserving custom actions.
        if (previous?.Action == "AUTORIZAR") previous.Action = "ENTREGAR";
        // Preserve the known membership before changing the catalogue, including legacy assignments.
        foreach (var assignment in (await db.OperationalAssignments.Where(a => a.TaskId == task.Id && a.DefinitionSnapshotJson == null && a.CompletedAt == null).ToArrayAsync(token)).Where(AssignmentDefinition.Pending))
        {
            var snapshot = JsonSerializer.SerializeToNode(AssignmentDefinition.Baseline(assignment, task))!;
            snapshot["_CalendarKnown"] = assignment.TaskRevision == task.Revision;
            assignment.DefinitionSnapshotJson = snapshot.ToJsonString();
        }
        task.ProcessId = id; task.Revision++; root.Revision++;
        db.Add(new OperationalSequenceStep { VersionId = draft.Id, Position = position, TaskId = task.Id,
            Action = "AUTORIZAR", ReleaseCondition = previous == null ? "INICIO" :
                previous.Action == "ENTREGAR" ? "ENTREGA" : "APROBACION", DeadlineHours = task.DeadlineHours });
        Audit(actor, "PROCESS_TASK_ADDED", id, new { task.Id, task.Revision, IndependentGenerationSuspended = true });
        await db.SaveChangesAsync(token); await tx.CommitAsync(token);
    }

    public async Task PublishAsync(Guid actor, Guid id, long revision, CancellationToken token)
    {
        await direction.AuthorizeAsync(actor, token);
        await using var tx = await db.Database.BeginTransactionAsync(token);
        await OperationalTaskService.LockAsync(db, token); await direction.AuthorizeAsync(actor, token);
        var root = await RequireRoot(id, revision, token);
        var draft = await db.Set<OperationalSequenceVersion>().SingleOrDefaultAsync(v => v.SequenceId == id && v.PublishedAt == null, token) ?? throw Error("PROCESS_DRAFT_REQUIRED");
        var steps = await db.Set<OperationalSequenceStep>().Where(s => s.VersionId == draft.Id).OrderBy(s => s.Position).ToArrayAsync(token);
        var members = await db.OperationalTasks.Where(t => t.ProcessId == id && !t.Deleted).ToArrayAsync(token);
        await RequireValidMembership(id, steps, members, token);
        await ValidateItems(steps, draft.Id, token);
        ProcessCountSources.ConvertLegacy(steps);
        ValidateRules(new() { Schedule = ProcessStartSchedule.Read(draft.StartScheduleJson), StartKind = draft.StartKind, FirstMonth = draft.FirstMonth, WindowFromDay = draft.WindowFromDay,
            WindowThroughDay = draft.WindowThroughDay, PreferredWeekDay = draft.PreferredWeekDay,
            StartHour = draft.StartHourConfirmed ? draft.StartHour : null, Steps = steps.ToList() }, true);
        if (members.Any(t => t.Requirement != "DOCUMENTO") && steps.Any(s => s.Action == "SELLAR"))
            throw LocatedError("PROCESS_SEAL_REQUIRES_DOCUMENT_FLOW", steps.Where(s => s.Action == "SELLAR").Select(s => s.TaskId).Concat(members.Where(t => t.Requirement != "DOCUMENTO").Select(t => t.Id)));
        var specialties = await db.Set<Sgol.Web.Infrastructure.Persistence.Organization.Specialization>().Where(s => s.Active).Select(s => s.Id).ToArrayAsync(token);
        if (members.Any(t => t.SpecializationId != null && !specialties.Contains(t.SpecializationId.Value))) throw LocatedError("TASK_SPECIALIZATION_INVALID", members.Where(t => t.SpecializationId != null && !specialties.Contains(t.SpecializationId.Value)).Select(t => t.Id));
        foreach (var s in steps) { var task = members.Single(t => t.Id == s.TaskId);
            try { task.Validate(); } catch (DirectionPlanException e) { e.Data["ProcessIssues"] = new[] { new ProcessValidationIssue(task.Id, "", "") }; throw; }
            if (task.ScheduleMode == "PROGRESO") throw LocatedError("PROCESS_PROGRESS_SCHEDULING_UNSUPPORTED", [task.Id], "");
            if (!OperationalAgenda.IsDefined(task.OperationalCriticality)) throw LocatedError("SCHEDULING_CRITICALITY_REQUIRED", [task.Id], "");
            if (s.Position > 1 && task.OperationalCriticality == "CRITICA" && s.MinimumStartOffsetHours == null) throw LocatedError("PROCESS_CRITICAL_ANCHOR_REQUIRED", [task.Id], "MinimumStartOffsetHours");
            s.TaskSnapshot = JsonSerializer.Serialize(task); }
        draft.AgendaVersion = 1;
        draft.CountSemanticsVersion = ProcessCountSources.CurrentVersion;
        foreach (var step in steps) step.OutputTargetTaskId = null;
        // Save snapshots while the version is still mutable, then freeze it atomically.
        await db.SaveChangesAsync(token);
        draft.PublishedAt = clock.UtcNow; draft.PublishedBy = actor; draft.Revision++;
        root.PublishedVersionId = draft.Id; root.Enabled = false; root.Revision++;
        Audit(actor, "PROCESS_PUBLISHED", id, new { VersionId = draft.Id, draft.Number });
        await db.SaveChangesAsync(token); await tx.CommitAsync(token);
    }

    public async Task SetEnabledAsync(Guid actor, Guid id, long revision, bool enabled, CancellationToken token)
    {
        await direction.AuthorizeAsync(actor, token);
        await using var tx = await db.Database.BeginTransactionAsync(token);
        await OperationalTaskService.LockAsync(db, token); await direction.AuthorizeAsync(actor, token);
        var root = await RequireRoot(id, revision, token);
        if (enabled && (!configuration.GetValue<bool>("Processes:OperationsEnabled") || root.PublishedVersionId == null)) throw Error("PROCESS_OPERATION_NOT_READY", 409);
        if (enabled)
        {
            var publishedSteps = await db.Set<OperationalSequenceStep>().Where(s => s.VersionId == root.PublishedVersionId).ToArrayAsync(token);
            if (publishedSteps.Length == 0) throw Error("PROCESS_MEMBERSHIP_INVALID");
            await RequireValidMembership(id, publishedSteps, [], token);
        }
        root.Enabled = enabled; root.Revision++; Audit(actor, enabled ? "PROCESS_ACTIVATED" : "PROCESS_DEACTIVATED", id);
        if (enabled) await CreateAutomaticCalendarAsync(actor, root, token);
        await db.SaveChangesAsync(token); await tx.CommitAsync(token);
    }

    public async Task AbandonAsync(Guid actor, Guid id, long revision, CancellationToken token)
    {
        await direction.AuthorizeAsync(actor, token);
        await using var tx = await db.Database.BeginTransactionAsync(token);
        await OperationalTaskService.LockAsync(db, token); await direction.AuthorizeAsync(actor, token);
        var root = await RequireRoot(id, revision, token);
        var draft = await db.Set<OperationalSequenceVersion>().SingleOrDefaultAsync(v => v.SequenceId == id && v.PublishedAt == null, token) ?? throw Error("PROCESS_DRAFT_REQUIRED");
        var committed = root.PublishedVersionId == null ? [] : await db.Set<OperationalSequenceStep>().Where(s => s.VersionId == root.PublishedVersionId).Select(s => s.TaskId).ToArrayAsync(token);
        var restored = await db.OperationalTasks.Where(t => committed.Contains(t.Id) && t.ProcessId != id).ToArrayAsync(token);
        if (restored.Any(t => t.ProcessId != OperationalSequence.GeneralId || t.Deleted || !t.Active))
            throw LocatedError("PROCESS_RESTORE_MEMBERSHIP_CONFLICT", restored.Where(t => t.ProcessId != OperationalSequence.GeneralId || t.Deleted || !t.Active).Select(t => t.Id), status: 409);
        foreach (var task in restored) { await PreservePendingDefinition(task, token); task.ProcessId = id; task.Revision++; }
        var returned = await db.OperationalTasks.Where(t => t.ProcessId == id && !committed.Contains(t.Id)).ToArrayAsync(token);
        foreach (var task in returned) await ReturnToGeneral(task, token);
        await db.Set<OperationalSequenceStep>().Where(s => s.VersionId == draft.Id).ExecuteDeleteAsync(token);
        DetachDraftSteps(draft.Id);
        // Preserve the draft identity and its audit; it becomes an empty, unpublished draft.
        foreach (var step in root.PublishedVersionId == null ? [] : await db.Set<OperationalSequenceStep>().AsNoTracking().Where(s => s.VersionId == root.PublishedVersionId).ToArrayAsync(token))
            db.Add(CopyStep(step, draft.Id));
        root.Revision++; draft.Revision++;
        Audit(actor, "PROCESS_DRAFT_ABANDONED", id, new { ReturnedTaskIds = returned.Select(t => t.Id).ToArray() });
        await db.SaveChangesAsync(token); await tx.CommitAsync(token);
    }

    internal static async Task RequireMembershipChangeAsync(SgolDbContext db, OperationalTask? task, Guid target, CancellationToken token)
    {
        if (!await db.Set<OperationalSequence>().AnyAsync(p => p.Id == target && p.DeletedAt == null, token)) throw Error("PROCESS_NOT_FOUND", 404);
        if (task == null || task.ProcessId == target) return;
        if (task.ProcessId != OperationalSequence.GeneralId || target != OperationalSequence.GeneralId)
            throw Error("PROCESS_USE_EDITOR", 409);
    }

    private async Task<OperationalSequence> RequireRoot(Guid id, long revision, CancellationToken token)
    {
        var root = await db.Set<OperationalSequence>().SingleOrDefaultAsync(p => p.Id == id && p.DeletedAt == null, token) ?? throw Error("PROCESS_NOT_FOUND", 404);
        if (root.IsGeneral) throw Error("PROCESS_GENERAL_RESERVED", 409);
        if (root.Revision != revision) throw Error("PROCESS_CONFLICT", 409);
        return root;
    }

    private async Task<OperationalSequenceVersion> EnsureDraft(OperationalSequence root, Guid actor, CancellationToken token)
    {
        var draft = await db.Set<OperationalSequenceVersion>().SingleOrDefaultAsync(v => v.SequenceId == root.Id && v.PublishedAt == null, token);
        if (draft != null) return draft;
        var published = root.PublishedVersionId == null ? null : await db.Set<OperationalSequenceVersion>().AsNoTracking().SingleAsync(v => v.Id == root.PublishedVersionId, token);
        draft = new() { Id = Guid.CreateVersion7(), SequenceId = root.Id,
            Number = (await db.Set<OperationalSequenceVersion>().Where(v => v.SequenceId == root.Id).MaxAsync(v => (int?)v.Number, token) ?? 0) + 1,
            Name = root.Name, Description = root.Description, CreatedAt = clock.UtcNow, CreatedBy = actor,
            StartScheduleJson = published?.StartScheduleJson, StartKind = published?.StartKind ?? "OCASIONAL", FirstMonth = published?.FirstMonth ?? 2,
            WindowFromDay = published?.WindowFromDay ?? 20, WindowThroughDay = published?.WindowThroughDay ?? 25,
            PreferredWeekDay = published?.PreferredWeekDay ?? 1, StartHour = published?.StartHour ?? 0,
            StartHourConfirmed = published?.StartHourConfirmed ?? false };
        db.Add(draft); await db.SaveChangesAsync(token);
        if (published != null)
            foreach (var step in await db.Set<OperationalSequenceStep>().AsNoTracking().Where(s => s.VersionId == published.Id).ToArrayAsync(token)) db.Add(CopyStep(step, draft.Id));
        await db.SaveChangesAsync(token); return draft;
    }

    private static OperationalSequenceStep CopyStep(OperationalSequenceStep s, Guid version) => new() { VersionId = version,
        InputCountSourceTaskId=s.InputCountSourceTaskId, OutputItemId=s.OutputItemId, OutputTargetTaskId=s.OutputTargetTaskId, IsFinalAudit=s.IsFinalAudit,
        Position = s.Position, TaskId = s.TaskId, Action = s.Action, ReleaseCondition = s.ReleaseCondition,
        DeadlineKind = s.DeadlineKind, DeadlineHours = s.DeadlineHours, MinimumStartOffsetHours = s.MinimumStartOffsetHours,
        ReturnToPosition = s.ReturnToPosition, Instructions = s.Instructions.Trim() };

    private void DetachDraftSteps(Guid version)
    {
        foreach (var entry in db.ChangeTracker.Entries<OperationalSequenceStep>().Where(e => e.Entity.VersionId == version).ToArray())
            entry.State = EntityState.Detached;
    }

    private static void ValidateIdentity(string name, string description)
    {
        var issues = new List<ProcessValidationIssue>();
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 120 || name.Any(char.IsControl)) issues.Add(new(null, "Name", "Escribe un nombre de 1 a 120 caracteres, sin caracteres de control."));
        if (description == null || description.Trim().Length > 4000) issues.Add(new(null, "Description", "La descripción admite hasta 4000 caracteres."));
        if (issues.Count == 0) return;
        var error = Error("PROCESS_INVALID"); error.Data["ProcessIssues"] = issues.ToArray(); throw error;
    }

    private static void ValidateRules(ProcessDraftInput input, bool publish)
    {
        try { ValidateRulesCore(input, publish); }
        catch (DirectionPlanException e) { e.Data["ProcessIssues"] = DiagnoseRules(input, publish); throw; }
    }
    private static void ValidateRulesCore(ProcessDraftInput input, bool publish)
    {
        input.Schedule.Capture(input.StartKind);
        if (input.StartKind is not ("OCASIONAL" or "SEMESTRAL" or "SEMANAL" or "MENSUAL" or "TRIMESTRAL" or "ANUAL") || input.FirstMonth is < 1 or > 6 || input.WindowFromDay is < 1 or > 28 ||
            input.WindowThroughDay < input.WindowFromDay || input.WindowThroughDay > 28 || input.PreferredWeekDay is < 0 or > 6 || input.StartHour is < 0 or > 23 ||
            input.Steps.Count > 32 || publish && (input.Steps.Count == 0 || input.StartKind == "SEMESTRAL" && input.StartHour == null) ||
            input.Steps.Select(s => s.TaskId).Distinct().Count() != input.Steps.Count ||
            !input.Steps.Select(s => s.Position).Order().SequenceEqual(Enumerable.Range(1, input.Steps.Count))) throw Error("PROCESS_RULES_INVALID");
        foreach (var s in input.Steps)
        {
            if (s.TaskId == Guid.Empty || s.Action is not ("ENTREGAR" or "REVISAR" or "SELLAR" or "AUTORIZAR") ||
                (s.Position == 1 ? s.ReleaseCondition != "INICIO" : s.ReleaseCondition is not ("ENTREGA" or "APROBACION")) ||
                s.DeadlineKind is not ("RELATIVO" or "FIJO") || s.DeadlineHours is < 1 or > 8760 || s.MinimumStartOffsetHours is < 0 or > 8760 ||
                s.ReturnToPosition is { } target && (target < 1 || target >= s.Position) || s.Instructions == null || s.Instructions.Length > 4000) throw Error("PROCESS_RULES_INVALID");
            if (publish && s.Position > 1)
            {
                var previous = input.Steps.Single(p => p.Position == s.Position - 1);
                if (s.ReleaseCondition != (previous.Action == "ENTREGAR" ? "ENTREGA" : "APROBACION")) throw Error("PROCESS_RULES_INVALID");
            }
        }
        if (input.Steps.Any(s => s.OutputItemId != null)) {
            if (input.Steps.Count(s => s.IsFinalAudit) != 1 || !input.Steps.Single(s => s.Position == input.Steps.Count).IsFinalAudit)
                throw Error("PROCESS_ITEM_AUDIT_REQUIRED");
            foreach (var s in input.Steps.Where(s => s.OutputItemId != null))
                if (s.IsFinalAudit || s.Position == input.Steps.Count)
                    throw Error("PROCESS_ITEM_TARGET_INVALID");
        }
        if (input.Steps.Any(s => s.IsFinalAudit && s.Position != input.Steps.Count || s.OutputItemId == null && s.OutputTargetTaskId != null)) throw Error("PROCESS_ITEM_TARGET_INVALID");
        if(publish && (input.Steps.Count<2 || input.Steps.Single(s=>s.Position==1).Action!="ENTREGAR" || input.Steps.Single(s=>s.Position==input.Steps.Count).Action!="AUTORIZAR"))
            throw Error("PROCESS_DOCUMENT_BOUNDARIES_REQUIRED");
        ProcessCountSources.FromSteps(input.Steps.ToArray());
    }

    private async Task ValidateItems(OperationalSequenceStep[] steps, Guid version, CancellationToken ct)
    {
        var selected = steps.Where(s => s.OutputItemId != null).ToArray();
        var ids = selected.Select(s => s.OutputItemId!.Value).ToArray();
        var items = await db.Set<ProcessItem>().AsNoTracking().Where(i => ids.Contains(i.Id)).ToArrayAsync(ct);
        var previous = await db.Set<OperationalSequenceStep>().AsNoTracking().Where(s => s.VersionId == version).ToArrayAsync(ct);
        foreach (var s in selected)
            if (!items.Any(i => i.Id == s.OutputItemId && (i.Active || previous.Any(p => p.TaskId == s.TaskId && p.OutputItemId == i.Id))))
                throw LocatedError("PROCESS_ITEM_RETIRED", [s.TaskId], "OutputItemId");
    }

    private void Audit(Guid actor, string action, Guid id, object? data = null) => db.AuditEvents.Add(new AuditEvent {
        Id = Guid.CreateVersion7(), OccurredAt = clock.UtcNow, ActorUserId = actor, ActorType = "USER", Action = action,
        ResourceType = "OPERATIONAL_PROCESS", ResourceId = id, BranchId = BranchScope.LorettaId,
        CorrelationId = Guid.CreateVersion7(), Outcome = "SUCCESS", AfterData = JsonSerializer.SerializeToDocument(data ?? new { Id = id }) });
    private static DirectionPlanException Error(string code, int status = 422) => new(code, status);
}
