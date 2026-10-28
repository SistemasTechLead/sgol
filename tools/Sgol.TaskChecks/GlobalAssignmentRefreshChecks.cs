using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Sgol.BuildingBlocks.Time;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Organization;
using Sgol.Web.Infrastructure.Persistence.Planning;

internal static class GlobalAssignmentRefreshChecks
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS global refresh: " + message); }
    private static async Task Reject(Func<Task> action)
    {
        try { await action(); throw new Exception("Stale or incompatible batch accepted"); }
        catch (DirectionPlanException e) when (e.Code == "REFRESH_STALE") { Console.WriteLine("PASS global refresh: rejects entire stale/conflicting batch"); }
    }
    public static async Task RunAsync(SgolDbContext db, OperationalTaskService tasks, IDirectionPreparedPlanService direction, IClock clock, Guid actor, Guid person)
    {
        var service = new AssignmentRefreshService(db, direction, clock);
        var day = WeekContract.LocalToday(clock.UtcNow);
        db.Add(new TemporalAssignmentState { Enabled = true });
        db.Add(new PersonalScheduleVersion { Id = Guid.CreateVersion7(), PersonId = person, From = day, Revision = 1,
            ChangedBy = actor, ChangedAt = clock.UtcNow, IntentKey = Guid.NewGuid(),
            DaysJson = JsonSerializer.Serialize(Enumerable.Range(0,7).ToDictionary(d => d, d => new[] { new WorkInterval(8*60,20*60) })) });
        var definitions = new List<OperationalTask>();
        foreach (var name in new[] { "Global A", "Global B", "Estable" })
        {
            await tasks.SaveAsync(actor, new OperationalTask { Name = name, Requirement = "NINGUNO", LoadMinutes = 20,
                OperationalCriticality = "FLEXIBLE", Revision = 0 }, default);
            definitions.Add(await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.Name == name));
        }
        var plan = DirectionPreparedPlan.ForOccasion(Guid.CreateVersion7(), BranchScope.LorettaId, actor, clock.UtcNow, "Actualización global", day, day.AddDays(6));
        db.Add(plan);
        var policy = await OperationalSchedulingPolicyService.CurrentAsync(db, default);
        var rows = definitions.Select((t,i) => {
            var row = new OperationalAssignment { Id = Guid.CreateVersion7(), PlanId = plan.Id, TaskId = t.Id, TaskCode = t.TaskCode,
                AssignedRole = t.AssignedRole, PersonId = person, PersonName = "A" };
            AssignmentDefinition.Adopt(row, t, null, false);
            OperationalAgenda.Capture(row, policy, t.OperationalCriticality, TemporalEligibilityContext.At(day, 14*60+i*30));
            return row;
        }).ToArray();
        rows[2].StartsAt = TemporalEligibilityContext.At(day,16*60); rows[2].NominalStartsAt = rows[2].StartsAt; rows[2].DueAt = rows[2].StartsAt.AddMinutes(30);
        var delivered = AssignmentDefinition.Copy(rows[0]); delivered.Id = Guid.CreateVersion7(); delivered.CompletedAt = clock.UtcNow;
        delivered.CompletionKind = "ENTREGA"; delivered.CompletedBy = actor; delivered.ReviewStatus = "PENDIENTE_REVISION";
        db.AddRange(rows); db.Add(delivered); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var protectedBefore = JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().Where(a => a.Id == rows[2].Id || a.Id == delivered.Id).OrderBy(a => a.Id).ToArrayAsync());
        definitions[0].Requirement = "COMENTARIO"; definitions[0].LoadMinutes = 40;
        definitions[1].Requirement = "COMENTARIO"; definitions[1].LoadMinutes = 35;
        await tasks.SaveAsync(actor, definitions[0], default); await tasks.SaveAsync(actor, definitions[1], default); db.ChangeTracker.Clear();
        var director = await db.AppUsers.Where(u => u.Id == actor).Select(u => u.PersonId).SingleAsync();
        var page = new Sgol.Web.Pages.Tasks.IndexModel(new CheckSession(actor, director), tasks, new SpecializationService(db,direction,clock),
            new OperationalProcessCatalogService(db,direction,clock,new ConfigurationBuilder().Build()), new OperationalSchedulingPolicyService(db,direction,clock),service)
            { Search = "Estable", PageContext = new PageContext { HttpContext = new DefaultHttpContext() } };
        await page.OnGetAsync(null,false,default);
        Check(page.Items.Count == 1 && page.RefreshCounts.Count == 2 && page.RefreshCounts.Values.Sum() == 2, "global enablement ignores the search filter and selected row");
        var request = new AssignmentRefreshRequest(Guid.Empty, Guid.NewGuid(), clock.UtcNow.AddMinutes(3));
        var preview = await service.PreviewAsync(actor,request,default);
        Check(preview.CanApply, "multiple changed tasks share one valid agenda: " + string.Join(";",preview.Conflicts));
        Check(preview.Differences.Select(d => (d.After ?? d.Before)!.TaskId).Distinct().Count() == 2, "one preview contains every changed task");
        Check(preview.Differences.Select(d => (d.After ?? d.Before)!.Id).Distinct().Count() == preview.Differences.Length, "shared reflow produces one proposal per assignment");
        Check(preview.Fingerprint == (await service.PreviewAsync(actor,request,default)).Fingerprint, "global preview is stable");
        var proposed = preview.Differences.Select(d => d.After!).OrderBy(a => a.StartsAt).ToArray();
        Check(proposed[0].DueAt <= proposed[1].StartsAt, "joint reflow does not overlap the two updated jobs");
        await service.ApplyAsync(actor,request,preview.Fingerprint,"Actualizar catálogo completo",default); db.ChangeTracker.Clear();
        Check(await db.OperationalAssignments.CountAsync(a => (a.Id == rows[0].Id || a.Id == rows[1].Id) && a.Requirement == "COMENTARIO") == 2,
            "global confirmation adopts both definitions atomically");
        Check(protectedBefore == JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().Where(a => a.Id == rows[2].Id || a.Id == delivered.Id).OrderBy(a => a.Id).ToArrayAsync()),
            "unchanged work and delivered assignments keep every field");
        Check((await service.CountsAsync(actor,default)).Count == 0 && !(await service.PreviewAsync(actor,request with { Key = Guid.NewGuid() },default)).CanApply,
            "global action becomes inactive when all parameters match");
        var audits = await db.AuditEvents.CountAsync();
        await service.ApplyAsync(actor,request,preview.Fingerprint,"Actualizar catálogo completo",default);
        Check(audits == await db.AuditEvents.CountAsync(),"global replay creates no extra writes");
        var a = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.Id == definitions[0].Id); a.Description = "Cambio A";
        var b = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.Id == definitions[1].Id); b.AssignedRole = "SUBCOORDINACION";
        await tasks.SaveAsync(actor,a,default); await tasks.SaveAsync(actor,b,default); db.ChangeTracker.Clear();
        request = request with { Key = Guid.NewGuid() };
        preview = await service.PreviewAsync(actor,request,default);
        Check(!preview.CanApply,"a conflict in one task blocks the complete batch");
        await Reject(() => service.ApplyAsync(actor,request,preview.Fingerprint,"Debe fallar",default));
        Check((await db.OperationalAssignments.AsNoTracking().SingleAsync(x => x.Id == rows[0].Id)).Description == null,"a blocked batch does not partially adopt another task");
        b = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.Id == b.Id); b.AssignedRole = "PISO_VENTAS"; b.LoadMinutes++;
        await tasks.SaveAsync(actor,b,default); db.ChangeTracker.Clear();
        request = request with { Key = Guid.NewGuid() };
        preview = await service.PreviewAsync(actor,request,default);
        Check(preview.CanApply,"resolved conflict produces a complete new preview");
        var changed = await db.OperationalAssignments.SingleAsync(x => x.Id == rows[1].Id); changed.Revision++;
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        await Reject(() => service.ApplyAsync(actor,request,preview.Fingerprint,"Versión vencida",default));
        Check((await db.OperationalAssignments.AsNoTracking().SingleAsync(x => x.Id == rows[0].Id)).Description == null,"concurrency in another task invalidates the whole global preview");
        var divergent = await db.OperationalAssignments.SingleAsync(x => x.Id == rows[2].Id);
        divergent.Requirement = "DOCUMENTO"; divergent.Revision++;
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        Check((await service.CountsAsync(actor,default)).ContainsKey(definitions[2].Id), "actual assignment differences enable the global action even when its saved definition snapshot matches");
        var actualPreview = await service.PreviewAsync(actor,request with { Key = Guid.NewGuid() },default);
        Check(actualPreview.Differences.Any(d => d.Before?.Id == rows[2].Id && d.Fields.Contains("Requisito")), "global preview compares effective assignment parameters against the catalogue");
        var legacy = await db.OperationalAssignments.SingleAsync(x => x.Id == rows[2].Id);
        legacy.DefinitionSnapshotJson = null; legacy.TaskRevision = 1; legacy.TransferRemainingMinutes = 7;
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var metadataBefore = await db.OperationalAssignments.AsNoTracking().SingleAsync(x => x.Id == rows[2].Id);
        var descriptive = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.Id == definitions[2].Id);
        descriptive.Name = "Nombre actualizado"; descriptive.Description = "Instrucciones actualizadas"; descriptive.Requirement = "COMENTARIO";
        await tasks.SaveAsync(actor,descriptive,default);
        b = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.Id == definitions[1].Id); b.LoadMinutes += 50;
        await tasks.SaveAsync(actor,b,default); db.ChangeTracker.Clear();
        request = request with { Key = Guid.NewGuid() };
        preview = await service.PreviewAsync(actor,request,default);
        Check(preview.CanApply,"metadata-only legacy work can coexist with a scheduling change: " + string.Join(";",preview.Conflicts));
        var metadataDiff = preview.Differences.Single(d => d.Before?.Id == rows[2].Id);
        Check(metadataDiff.MetadataOnly && !preview.Decisions.Any(d => d.TaskCodes.Contains(descriptive.TaskCode)),"name, description and evidence need no historical-calendar or manual-estimate decision");
        static string OperationalFields(OperationalAssignment row) => JsonSerializer.Serialize(typeof(OperationalAssignment).GetProperties()
            .Where(p => p.CanWrite && p.Name is not ("Name" or "Description" or "Requirement" or "MinimumPhotos" or "MaximumPhotos" or "MaximumPhotoBytes" or "DefinitionSnapshotJson" or "Revision"))
            .OrderBy(p => p.Name).ToDictionary(p => p.Name,p => p.GetValue(row)));
        Check(OperationalFields(metadataBefore) == OperationalFields(metadataDiff.After!),"mixed global reflow keeps every operational field of metadata-only work, including activation and manual estimate");
        await service.ApplyAsync(actor,request,preview.Fingerprint,"Datos descriptivos y nueva carga",default); db.ChangeTracker.Clear();
        var metadataSaved = await db.OperationalAssignments.AsNoTracking().SingleAsync(x => x.Id == rows[2].Id);
        Check(metadataSaved.Name == descriptive.Name && metadataSaved.Description == descriptive.Description && metadataSaved.Requirement == "COMENTARIO" &&
            OperationalFields(metadataBefore) == OperationalFields(metadataSaved),"metadata confirmation updates text and evidence while preserving the complete operational state");
        Check(!(await service.CountsAsync(actor,default)).ContainsKey(descriptive.Id),"metadata adoption clears the actual difference without pretending to adopt a historical calendar");
        descriptive = await db.OperationalTasks.AsNoTracking().SingleAsync(t => t.Id == descriptive.Id); descriptive.LoadMinutes++;
        await tasks.SaveAsync(actor,descriptive,default); db.ChangeTracker.Clear();
        preview = await service.PreviewAsync(actor,request with { Key = Guid.NewGuid() },default);
        Check(preview.Decisions.Any(d => d.Code == "calendar") && preview.Decisions.Any(d => d.Code == "remaining"),"a later workload change still requires calendar and manual-estimate decisions");
        Console.WriteLine("PASS global assignment refresh lifecycle");
    }
}

