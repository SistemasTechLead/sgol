using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.Assignment.Contracts;
using Sgol.BuildingBlocks.Time;
using Sgol.Evidence.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed partial class OperationalTaskService(SgolDbContext db, IDirectionPreparedPlanService direction,
    IClock clock, IFileTechnicalValidator validator, IFileMalwareScanner scanner, IConfiguration? configuration = null)
{
    public async Task<List<OperationalTask>> ListAsync(Guid actor, CancellationToken token)
    {
        await TaskCatalogAuthorization.RequireReadAsync(db, actor, clock.UtcNow, token);
        return await db.OperationalTasks.AsNoTracking().Where(t => !t.Deleted).OrderBy(t => t.Name).ToListAsync(token);
    }

    public async Task SaveAsync(Guid actor, OperationalTask input, CancellationToken token)
    {
        await direction.AuthorizeAsync(actor, token); input.Validate();
        await using var tx = await db.Database.BeginTransactionAsync(token);
        await LockAsync(db, token); await direction.AuthorizeAsync(actor, token);
        if (input.SpecializationId is { } specialization && !await db.Set<Sgol.Web.Infrastructure.Persistence.Organization.Specialization>().AnyAsync(s => s.Id == specialization && s.Active, token))
            throw new DirectionPlanException("TASK_SPECIALIZATION_INVALID", 422);
        var task = await db.OperationalTasks.SingleOrDefaultAsync(t => t.Id == input.Id && !t.Deleted, token);
        if (task is null && input.Revision != 0) throw new DirectionPlanException("TASK_NOT_FOUND", 404);
        await OperationalProcessCatalogService.RequireMembershipChangeAsync(db, task, input.ProcessId, token);
        if (task != null && !input.Active && await (from step in db.Set<OperationalSequenceStep>()
            join process in db.Set<OperationalSequence>() on step.VersionId equals process.PublishedVersionId
            where step.TaskId == task.Id select step).AnyAsync(token))
            throw new DirectionPlanException("PROCESS_TASK_COMMITTED", 409);
        if (task is null)
        {
            task = new OperationalTask { Id = Guid.CreateVersion7(), TaskCode = "T-" + Guid.NewGuid().ToString("N")[..12] };
            db.OperationalTasks.Add(task);
        }
        else if (task.Revision != input.Revision) throw new DirectionPlanException("TASK_CONFLICT", 409);
        task.ProcessId = input.ProcessId;
        if (task.ProcessId != OperationalSequence.GeneralId)
        {
            var process = await db.Set<OperationalSequence>().SingleAsync(p => p.Id == task.ProcessId, token);
            process.Revision++;
        }
        task.Name = input.Name; task.Requirement = input.Requirement; task.PeriodType = input.PeriodType;
        task.Description = input.Description;
        task.AssignedRole = input.AssignedRole;
        task.IsWeeklyOperationsTask = input.IsWeeklyOperationsTask;
        task.SpecializationId = input.SpecializationId;
        task.Repetitions = input.Repetitions; task.DeadlineHours = input.DeadlineHours;
        task.LoadMinutes = input.LoadMinutes; task.Active = input.Active; task.Revision++;
        task.StartMinute = input.StartMinute; task.SundayStartMinute = input.SundayStartMinute;
        task.ScheduleMode = input.ScheduleMode; task.WeekDays = input.WeekDays; task.StartHour = input.StartHour;
        task.PreferredMonthDays = input.PreferredMonthDays;
        task.MinimumPhotos = input.MinimumPhotos; task.MaximumPhotos = input.MaximumPhotos;
        task.MaximumPhotoBytes = input.MaximumPhotoBytes;
        task.DifferentPeople = input.DifferentPeople; task.GoalUnits = input.GoalUnits; task.ProgressUnit = input.ProgressUnit;
        Audit(actor, "OPERATIONAL_TASK_SAVED", task.Id, new { task.Id, task.ProcessId, task.Name, task.Description, task.Requirement, task.AssignedRole, task.SpecializationId, task.PeriodType, task.Repetitions, task.DeadlineHours, task.LoadMinutes, task.Active, task.IsWeeklyOperationsTask, task.ScheduleMode, task.WeekDays, task.PreferredMonthDays, task.StartHour, task.StartMinute, task.SundayStartMinute, task.Revision, task.DifferentPeople, task.GoalUnits, task.ProgressUnit });
        await db.SaveChangesAsync(token); await tx.CommitAsync(token);
    }

    public async Task ChangeAsync(Guid actor, Guid id, long revision, bool delete, CancellationToken token)
    {
        await direction.AuthorizeAsync(actor, token);
        await using var tx = await db.Database.BeginTransactionAsync(token);
        await LockAsync(db, token); await direction.AuthorizeAsync(actor, token);
        var task = await db.OperationalTasks.SingleOrDefaultAsync(t => t.Id == id && !t.Deleted, token)
            ?? throw new DirectionPlanException("TASK_NOT_FOUND", 404);
        if (task.Revision != revision) throw new DirectionPlanException("TASK_CONFLICT", 409);
        if ((delete || task.Active) && await (from step in db.Set<OperationalSequenceStep>()
            join process in db.Set<OperationalSequence>() on step.VersionId equals process.PublishedVersionId
            where step.TaskId == id select step).AnyAsync(token))
            throw new DirectionPlanException("PROCESS_TASK_COMMITTED", 409);
        if (!delete && !task.Active && task.SpecializationId is { } specialization &&
            !await db.Set<Sgol.Web.Infrastructure.Persistence.Organization.Specialization>().AnyAsync(s => s.Id == specialization && s.Active, token))
            throw new DirectionPlanException("TASK_SPECIALIZATION_INVALID", 422);
        if (delete) { task.Deleted = true; task.Active = false; }
        else { task.Active = !task.Active; task.Validate(); }
        task.Revision++;
        Audit(actor, delete ? "OPERATIONAL_TASK_DELETED" : "OPERATIONAL_TASK_TOGGLED", id, new { task.Active, task.Deleted });
        await db.SaveChangesAsync(token); await tx.CommitAsync(token);
    }

    // A branch-wide transactional lock also protects balancing across different Direction accounts.
    internal static Task LockAsync(SgolDbContext context, CancellationToken token) =>
        context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(734651001)", token);

    private async Task<Guid> PersonAsync(Guid actor, CancellationToken token)
    {
        var at = clock.UtcNow;
        var user = await db.AppUsers.AsNoTracking().SingleOrDefaultAsync(u => u.Id == actor && u.Status == AccountStatus.Active, token);
        if (user is null || !await db.EmploymentVersions.AnyAsync(e => e.PersonId == user.PersonId && e.BranchId == BranchScope.LorettaId &&
            e.Status == EmploymentStatus.Active && e.ValidFrom <= at && (e.ValidTo == null || e.ValidTo > at), token) ||
            !await db.RoleAssignmentVersions.AnyAsync(r => r.UserId == actor && r.BranchId == BranchScope.LorettaId &&
            r.Status == RoleAssignmentStatus.Active && r.ValidFrom <= at && (r.ValidTo == null || r.ValidTo > at), token))
            throw new DirectionPlanException("ACCESO_DENEGADO", 403);
        return user.PersonId;
    }

    public async Task<IReadOnlyList<OperationalAssignmentDetails>> MineAsync(Guid actor, CancellationToken token)
    {
        var person = await PersonAsync(actor, token);
        // Do not read attachment bytes when listing tasks.
        return await db.OperationalAssignments.AsNoTracking().Where(a => a.PersonId == person)
            .Join(db.DirectionPreparedPlans.AsNoTracking(), a => a.PlanId, p => p.Id, (a, p) => new { Assignment = a, Plan = p })
            .OrderBy(x => x.Assignment.CompletedAt != null).ThenBy(x => x.Assignment.DueAt).ThenBy(x => x.Assignment.Id)
            .Select(x => new OperationalAssignmentDetails(
                x.Assignment.Id, x.Assignment.TaskCode, x.Assignment.Name, x.Assignment.Requirement, x.Assignment.LoadMinutes, x.Assignment.PersonId, x.Assignment.PersonName,
                x.Assignment.StartsAt, x.Assignment.DueAt, x.Assignment.CompletedAt, x.Assignment.PreviousLoadMinutes, x.Assignment.Comment, (x.Assignment.Attachment != null ? x.Assignment.Attachment.FileName : db.OperationalPhotographs.Where(f => f.AssignmentId == x.Assignment.Id && f.AttachedAt != null).OrderBy(f => f.Position).Select(f => f.FileName).FirstOrDefault()), x.Assignment.AssignedRole,
                x.Assignment.Conclusion, x.Assignment.ReviewStatus, x.Assignment.ReviewComment ?? x.Assignment.ReassignmentComment,
                x.Plan.Id, x.Plan.Name, x.Plan.PeriodType, x.Plan.StartsOn, x.Plan.EndsOn,
                x.Assignment.ScheduleMode, x.Assignment.GoalUnits, x.Assignment.ProgressUnits, x.Assignment.ProgressUnit, x.Assignment.Revision,
                x.Assignment.SpecializationId, x.Assignment.SpecializationName, x.Assignment.MinimumPhotos, x.Assignment.MaximumPhotos, x.Assignment.MaximumPhotoBytes, x.Assignment.OccasionId, x.Assignment.Description, x.Assignment.TemporalPolicyJson, x.Assignment.TemporalDecisionJson)).ToArrayAsync(token);
    }

    public async Task CompleteAsync(Guid actor, Guid id, string? comment, IFormFile? file, CancellationToken token, string? conclusion = null,
        Guid? captureSessionId = null, IReadOnlyList<Guid>? photographIds = null, IReadOnlyList<IFormFile>? screenshots = null)
    {
        await RequireIndependentActionAsync(id, token);
        var person = await PersonAsync(actor, token);
        var assignment = await db.OperationalAssignments.AsNoTracking().Where(a => a.Id == id && a.PersonId == person)
            .Select(a => new { a.Requirement, a.StartsAt, a.CompletedAt, a.MinimumPhotos, a.MaximumPhotos, a.MaximumPhotoBytes }).SingleOrDefaultAsync(token)
            ?? throw new DirectionPlanException("TASK_NOT_FOUND", 404);
        if (assignment.CompletedAt is not null) return;
        if (assignment.StartsAt > clock.UtcNow) throw new DirectionPlanException("TASK_NOT_STARTED", 422);
        conclusion = conclusion?.Trim();
        if (conclusion?.Length > 4000)
            throw new DirectionPlanException("TASK_CONCLUSION_TOO_LONG", 422);
        if (string.IsNullOrEmpty(conclusion)) conclusion = null;
        comment = comment?.Trim();
        if (comment?.Length > 4000 || assignment.Requirement == "COMENTARIO" && string.IsNullOrWhiteSpace(comment))
            throw new DirectionPlanException("TASK_COMMENT_REQUIRED", 422);
        byte[]? bytes = null; string? media = null; string? name = null;
        var verifiedScreenshots = new List<(byte[] Bytes, string Media, string Name)>();
        if (assignment.Requirement != "CAPTURA_PANTALLA" && screenshots?.Count > 0)
            throw new DirectionPlanException("TASK_FILE_INVALID", 422);
        if (assignment.Requirement == "CAPTURA_PANTALLA")
        {
            // Retain compatibility with callers submitting a single file.
            var files = screenshots ?? (file is null ? Array.Empty<IFormFile>() : new[] { file });
            if (screenshots is not null && file is not null)
                throw new DirectionPlanException("TASK_FILE_INVALID", 422);
            if (files.Count < assignment.MinimumPhotos || files.Count > assignment.MaximumPhotos ||
                files.Any(f => f.Length is < 1 or > EvidenceFileLimits.MaximumBytes) ||
                files.Sum(f => f.Length) > assignment.MaximumPhotoBytes)
                throw new DirectionPlanException("TASK_PHOTO_LIMIT", 422);
            foreach (var screenshot in files)
                verifiedScreenshots.Add(await VerifiedFileAsync(screenshot, "CAPTURA_PANTALLA", token));
        }
        if (assignment.Requirement == "FOTOGRAFIA" && (file is not null || captureSessionId is null || photographIds is null))
            throw new DirectionPlanException("TASK_CAMERA_REQUIRED", 422);
        if (assignment.Requirement != "FOTOGRAFIA" && (captureSessionId is not null || photographIds?.Count > 0))
            throw new DirectionPlanException("TASK_FILE_INVALID", 422);
        if (assignment.Requirement == "EXCEL")
        {
            if (file is null) throw new DirectionPlanException("TASK_FILE_REQUIRED", 422);
            bytes = await OperationalExcel.ReadAsync(file, token);
            name = Path.GetFileName(file.FileName);
            if (name.Length is < 1 or > 255 || name.Any(char.IsControl))
                throw new DirectionPlanException("TASK_FILE_INVALID", 422);
            using var content = new MemoryStream(bytes, writable: false);
            if ((await scanner.ScanAsync(content, bytes.Length, token)).Result != EvidenceScanResult.Limpio)
                throw new DirectionPlanException("TASK_FILE_SCAN_FAILED", 422);
            media = OperationalExcel.MediaType;
        }
        if (assignment.Requirement == "DOCUMENTO")
        {
            if (file is null || file.Length is < 1 or > EvidenceFileLimits.MaximumBytes)
                throw new DirectionPlanException("TASK_FILE_REQUIRED", 422);
            await using var content = file.OpenReadStream();
            var result = await validator.ValidateAsync(content, file.ContentType, file.FileName, token);
            if (!result.IsValid || result.File is null) throw new DirectionPlanException("TASK_FILE_INVALID", 422);
            await using var valid = result.File;
            if (assignment.Requirement == "FOTOGRAFIA" && valid.MediaType is not (EvidenceMediaType.Jpeg or EvidenceMediaType.Png) ||
                assignment.Requirement == "DOCUMENTO" && valid.MediaType != EvidenceMediaType.Pdf)
                throw new DirectionPlanException("TASK_FILE_INVALID", 422);
            var scan = await scanner.ScanAsync(valid.Content, valid.SizeBytes, token);
            if (scan.Result != EvidenceScanResult.Limpio) throw new DirectionPlanException("TASK_FILE_SCAN_FAILED", 422);
            valid.Content.Position = 0;
            using var buffer = new MemoryStream(); await valid.Content.CopyToAsync(buffer, token);
            bytes = buffer.ToArray(); media = valid.MediaType.ToMediaType(); name = Path.GetFileName(file.FileName);
        }
        await using var tx = await db.Database.BeginTransactionAsync(token);
        await LockAsync(db, token); person = await PersonAsync(actor, token);
        var current = await db.OperationalAssignments.SingleOrDefaultAsync(a => a.Id == id && a.PersonId == person, token)
            ?? throw new DirectionPlanException("TASK_NOT_FOUND", 404);
        await RequireIndependentActionAsync(id, token);
        if (current.CompletedAt is null)
        {
            if (current.StartsAt > clock.UtcNow) throw new DirectionPlanException("TASK_NOT_STARTED", 422);
            if (current.GoalUnits > 0 && current.ProgressUnits < current.GoalUnits) throw new DirectionPlanException("TASK_GOAL_PENDING", 422);
            if (current.Requirement == "FOTOGRAFIA")
            {
                var photographs = await AttachPhotographsAsync(actor, current, captureSessionId!.Value, photographIds!, token);
                name = photographs[0].FileName;
            }
            if (current.Requirement == "CAPTURA_PANTALLA")
            {
                if (verifiedScreenshots.Count < current.MinimumPhotos || verifiedScreenshots.Count > current.MaximumPhotos ||
                    verifiedScreenshots.Sum(f => f.Bytes.LongLength) > current.MaximumPhotoBytes)
                    throw new DirectionPlanException("TASK_PHOTO_LIMIT", 422);
                for (var i = 0; i < verifiedScreenshots.Count; i++)
                {
                    var image = verifiedScreenshots[i];
                    var hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(image.Bytes));
                    db.OperationalPhotographs.Add(new() { Id = Guid.CreateVersion7(), AssignmentId = current.Id,
                        Position = i, FileName = image.Name, ContentType = image.Media, Content = image.Bytes,
                        ReceivedBytes = image.Bytes.LongLength, ReceivedSha256 = hash, Sha256 = hash,
                        CreatedAt = clock.UtcNow, AttachedAt = clock.UtcNow });
                }
            }
            current.CompletedAt = clock.UtcNow; current.Comment = comment;
            current.CompletedBy = actor; current.CompletionKind = "ENTREGA";
            if (bytes is not null)
                db.OperationalAttachments.Add(new() { AssignmentId = current.Id, FileName = name!,
                    ContentType = media!, Content = bytes, CreatedAt = clock.UtcNow });
            current.Conclusion = conclusion; current.ReviewStatus = "PENDIENTE_REVISION"; current.Revision++;
            Audit(actor, "OPERATIONAL_TASK_COMPLETED", id, new { current.PlanId, current.CompletedAt, current.Requirement });
            await db.SaveChangesAsync(token);
        }
        await tx.CommitAsync(token);
    }

    public async Task ProgressAsync(Guid actor, Guid id, long revision, int units, string? note, CancellationToken token)
    {
        if (units <= 0 || string.IsNullOrWhiteSpace(note) || note.Trim().Length > 1000)
            throw new DirectionPlanException("TASK_PROGRESS_INVALID", 422);
        await using var tx = await db.Database.BeginTransactionAsync(token);
        await LockAsync(db, token);
        var person = await PersonAsync(actor, token);
        var task = await db.OperationalAssignments.SingleOrDefaultAsync(a => a.Id == id && a.PersonId == person, token)
            ?? throw new DirectionPlanException("TASK_NOT_FOUND", 404);
        await RequireIndependentActionAsync(id, token);
        if (task.StartsAt > clock.UtcNow) throw new DirectionPlanException("TASK_NOT_STARTED", 422);
        if (task.Revision != revision || task.CompletedAt != null) throw new DirectionPlanException("TASK_CONFLICT", 409);
        if (task.GoalUnits == 0 || units > task.GoalUnits - task.ProgressUnits) throw new DirectionPlanException("TASK_PROGRESS_INVALID", 422);
        task.ProgressUnits += units; task.Revision++;
        db.OperationalProgressEntries.Add(new() { Id = Guid.CreateVersion7(), AssignmentId = id,
            ActorId = actor, OccurredAt = clock.UtcNow, Units = units, Note = note.Trim() });
        Audit(actor, "OPERATIONAL_TASK_PROGRESS", id, new { units, note, task.ProgressUnits, task.Revision });
        await db.SaveChangesAsync(token); await tx.CommitAsync(token);
    }

    public async Task<IReadOnlyList<OperationalProgressEntry>> ProgressHistoryAsync(Guid actor, Guid id, CancellationToken token)
    {
        var person = await PersonAsync(actor, token);
        if (!await db.OperationalAssignments.AnyAsync(a => a.Id == id && a.PersonId == person, token))
            await new OperationalSupervisionService(db, clock, direction).AuthorizeAssignmentAsync(actor, id, token);
        return await db.OperationalProgressEntries.AsNoTracking().Where(p => p.AssignmentId == id).OrderBy(p => p.OccurredAt).ToArrayAsync(token);
    }

    public async Task<OperationalAssignment> FileAsync(Guid actor, Guid id, CancellationToken token)
    {
        var person = await PersonAsync(actor, token);
        var own = await db.OperationalAssignments.AnyAsync(a => a.Id == id && a.PersonId == person, token);
        if (!own && !await new OperationalProcessService(db, direction, clock, validator, scanner).CanReadDocumentAsync(actor, id, token))
            await new OperationalSupervisionService(db, clock, direction).AuthorizeAssignmentAsync(actor, id, token);
        var photo = await db.OperationalPhotographs.AsNoTracking().Where(p => p.AssignmentId == id && p.AttachedAt != null).OrderBy(p => p.Position).FirstOrDefaultAsync(token);
        if (photo is not null) return new OperationalAssignment { Id = id, FileContent = photo.Content, ContentType = photo.ContentType, FileName = photo.FileName };
        return await db.OperationalAttachments.AsNoTracking().Where(a => a.AssignmentId == id)
            .Select(a => new OperationalAssignment { Id = id, FileContent = a.Content, ContentType = a.ContentType, FileName = a.FileName }).SingleOrDefaultAsync(token)
            ?? throw new DirectionPlanException("TASK_NOT_FOUND", 404);
    }

    private void Audit(Guid actor, string action, Guid id, object data) => db.AuditEvents.Add(new AuditEvent {
        Id = Guid.CreateVersion7(), OccurredAt = clock.UtcNow, ActorUserId = actor, ActorType = "USER",
        Action = action, ResourceType = "OPERATIONAL_TASK", ResourceId = id, BranchId = BranchScope.LorettaId,
        CorrelationId = Guid.CreateVersion7(), Outcome = "SUCCESS", AfterData = JsonSerializer.SerializeToDocument(data)
    });

    internal Task RequireIndependentActionAsync(Guid id, CancellationToken token) => RequireIndependentActionAsync(db, id, token);
    internal static async Task RequireIndependentActionAsync(SgolDbContext context, Guid id, CancellationToken token)
    {
        if (await context.Set<OperationalSequenceAttempt>().AnyAsync(a => a.AssignmentId == id, token))
            throw new DirectionPlanException("PROCESS_USE_ACTION", 409);
    }
    internal async Task<OperationalAttachment> ProcessPdfAsync(IFormFile file, CancellationToken token)
    {
        var verified = await VerifiedFileAsync(file, "DOCUMENTO", token);
        return new OperationalAttachment { FileName = verified.Name, ContentType = verified.Media, Content = verified.Bytes };
    }
}
