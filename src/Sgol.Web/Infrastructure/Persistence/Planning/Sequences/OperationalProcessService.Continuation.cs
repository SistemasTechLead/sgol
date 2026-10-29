using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Sgol.Evidence.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed record ProcessPreservedPhoto(Guid AttemptId, Guid AssignmentId, Guid PhotoId, string Name);

public sealed partial class OperationalProcessService
{
    private async Task<OperationalSequenceAttempt[]> Continuations(OperationalSequenceAttempt attempt, CancellationToken ct)
    {
        var result = new List<OperationalSequenceAttempt>(); var visited = new HashSet<Guid> { attempt.Id };
        while (attempt.ContinuationAttemptId is {} id && visited.Add(id)) {
            attempt = await db.Set<OperationalSequenceAttempt>().AsNoTracking().SingleAsync(a => a.Id == id && a.RunId == attempt.RunId && a.StageId == attempt.StageId, ct);
            result.Add(attempt);
        }
        return result.ToArray();
    }

    public async Task<OperationalPhotograph> PreservedPhotoAsync(Guid actor, Guid currentAttemptId, Guid photoId, CancellationToken ct)
    {
        var person = await Person(actor, ct);
        var current = await db.Set<OperationalSequenceAttempt>().AsNoTracking().SingleOrDefaultAsync(a => a.Id == currentAttemptId, ct) ?? throw Error("TASK_NOT_FOUND",404);
        var owner = await db.OperationalAssignments.AnyAsync(a => a.Id == current.AssignmentId && a.PersonId == person, ct);
        if (!owner) throw Error("ACCESO_DENEGADO",403);
        var assignments = (await Continuations(current, ct)).Select(a => a.AssignmentId).ToArray();
        return await db.OperationalPhotographs.AsNoTracking().SingleOrDefaultAsync(p => p.Id == photoId && assignments.Contains(p.AssignmentId), ct) ?? throw Error("TASK_NOT_FOUND",404);
    }

    private async Task<(IFormFile? File, IReadOnlyList<IFormFile>? Screenshots, Guid? Session, IReadOnlyList<Guid>? Photos)> ContinueEvidence(
        Guid actor, OperationalSequenceRun run, OperationalSequenceStage stage, OperationalSequenceAttempt attempt, OperationalAssignment assignment,
        IFormFile? file, IReadOnlyList<IFormFile>? screenshots, Guid? session, IReadOnlyList<Guid>? photos, CancellationToken ct)
    {
        if (file != null || screenshots?.Count > 0 || photos?.Count > 0 || attempt.ContinuationAttemptId == null) return (file,screenshots,session,photos);
        var history = await Continuations(attempt, ct);
        foreach (var prior in history.Where(a => a.AssignmentId != null)) {
            var original = await db.OperationalAssignments.IgnoreQueryFilters().AsNoTracking().SingleAsync(a => a.Id == prior.AssignmentId, ct);
            if (original.Requirement != assignment.Requirement) continue;
            var attachment = await db.OperationalAttachments.AsNoTracking().SingleOrDefaultAsync(a => a.AssignmentId == original.Id, ct);
            if (attachment != null && assignment.Requirement is "DOCUMENTO" or "EXCEL") {
                file = new FormFile(new MemoryStream(attachment.Content),0,attachment.Content.Length,"Document",attachment.FileName) { Headers=new HeaderDictionary(),ContentType=attachment.ContentType };
                db.Add(Event(run,stage,attempt,"ASIGNACION",$"continue-work:{attempt.Id:N}",actor,clock.UtcNow,null,$"Material conservado reutilizado del intento {prior.Number} de esta tarea. Se conserva el original y se valida la nueva entrega."));
                AuditContinuation(actor,run,attempt,prior,[],[]);
                return (file,screenshots,session,photos);
            }
            if (assignment.Requirement is not ("FOTOGRAFIA" or "CAPTURA_PANTALLA")) continue;
            var saved = await db.OperationalPhotographs.AsNoTracking().Where(p => p.AssignmentId == original.Id).OrderBy(p=>p.CreatedAt).ThenBy(p=>p.Position).ToArrayAsync(ct);
            if (saved.Length == 0) continue;
            if (saved.Length < assignment.MinimumPhotos || saved.Length > assignment.MaximumPhotos || saved.Sum(p=>(long)p.Content.Length) > assignment.MaximumPhotoBytes) throw Error("TASK_PHOTO_LIMIT");
            if (assignment.Requirement == "CAPTURA_PANTALLA")
                screenshots = saved.Select(p => (IFormFile)new FormFile(new MemoryStream(p.Content),0,p.Content.Length,"screenshots",p.FileName) { Headers=new HeaderDictionary(),ContentType=p.ContentType }).ToArray();
            else {
                // A new receipt session belongs to this submission; never reopen an expired session.
                var capture = new OperationalCaptureSession { Id=Guid.CreateVersion7(),AssignmentId=assignment.Id,ActorId=actor,CreatedAt=clock.UtcNow,ExpiresAt=clock.UtcNow.AddMinutes(30) };
                db.Add(capture); var ids=new List<Guid>();
                foreach(var photo in saved) {
                    await using var bytes=new MemoryStream(photo.Content);
                    var validation=await validator.ValidateAsync(bytes,photo.ContentType,photo.FileName,ct);
                    if (!validation.IsValid || validation.File == null) throw Error("TASK_FILE_INVALID");
                    await using var valid=validation.File;
                    if ((await scanner.ScanAsync(valid.Content,valid.SizeBytes,ct)).Result != EvidenceScanResult.Limpio) throw Error("TASK_FILE_INVALID");
                    var copy=new OperationalPhotograph { Id=Guid.CreateVersion7(),AssignmentId=assignment.Id,CaptureSessionId=capture.Id,Position=ids.Count,FileName=photo.FileName,ContentType=photo.ContentType,Content=photo.Content.ToArray(),ReceivedBytes=photo.Content.Length,ReceivedSha256=Convert.ToHexStringLower(SHA256.HashData(photo.Content)),Sha256=photo.Sha256,CreatedAt=clock.UtcNow };
                    db.Add(copy);ids.Add(copy.Id);
                }
                session=capture.Id;photos=ids;await db.SaveChangesAsync(ct);
            }
            db.Add(Event(run,stage,attempt,"ASIGNACION",$"continue-work:{attempt.Id:N}",actor,clock.UtcNow,null,
                $"Material conservado reutilizado del intento {prior.Number} de esta tarea. Se conservan originales y autoría; nueva entrega de {assignment.PersonName}."));
            AuditContinuation(actor,run,attempt,prior,saved.Select(p=>p.Id).ToArray(),photos?.ToArray()??[]);
            return (file,screenshots,session,photos);
        }
        return (file,screenshots,session,photos);
    }
    private void AuditContinuation(Guid actor, OperationalSequenceRun run, OperationalSequenceAttempt current, OperationalSequenceAttempt prior, Guid[] originalPhotos, Guid[] newPhotos) =>
        db.AuditEvents.Add(new Sgol.Web.Infrastructure.Persistence.Auditing.AuditEvent { Id=Guid.CreateVersion7(),ActorUserId=actor,ActorType="USER",OccurredAt=clock.UtcNow,
            ResourceType="OPERATIONAL_PROCESS",ResourceId=run.Id,BranchId=Sgol.Organization.Contracts.BranchScope.LorettaId,Action="PROCESS_WORK_CONTINUED",Outcome="SUCCESS",CorrelationId=current.Id,
            AfterData=System.Text.Json.JsonSerializer.SerializeToDocument(new { OriginalAttemptId=prior.Id,OriginalAssignmentId=prior.AssignmentId,AttemptId=current.Id,OriginalPhotos=originalPhotos,NewPhotos=newPhotos }) });

}
