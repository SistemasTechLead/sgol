using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Planning;

static class EarlyDeliveryChecks
{
    public static async Task RunAsync(SgolDbContext db, OperationalTaskService tasks,
        OperationalSupervisionService supervision, Guid director, Guid actor, Guid person, Guid other, DateTimeOffset now)
    {
        var day = WeekContract.LocalToday(now);
        var plan = DirectionPreparedPlan.ForOccasion(Guid.CreateVersion7(), BranchScope.LorettaId, director,
            now, "Entrega anticipada", day, day.AddDays(7));
        db.Add(plan);
        var definition = await db.OperationalTasks.FirstAsync();
        OperationalAssignment Make(string requirement) => new() { Id = Guid.CreateVersion7(), PlanId = plan.Id,
            TaskId = definition.Id, TaskCode = definition.TaskCode, Name = "Anticipada " + requirement,
            AssignedRole = CanonicalRole.SalesFloor, PersonId = person, PersonName = "A", Requirement = requirement,
            LoadMinutes = 10, StartsAt = now.AddDays(2), DueAt = now.AddDays(2).AddHours(1) };
        var comment = Make("COMENTARIO"); var photo = Make("FOTOGRAFIA"); var goal = Make("NINGUNO");
        goal.GoalUnits = 2; goal.ProgressUnit = "unidades"; goal.ScheduleMode = "PROGRESO";
        db.AddRange(comment, photo, goal);
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        await Reject(() => tasks.CompleteAsync(other, comment.Id, "Listo", null, default), "TASK_NOT_FOUND");
        await Reject(() => tasks.CompleteAsync(actor, comment.Id, "", null, default), "TASK_COMMENT_REQUIRED");
        await Reject(() => supervision.ReviewAsync(director, comment.Id, comment.Revision, "CONCLUIDA", null, default), "TASK_NOT_STARTED");
        await tasks.CompleteAsync(actor, comment.Id, "Trabajo realizado", null, default);
        db.ChangeTracker.Clear();
        var submitted = await db.OperationalAssignments.AsNoTracking().SingleAsync(a => a.Id == comment.Id);
        Check(submitted.CompletedAt == now && submitted.CompletedAt < submitted.StartsAt &&
            submitted.StartsAt == comment.StartsAt && submitted.DueAt == comment.DueAt &&
            submitted.ReviewStatus == "PENDIENTE_REVISION", "Early delivery preserves dates and awaits review");
        await tasks.CompleteAsync(actor, comment.Id, "Retry", null, default); db.ChangeTracker.Clear();
        Check((await db.OperationalAssignments.AsNoTracking().SingleAsync(a => a.Id == comment.Id)).Revision == submitted.Revision,
            "Retry does not rewrite early delivery");
        await supervision.ReviewAsync(director, comment.Id, submitted.Revision, "CONCLUIDA", null, default);
        db.ChangeTracker.Clear();
        Check((await db.OperationalAssignments.AsNoTracking().SingleAsync(a => a.Id == comment.Id)).ReviewStatus == "CONCLUIDA",
            "Supervision can approve a submitted future task");
        await Reject(() => tasks.CompleteAsync(actor, goal.Id, null, null, default), "TASK_GOAL_PENDING");
        await tasks.ProgressAsync(actor, goal.Id, goal.Revision, 2, "Adelanto realizado", default);
        db.ChangeTracker.Clear();
        await tasks.CompleteAsync(actor, goal.Id, null, null, default); db.ChangeTracker.Clear();
        Check(await db.OperationalProgressEntries.AnyAsync(p => p.AssignmentId == goal.Id && p.OccurredAt == now),
            "Future goals accept progress and completion after reaching the goal");
        await Reject(() => tasks.BeginCaptureAsync(other, photo.Id, default), "TASK_NOT_FOUND");
        await Reject(() => tasks.CompleteAsync(actor, photo.Id, null, null, default), "TASK_CAMERA_REQUIRED");
        var capture = await tasks.BeginCaptureAsync(actor, photo.Id, default); db.ChangeTracker.Clear();
        using var bitmap = new SkiaSharp.SKBitmap(64, 32);
        using var canvas = new SkiaSharp.SKCanvas(bitmap); canvas.Clear(SkiaSharp.SKColors.Blue);
        using var image = bitmap.Encode(SkiaSharp.SKEncodedImageFormat.Jpeg, 90);
        var bytes = image.ToArray();
        var file = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "photo", "capture.jpg") {
            Headers = new HeaderDictionary(), ContentType = "image/jpeg" };
        var photoId = Guid.NewGuid();
        await tasks.UploadPhotographAsync(actor, photo.Id, capture.Id, photoId, file, default); db.ChangeTracker.Clear();
        await tasks.CompleteAsync(actor, photo.Id, null, null, default, captureSessionId: capture.Id, photographIds: [photoId]);
        db.ChangeTracker.Clear();
        Check(await db.OperationalPhotographs.AnyAsync(p => p.AssignmentId == photo.Id && p.AttachedAt == now),
            "Camera evidence uploads and attaches before activation");
        await Reject(() => tasks.BeginCaptureAsync(actor, photo.Id, default), "TASK_CONFLICT");
        var photoRevision = await db.OperationalAssignments.Where(a => a.Id == photo.Id).Select(a => a.Revision).SingleAsync();
        await supervision.ReviewBatchAsync(director, [new(photo.Id, photoRevision, "CONCLUIDA", null)], default);
        db.ChangeTracker.Clear();
        Check(await db.OperationalAssignments.AnyAsync(a => a.Id == photo.Id && a.ReviewStatus == "CONCLUIDA"),
            "Batch validation can approve early camera evidence");
        Console.WriteLine("PASS Early delivery integration completed on dedicated database");
    }
    static void Check(bool condition, string label) { if (!condition) throw new Exception(label); Console.WriteLine("PASS " + label); }
    static async Task Reject(Func<Task> action, string code) {
        try { await action(); throw new Exception("Expected " + code); }
        catch (DirectionPlanException e) when (e.Code == code) { Console.WriteLine("PASS Rejected " + code); }
    }
}
