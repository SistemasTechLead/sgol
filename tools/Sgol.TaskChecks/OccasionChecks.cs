using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Time;
using Sgol.Evidence.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence.Identity;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Sgol.Web.Infrastructure.Persistence.Planning;

internal static class OccasionChecks
{
    public static async Task RunAsync(SgolDbContext db, IDirectionPreparedPlanService direction, OperationalTaskService tasks,
        OperationalSupervisionService supervision, IClock clock, CheckScanner scanner, Guid director, Guid outsider)
    {
        var now = clock.UtcNow;
        var first = Person("OCC-A"); var second = Person("OCC-B");
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        await tasks.SaveAsync(director, new() { Name = "Compartir contenido en redes", Description = "Revisa el material.\nPublica el contenido autorizado.", PeriodType = "OCASIONAL", Requirement = "CAPTURA_PANTALLA",
            Repetitions = 50, ScheduleMode = "PROGRESO", LoadMinutes = 10, Revision = 0 }, default);
        db.ChangeTracker.Clear();
        var task = (await tasks.ListAsync(director, default)).Single(t => t.Name == "Compartir contenido en redes");
        Assert(task.Repetitions == 1 && task.ScheduleMode == "DISTRIBUIDA" && !task.DifferentPeople, "Occasional tasks normalize calendar settings");
        Assert(!OperationalSchedule.Dates(new(2026, 1, 1), new(2026, 12, 31), task).Any(), "Occasional tasks never produce calendar dates");
        Assert((await new DirectionPlanningCatalogReader(db).ReadTaskOptionsAsync()).All(t => t.TaskCode != task.TaskCode), "Occasional tasks excluded from plan catalog");
        await Reject(() => direction.CreateAsync(director, Guid.CreateVersion7(), Guid.CreateVersion7(), new("Invalid occasional plan", "Test", "SEMANAL", 2026, 42, [task.TaskCode])), "DIRECTION_PLAN_TASK_INVALID");
        db.ChangeTracker.Clear();
        var input = new OccasionInput(task.Id, task.Revision, "Promoción botas", "Publica la foto y el texto completos.", "Botas para esta temporada 👢\nVisítanos", "Estado de WhatsApp",
            now.AddMinutes(-1), now.AddHours(5), [first.Person, second.Person]);
        var key = Guid.CreateVersion7();
        var bytes = Image();
        IFormFile File() => new FormFile(new MemoryStream(bytes), 0, bytes.Length, "Material", "promocion.jpg") { Headers = new HeaderDictionary(), ContentType = "image/jpeg" };
        await Reject(() => tasks.AssignOccasionAsync(outsider, key, input, [File()], default), "ACCESO_DENEGADO");
        await Reject(() => tasks.AssignOccasionAsync(director, key, input with { PersonIds = [] }, [], default), "OCCASION_INVALID");
        await Reject(() => tasks.AssignOccasionAsync(director, key, input with { PersonIds = [first.Person, first.Person] }, [], default), "OCCASION_INVALID");
        await Reject(() => tasks.AssignOccasionAsync(director, key, input with { PersonIds = [Guid.NewGuid()] }, [], default), "OCCASION_RECIPIENT_INVALID");
        db.ChangeTracker.Clear();
        scanner.Outcome = EvidenceScanResult.Infectado;
        await Reject(() => tasks.AssignOccasionAsync(director, key, input, [File()], default), "TASK_FILE_SCAN_FAILED");
        scanner.Outcome = EvidenceScanResult.Limpio;
        Assert(!await db.Set<OperationalOccasion>().AnyAsync(x => x.Id == key), "Rejected material stores no occasion");
        var tomorrow = WeekContract.LocalToday(now).AddDays(1);
        db.AvailabilityDayVersions.Add(new(Guid.CreateVersion7(), second.Person, BranchScope.LorettaId, tomorrow, false, director));
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var beforePlans = await db.DirectionPreparedPlans.CountAsync(); var beforeAssignments = await db.OperationalAssignments.CountAsync();
        var blockedKey = Guid.CreateVersion7();
        await Reject(() => tasks.AssignOccasionAsync(director, blockedKey, input with { StartsAt = OperationalSchedule.Instant(tomorrow, 10), DueAt = OperationalSchedule.Instant(tomorrow, 18) }, [File()], default), "PLAN_NO_AVAILABLE_WORKER");
        db.ChangeTracker.Clear();
        Assert(await db.DirectionPreparedPlans.CountAsync() == beforePlans && await db.OperationalAssignments.CountAsync() == beforeAssignments &&
            !await db.Set<OperationalOccasion>().AnyAsync(x => x.Id == blockedKey), "Broadcast assignment is atomic when one recipient is unavailable");
        var planId = await tasks.AssignOccasionAsync(director, key, input, [File()], default); db.ChangeTracker.Clear();
        var assigned = await db.OperationalAssignments.AsNoTracking().Where(x => x.OccasionId == key).ToArrayAsync();
        Assert(assigned.All(x => x.Description == task.Description), "Occasional assignments retain catalogue description alongside occasion instructions");
        Assert(assigned.Length == 2 && assigned.Select(x => x.PersonId).Order().SequenceEqual(input.PersonIds.Order()), "Each selected recipient receives an independent task");
        Assert((await direction.ReadAsync(director, planId)).PeriodType == "OCASIONAL", "Independent occasion is visible in planning");
        Assert((await direction.ReadAsync(director, planId)).Assignments!.All(a => a.OccasionId == key), "Plan details identify occasional assignments");
        var replay = await tasks.AssignOccasionAsync(director, key, input, [File()], default);
        Assert(replay == planId && await db.OperationalAssignments.CountAsync(x => x.OccasionId == key) == 2, "Retry recovers occasion without duplicating recipients");
        await Reject(() => tasks.AssignOccasionAsync(director, key, input with { PublicationText = "Changed" }, [File()], default), "OCCASION_CONFLICT");
        db.ChangeTracker.Clear();
        var own = assigned.Single(x => x.PersonId == first.Person);
        var material = await tasks.OccasionAsync(first.User, own.Id, default);
        Assert(material!.PublicationText == input.PublicationText && material.Files.Count == 1, "Worker can read immutable instructions and publication text");
        var file = await tasks.ReferenceFileAsync(first.User, own.Id, material.Files[0].Id, default);
        Assert(file.Content.SequenceEqual(bytes), "Reference image downloads unchanged");
        await Reject(() => tasks.OccasionAsync(outsider, own.Id, default), "ACCESO_DENEGADO");
        await Reject(() => tasks.ReferenceFileAsync(outsider, own.Id, file.Id, default), "ACCESO_DENEGADO");
        await Reject(() => tasks.ReferenceFileAsync(first.User, own.Id, Guid.NewGuid(), default), "TASK_NOT_FOUND");
        Assert((await supervision.ListAsync(director, default)).Any(x => x.Assignment.Id == own.Id && x.Assignment.OccasionId == key), "Supervision sees occasional work");
        var day = WeekContract.LocalToday(now);
        var capacity = new OperationalCapacityService(db, clock, supervision);
        var capacityPreview = await capacity.PreviewAsync(director, new(first.Person, day, day, 0, 120, "Capacity check"), default);
        Assert(capacityPreview.Moves.All(m => m.TaskId != own.Id), "Capacity changes preserve explicit occasion recipients");
        Assert((await capacity.RowsAsync(director, day, day, default)).Single(w => w.PersonId == first.Person).ProtectedLoad == 10, "Occasional load is protected and counted in capacity");
        Assert((await new LiveBoardReader(db, clock).ReadAsync(first.User, planId)).Tasks.Count == 2, "Occasional assignments appear on live board");
        await Reject(() => supervision.ReviewAsync(director, own.Id, own.Revision, "CONCLUIDA", null, default), "TASK_FILE_REQUIRED");
        await Reject(() => tasks.CompleteAsync(first.User, own.Id, null, null, default, "Publicado"), "TASK_FILE_REQUIRED");
        var invalid = new FormFile(new MemoryStream("not an image"u8.ToArray()), 0, 12, "file", "capture.png") { Headers = new HeaderDictionary(), ContentType = "image/png" };
        await Reject(() => tasks.CompleteAsync(first.User, own.Id, null, invalid, default, "Publicado"), "TASK_FILE_INVALID");
        scanner.Outcome = EvidenceScanResult.Infectado;
        await Reject(() => tasks.CompleteAsync(first.User, own.Id, null, File(), default, "Publicado"), "TASK_FILE_SCAN_FAILED");
        scanner.Outcome = EvidenceScanResult.Limpio;
        await tasks.CompleteAsync(first.User, own.Id, null, File(), default, "Publicado"); db.ChangeTracker.Clear();
        var delivered = (await tasks.MineAsync(first.User, default)).Single(a => a.Id == own.Id);
        Assert(delivered.ReviewStatus == "PENDIENTE_REVISION" && delivered.OccasionId == key, "Screenshot delivery enters review and appears in My work");
        Assert((await tasks.FileAsync(first.User, own.Id, default)).FileContent!.SequenceEqual(bytes), "Screenshot evidence is independent of reference material");
        Assert((await OperationalCapacityService.LedgerAsync(db, now, default)).Tasks.All(a => a.Id != own.Id), "Occasional delivery releases pending capacity");
        task.Name = "Plantilla editada"; task.Requirement = "NINGUNO";
        await tasks.SaveAsync(director, task, default); db.ChangeTracker.Clear();
        Assert((await tasks.MineAsync(first.User, default)).Single(a => a.Id == own.Id).Requirement == "CAPTURA_PANTALLA", "Catalog edits preserve previous evidence requirements");
        await supervision.ReviewAsync(director, own.Id, delivered.Revision, "MISMA_PERSONA", "Mostrar texto completo", default); db.ChangeTracker.Clear();
        var replacement = await db.OperationalAssignments.AsNoTracking().SingleAsync(a => a.ReassignedFromId == own.Id);
        Assert(replacement.OccasionId == key && replacement.Requirement == "CAPTURA_PANTALLA" && replacement.FileContent == null, "Reassignment retains occasion and requires fresh evidence");
        Assert((await tasks.OccasionAsync(first.User, replacement.Id, default))!.PublicationText == input.PublicationText, "Reassignment retains original material and copy");
        await tasks.CompleteAsync(first.User, replacement.Id, null, File(), default, "Corregido"); db.ChangeTracker.Clear();
        replacement = await db.OperationalAssignments.SingleAsync(a => a.Id == replacement.Id);
        await supervision.ReviewAsync(director, replacement.Id, replacement.Revision, "CONCLUIDA", null, default); db.ChangeTracker.Clear();
        Assert((await tasks.MineAsync(first.User, default)).Single(a => a.Id == replacement.Id).ReviewStatus == "CONCLUIDA", "Screenshot can be approved after correction");
        var current = (await tasks.ListAsync(director, default)).Single(t => t.Id == task.Id);
        var autoKey = Guid.CreateVersion7();
        await tasks.AssignOccasionAsync(director, autoKey, input with { TaskRevision = current.Revision, PersonIds = [], Automatic = true }, [], default); db.ChangeTracker.Clear();
        Assert(await db.OperationalAssignments.CountAsync(a => a.OccasionId == autoKey) == 1, "Automatic distribution creates one assignment");
        var capacityKey = Guid.CreateVersion7();
        current.LoadMinutes = 4800; await tasks.SaveAsync(director, current, default); db.ChangeTracker.Clear();
        current = (await tasks.ListAsync(director, default)).Single(t => t.Id == current.Id);
        await Reject(() => tasks.AssignOccasionAsync(director, capacityKey, input with { TaskRevision = current.Revision }, [], default), "PLAN_NO_AVAILABLE_WORKER");
        db.ChangeTracker.Clear();
        Assert(!await db.Set<OperationalOccasion>().AnyAsync(o => o.Id == capacityKey), "Insufficient capacity does not save a partial occasion");
        current.LoadMinutes = 10; await tasks.SaveAsync(director, current, default); db.ChangeTracker.Clear();
        current = (await tasks.ListAsync(director, default)).Single(t => t.Id == current.Id);
        var month = WeekContract.LocalToday(now);
        var regular = new DirectionPreparedPlan(Guid.CreateVersion7(), BranchScope.LorettaId, director, now, new("Existing plan for occasional work", "Tests", "MENSUAL", month.Year, month.Month, []));
        regular.MarkAssigned(); db.DirectionPreparedPlans.Add(regular); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var linkedKey = Guid.CreateVersion7();
        await tasks.AssignOccasionAsync(director, linkedKey, input with { TaskRevision = current.Revision, PersonIds = [first.Person], PlanId = regular.Id }, [File()], default); db.ChangeTracker.Clear();
        Assert(await db.OperationalAssignments.AnyAsync(a => a.OccasionId == linkedKey && a.PlanId == regular.Id), "Occasional task can be attached to an existing plan");
        var futureKey = Guid.CreateVersion7();
        await tasks.AssignOccasionAsync(director, futureKey, input with { TaskRevision = current.Revision, PersonIds = [first.Person], StartsAt = now.AddDays(2), DueAt = now.AddDays(2).AddHours(5) }, [], default); db.ChangeTracker.Clear();
        var future = await db.OperationalAssignments.AsNoTracking().SingleAsync(a => a.OccasionId == futureKey);
        await Reject(() => tasks.CompleteAsync(first.User, future.Id, null, null, default, "Too early"), "TASK_NOT_STARTED");
        // Existing explicit plan deletion also removes the occasion's private material atomically.
        var deletion = await direction.PreviewDeletionAsync(director, regular.Id);
        await direction.DeleteAsync(director, regular.Id, Guid.CreateVersion7(), Guid.CreateVersion7(), deletion.Fingerprint); db.ChangeTracker.Clear();
        Assert(!await db.Set<OperationalOccasion>().AnyAsync(o => o.Id == linkedKey) && !await db.Set<OperationalReferenceFile>().AnyAsync(f => f.OccasionId == linkedKey), "Plan deletion handles occasional metadata and files");

        (Guid User, Guid Person) Person(string code)
        {
            var person = Guid.CreateVersion7(); var user = Guid.CreateVersion7(); var from = now.AddYears(-1);
            db.People.Add(new Person { Id = person, StableCode = code, DisplayName = code, CreatedAt = from });
            db.AppUsers.Add(new AppUser { Id = user, PersonId = person, Status = AccountStatus.Active, SecurityStamp = "checks" });
            db.EmploymentVersions.Add(new(Guid.CreateVersion7(), person, BranchScope.LorettaId, EmploymentStatus.Active, from));
            db.RoleAssignmentVersions.Add(new RoleAssignmentVersion { Id = Guid.CreateVersion7(), UserId = user, BranchId = BranchScope.LorettaId, RoleCode = CanonicalRole.SalesFloor, Status = RoleAssignmentStatus.Active, ValidFrom = from });
            return (user, person);
        }
    }

    private static byte[] Image()
    {
        using var bitmap = new SkiaSharp.SKBitmap(20, 20);
        bitmap.Erase(SkiaSharp.SKColors.Red); using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Jpeg, 90); return data.ToArray();
    }
    private static void Assert(bool condition, string name) { if (!condition) throw new Exception("FAILED: " + name); Console.WriteLine("PASS: " + name); }
    private static async Task Reject(Func<Task> action, string code)
    {
        try { await action(); throw new Exception("Expected rejection: " + code); }
        catch (DirectionPlanException e) when (e.Code == code) { Console.WriteLine("PASS: Rejected " + code); }
    }
}
