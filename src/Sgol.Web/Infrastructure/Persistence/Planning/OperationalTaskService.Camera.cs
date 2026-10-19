using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Sgol.Evidence.Contracts;
using Sgol.Evidence.Technical;
using Sgol.Planning.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed partial class OperationalTaskService
{
    public async Task<OperationalCaptureSession> BeginCaptureAsync(Guid actor, Guid id, CancellationToken token)
    {
        await using var tx = await db.Database.BeginTransactionAsync(token);
        await LockAsync(db, token);
        var assignment = await CaptureAssignmentAsync(actor, id, token);
        if (await db.Set<OperationalCaptureSession>().CountAsync(x => x.ActorId == actor && x.CreatedAt > clock.UtcNow.AddHours(-1), token) >= 30)
            throw new DirectionPlanException("TASK_CAMERA_RATE_LIMIT", 429);
        var session = new OperationalCaptureSession { Id = Guid.NewGuid(), AssignmentId = id, ActorId = actor,
            CreatedAt = clock.UtcNow, ExpiresAt = clock.UtcNow.AddMinutes(30) };
        db.Add(session);
        Audit(actor, "OPERATIONAL_CAMERA_OPENED", assignment.Id, new { session.Id, session.CreatedAt, session.ExpiresAt });
        await db.SaveChangesAsync(token); await tx.CommitAsync(token);
        return session;
    }

    private async Task<OperationalAssignment> CaptureAssignmentAsync(Guid actor, Guid id, CancellationToken token)
    {
        if (configuration?.GetValue<bool?>("Evidence:Camera:Enabled") == false) throw new DirectionPlanException("TASK_CAMERA_DISABLED", 503);
        var person = await PersonAsync(actor, token);
        var assignment = await db.OperationalAssignments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.PersonId == person, token)
            ?? throw new DirectionPlanException("TASK_NOT_FOUND", 404);
        if (assignment.Requirement != "FOTOGRAFIA" || assignment.CompletedAt is not null ||
            assignment.ReviewStatus is "CONCLUIDA" or "NO_SATISFECHA" or "REASIGNADA") throw new DirectionPlanException("TASK_CONFLICT", 409);
        if (assignment.StartsAt > clock.UtcNow) throw new DirectionPlanException("TASK_NOT_STARTED", 422);
        if (assignment.GoalUnits > assignment.ProgressUnits) throw new DirectionPlanException("TASK_GOAL_PENDING", 422);
        return assignment;
    }

    private async Task<OperationalCaptureSession> RequireCaptureAsync(Guid actor, Guid assignment, Guid session, CancellationToken token)
    {
        var capture = await db.Set<OperationalCaptureSession>().SingleOrDefaultAsync(x => x.Id == session && x.ActorId == actor && x.AssignmentId == assignment, token);
        if (capture is null || capture.ConsumedAt is not null || capture.ExpiresAt <= clock.UtcNow)
            throw new DirectionPlanException("TASK_CAMERA_EXPIRED", 410);
        return capture;
    }

    public async Task<OperationalPhotographDetails> UploadPhotographAsync(Guid actor, Guid id, Guid sessionId,
        Guid photographId, IFormFile file, CancellationToken token)
    {
        if (photographId == Guid.Empty || file.Length is < 1 or > EvidenceFileLimits.MaximumBytes || file.ContentType != "image/jpeg")
            throw new DirectionPlanException("TASK_FILE_INVALID", 422);
        await using (var tx = await db.Database.BeginTransactionAsync(token))
        {
            await LockAsync(db, token);
            var assignment = await CaptureAssignmentAsync(actor, id, token);
            var session = await RequireCaptureAsync(actor, id, sessionId, token);
            var existing = await db.Set<OperationalPhotograph>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == photographId, token);
            if (existing is not null)
            {
                if (existing.AssignmentId != id || existing.CaptureSessionId != sessionId) throw new DirectionPlanException("TASK_CONFLICT", 409);
                await using var incoming = file.OpenReadStream();
                if (existing.ReceivedBytes != file.Length || existing.ReceivedSha256 != Convert.ToHexStringLower(await SHA256.HashDataAsync(incoming, token)))
                    throw new DirectionPlanException("TASK_CONFLICT", 409);
                return PhotoDetails(existing);
            }
            var staged = db.Set<OperationalPhotograph>().Where(x => x.CaptureSessionId == sessionId);
            if (session.UploadAttempts >= Math.Max(10, assignment.MaximumPhotos * 2) || await staged.CountAsync(token) >= assignment.MaximumPhotos ||
                (await staged.SumAsync(x => (long?)x.ReceivedBytes, token) ?? 0) + file.Length > assignment.MaximumPhotoBytes)
                throw new DirectionPlanException("TASK_PHOTO_LIMIT", 422);
            var attempts = await db.Set<OperationalCaptureSession>().Where(x => x.ActorId == actor && x.CreatedAt > clock.UtcNow.AddHours(-1))
                .SumAsync(x => x.UploadAttempts, token);
            if (attempts >= 120) throw new DirectionPlanException("TASK_CAMERA_RATE_LIMIT", 429);
            session.UploadAttempts++;
            await db.SaveChangesAsync(token); await tx.CommitAsync(token);
        }

        byte[] normalized; string receivedHash; long receivedBytes;
        await using (var incoming = file.OpenReadStream())
        {
            var validation = await validator.ValidateAsync(incoming, "image/jpeg", "capture.jpg", token);
            if (!validation.IsValid || validation.File is null) throw new DirectionPlanException("TASK_FILE_INVALID", 422);
            await using var valid = validation.File;
            if (valid.MediaType != EvidenceMediaType.Jpeg || (await scanner.ScanAsync(valid.Content, valid.SizeBytes, token)).Result != EvidenceScanResult.Limpio)
                throw new DirectionPlanException("TASK_FILE_SCAN_FAILED", 422);
            receivedHash = valid.Sha256; receivedBytes = valid.SizeBytes;
            try { normalized = await PhotographNormalizer.NormalizeAsync(valid.Content, token); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception) { throw new DirectionPlanException("TASK_FILE_INVALID", 422); }
        }
        await using var normalizedStream = new MemoryStream(normalized, false);
        if ((await scanner.ScanAsync(normalizedStream, normalized.Length, token)).Result != EvidenceScanResult.Limpio)
            throw new DirectionPlanException("TASK_FILE_SCAN_FAILED", 422);

        await using var finalTx = await db.Database.BeginTransactionAsync(token);
        await LockAsync(db, token);
        var current = await CaptureAssignmentAsync(actor, id, token);
        await RequireCaptureAsync(actor, id, sessionId, token);
        var entries = await db.Set<OperationalPhotograph>().Where(x => x.CaptureSessionId == sessionId)
            .Select(x => new { x.Id, x.ReceivedBytes, Length = x.Content.Length, x.ReceivedSha256 }).ToArrayAsync(token);
        if (entries.Any(x => x.Id == photographId))
        {
            var existing = await db.Set<OperationalPhotograph>().SingleAsync(x => x.Id == photographId, token);
            if (existing.ReceivedSha256 != receivedHash) throw new DirectionPlanException("TASK_CONFLICT", 409);
            return PhotoDetails(existing);
        }
        if (entries.Length >= current.MaximumPhotos || entries.Sum(x => x.ReceivedBytes) + receivedBytes > current.MaximumPhotoBytes ||
            entries.Sum(x => (long)x.Length) + normalized.Length > current.MaximumPhotoBytes)
            throw new DirectionPlanException("TASK_PHOTO_LIMIT", 422);
        var photograph = new OperationalPhotograph { Id = photographId, AssignmentId = id, CaptureSessionId = sessionId,
            CreatedAt = clock.UtcNow, Content = normalized, ReceivedBytes = receivedBytes, ReceivedSha256 = receivedHash,
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(normalized)), FileName = $"foto-{photographId:N}.jpg" };
        db.Add(photograph);
        Audit(actor, "OPERATIONAL_PHOTOGRAPH_VERIFIED", id, new { photograph.Id, sessionId, receivedBytes, photograph.ReceivedSha256, photograph.Sha256 });
        await db.SaveChangesAsync(token); await finalTx.CommitAsync(token);
        return PhotoDetails(photograph);
    }

    public async Task<OperationalPhotographDetails> PhotographStatusAsync(Guid actor, Guid id, Guid sessionId, Guid photoId, CancellationToken token)
    {
        await CaptureAssignmentAsync(actor, id, token);
        await RequireCaptureAsync(actor, id, sessionId, token);
        var photo = await db.Set<OperationalPhotograph>().AsNoTracking().Where(x => x.Id == photoId && x.AssignmentId == id && x.CaptureSessionId == sessionId)
            .Select(x => new OperationalPhotographDetails(x.Id, x.FileName, x.ContentType, x.Position, (long)x.Content.Length)).SingleOrDefaultAsync(token);
        return photo ?? throw new DirectionPlanException("TASK_PHOTO_NOT_FOUND", 404);
    }

    private async Task<List<OperationalPhotographDetails>> AttachPhotographsAsync(Guid actor, OperationalAssignment assignment, Guid sessionId,
        IReadOnlyList<Guid> ids, CancellationToken token)
    {
        if (configuration?.GetValue<bool?>("Evidence:Camera:Enabled") == false) throw new DirectionPlanException("TASK_CAMERA_DISABLED", 503);
        if (ids.Count < assignment.MinimumPhotos || ids.Count > assignment.MaximumPhotos || ids.Any(x => x == Guid.Empty) || ids.Distinct().Count() != ids.Count)
            throw new DirectionPlanException("TASK_PHOTO_LIMIT", 422);
        var session = await RequireCaptureAsync(actor, assignment.Id, sessionId, token);
        // PostgreSQL computes byte lengths; attaching an already verified upload never downloads the image again.
        var photos = await db.Set<OperationalPhotograph>().Where(x => ids.Contains(x.Id) && x.AssignmentId == assignment.Id && x.CaptureSessionId == sessionId && x.AttachedAt == null)
            .Select(x => new { x.Id, x.FileName, x.ContentType, x.ReceivedBytes, SizeBytes = (long)x.Content.Length }).ToListAsync(token);
        if (photos.Count != ids.Count || photos.Sum(x => x.ReceivedBytes) > assignment.MaximumPhotoBytes || photos.Sum(x => x.SizeBytes) > assignment.MaximumPhotoBytes)
            throw new DirectionPlanException("TASK_PHOTO_LIMIT", 422);
        photos = ids.Select(id => photos.Single(x => x.Id == id)).ToList();
        for (var i = 0; i < photos.Count; i++)
        {
            var photoId = photos[i].Id; var position = i;
            await db.OperationalPhotographs.Where(x => x.Id == photoId).ExecuteUpdateAsync(
                s => s.SetProperty(x => x.Position, position).SetProperty(x => x.AttachedAt, clock.UtcNow), token);
        }
        session.ConsumedAt = clock.UtcNow;
        Audit(actor, "OPERATIONAL_PHOTOGRAPHS_ATTACHED", assignment.Id, new { sessionId, photographIds = ids });
        return photos.Select((p, i) => new OperationalPhotographDetails(p.Id, p.FileName, p.ContentType, i, p.SizeBytes)).ToList();
    }

    public async Task<IReadOnlyList<OperationalPhotographDetails>> PhotographsAsync(Guid actor, Guid id, CancellationToken token)
    {
        await AuthorizePhotoReadAsync(actor, id, token);
        return await db.Set<OperationalPhotograph>().AsNoTracking().Where(x => x.AssignmentId == id && x.AttachedAt != null).OrderBy(x => x.Position)
            .Select(x => new OperationalPhotographDetails(x.Id, x.FileName, x.ContentType, x.Position, (long)x.Content.Length)).ToArrayAsync(token);
    }

    public async Task DeleteStagedPhotographAsync(Guid actor, Guid id, Guid sessionId, Guid photoId, CancellationToken token)
    {
        await using var tx = await db.Database.BeginTransactionAsync(token);
        await LockAsync(db, token);
        await CaptureAssignmentAsync(actor, id, token);
        await RequireCaptureAsync(actor, id, sessionId, token);
        await db.Set<OperationalPhotograph>().Where(x => x.Id == photoId && x.AssignmentId == id && x.CaptureSessionId == sessionId && x.AttachedAt == null).ExecuteDeleteAsync(token);
        await tx.CommitAsync(token);
    }

    private async Task AuthorizePhotoReadAsync(Guid actor, Guid id, CancellationToken token)
    {
        var person = await PersonAsync(actor, token);
        if (!await db.OperationalAssignments.AnyAsync(x => x.Id == id && x.PersonId == person, token))
            await new OperationalSupervisionService(db, clock, direction).AuthorizeAssignmentAsync(actor, id, token);
    }

    public async Task<OperationalPhotograph> PhotographAsync(Guid actor, Guid id, Guid photoId, CancellationToken token)
    {
        await AuthorizePhotoReadAsync(actor, id, token);
        return await db.Set<OperationalPhotograph>().AsNoTracking().SingleOrDefaultAsync(x => x.AssignmentId == id && x.Id == photoId && x.AttachedAt != null, token)
            ?? throw new DirectionPlanException("TASK_NOT_FOUND", 404);
    }
    private static OperationalPhotographDetails PhotoDetails(OperationalPhotograph photo) => new(photo.Id, photo.FileName, photo.ContentType, photo.Position, photo.Content.LongLength);
}
