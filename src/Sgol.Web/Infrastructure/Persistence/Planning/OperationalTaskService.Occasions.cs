using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgol.Evidence.Contracts;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Sgol.Planning.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed class OperationalOccasion
{
    public Guid Id { get; set; }
    public Guid PlanId { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string Name { get; set; } = "";
    public string Instructions { get; set; } = "";
    public string PublicationText { get; set; } = "";
    public string Channels { get; set; } = "";
    public string RequestHash { get; set; } = "";
}

public sealed class OperationalReferenceFile
{
    public Guid Id { get; set; }
    public Guid OccasionId { get; set; }
    public int Position { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public byte[] Content { get; set; } = [];
}

public sealed record OccasionInput(Guid TaskId, long TaskRevision, string Name, string Instructions,
    string PublicationText, string Channels, DateTimeOffset StartsAt, DateTimeOffset DueAt,
    Guid[] PersonIds, bool Automatic = false, Guid? PlanId = null, string? SchedulingFingerprint = null, DateTimeOffset? SchedulingNotBefore = null);
public sealed record ReferenceFileDetails(Guid Id, string FileName, string ContentType);
public sealed record OccasionDetails(string Name, string Instructions, string PublicationText,
    string Channels, IReadOnlyList<ReferenceFileDetails> Files);

internal sealed class OperationalOccasionConfiguration : IEntityTypeConfiguration<OperationalOccasion>
{
    public void Configure(EntityTypeBuilder<OperationalOccasion> b)
    {
        b.ToTable("operational_occasion"); b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.Id, x.PlanId });
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Name).HasMaxLength(120);
        b.Property(x => x.Instructions).HasMaxLength(4000);
        b.Property(x => x.PublicationText).HasMaxLength(4000);
        b.Property(x => x.Channels).HasMaxLength(500);
        b.Property(x => x.RequestHash).HasMaxLength(64);
        b.HasOne<DirectionPreparedPlan>().WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class OperationalReferenceFileConfiguration : IEntityTypeConfiguration<OperationalReferenceFile>
{
    public void Configure(EntityTypeBuilder<OperationalReferenceFile> b)
    {
        b.ToTable("operational_reference_file"); b.HasKey(x => x.Id);
        b.Property(x => x.FileName).HasMaxLength(255); b.Property(x => x.ContentType).HasMaxLength(100);
        b.HasIndex(x => new { x.OccasionId, x.Position }).IsUnique();
        b.HasOne<OperationalOccasion>().WithMany().HasForeignKey(x => x.OccasionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed partial class OperationalTaskService
{
    public async Task<CapacityWorker[]> OccasionWorkersAsync(Guid actor, CancellationToken token)
    {
        await direction.AuthorizeAsync(actor, token);
        return await OperationalCapacityService.WorkersAsync(db, clock.UtcNow, token);
    }

    public async Task<Guid> AssignOccasionAsync(Guid actor, Guid key, OccasionInput input,
        IReadOnlyList<IFormFile> files, CancellationToken token)
    {
        var actorRole = await AuthorizeOccasionalAsync(actor, token);
        if (actorRole != CanonicalRole.Direction && (input.PlanId != null || input.Automatic))
            throw new DirectionPlanException("ACCESO_DENEGADO", 403);
        if (key == Guid.Empty || input.TaskId == Guid.Empty || input.PersonIds is null ||
            input.PersonIds.Length > 1000 || input.PersonIds.Distinct().Count() != input.PersonIds.Length ||
            input.PersonIds.Any(x => x == Guid.Empty) || input.Automatic && input.PersonIds.Length != 0 ||
            !input.Automatic && input.PersonIds.Length == 0 || files.Count > 5 ||
            files.Sum(f => f.Length) > 31457280)
            throw new DirectionPlanException("OCCASION_INVALID", 422);
        input = input with { Name = (input.Name ?? "").Trim(), Instructions = (input.Instructions ?? "").Trim(),
            PublicationText = (input.PublicationText ?? "").Trim(), Channels = (input.Channels ?? "").Trim(),
            StartsAt = input.StartsAt.ToUniversalTime(), DueAt = input.DueAt.ToUniversalTime(),
            PersonIds = input.PersonIds.Order().ToArray() };
        if (input.Name.Length is < 1 or > 120 || input.Name.Any(char.IsControl) ||
            input.Instructions.Length is < 1 or > 4000 || input.PublicationText.Length > 4000 || input.Channels.Length > 500 ||
            input.DueAt <= input.StartsAt || input.DueAt - input.StartsAt > TimeSpan.FromDays(365))
            throw new DirectionPlanException("OCCASION_INVALID", 422);

        // Validate and scan material before taking the branch lock; nothing is stored on failure.
        var material = new List<OperationalReferenceFile>();
        foreach (var file in files)
        {
            var verified = await VerifiedFileAsync(file, null, token);
            material.Add(new() { Id = Guid.CreateVersion7(), OccasionId = key, Position = material.Count,
                FileName = verified.Name, ContentType = verified.Media, Content = verified.Bytes });
        }
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new {
            input, files = material.Select(f => new { f.FileName, f.ContentType, hash = Convert.ToHexString(SHA256.HashData(f.Content)) })
        })));
        await using var tx = await db.Database.BeginTransactionAsync(token);
        await LockAsync(db, token); actorRole = await AuthorizeOccasionalAsync(actor, token);
        if (actorRole != CanonicalRole.Direction && (input.PlanId != null || input.Automatic))
            throw new DirectionPlanException("ACCESO_DENEGADO", 403);
        var previous = await db.Set<OperationalOccasion>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == key, token);
        if (previous is not null)
        {
            if (previous.CreatedBy != actor || previous.RequestHash != hash) throw new DirectionPlanException("OCCASION_CONFLICT", 409);
            await tx.CommitAsync(token); return previous.PlanId;
        }
        var now = clock.UtcNow;
        if (input.StartsAt < OperationalSchedule.Instant(WeekContract.LocalToday(now)) || input.DueAt <= now ||
            input.StartsAt > now.AddDays(365)) throw new DirectionPlanException("OCCASION_INVALID", 422);
        var task = await db.OperationalTasks.AsNoTracking().SingleOrDefaultAsync(t => t.Id == input.TaskId && t.ProcessId == OperationalSequence.GeneralId && t.Active && !t.Deleted && t.PeriodType == "OCASIONAL", token)
            ?? throw new DirectionPlanException("TASK_NOT_FOUND", 404);
        if (task.Revision != input.TaskRevision) throw new DirectionPlanException("TASK_CONFLICT", 409);
        var specialty = task.SpecializationId is { } sid ? await db.Set<Sgol.Web.Infrastructure.Persistence.Organization.Specialization>()
            .AsNoTracking().SingleOrDefaultAsync(s => s.Id == sid && s.Active, token) : null;
        if (task.SpecializationId != null && specialty == null) throw new DirectionPlanException("TASK_SPECIALIZATION_INVALID", 422);
        DirectionPreparedPlan plan;
        if (input.PlanId is { } planId)
        {
            plan = await db.DirectionPreparedPlans.SingleOrDefaultAsync(p => p.Id == planId && p.BranchId == BranchScope.LorettaId && p.Status == "ASIGNADO" && p.PeriodType != "OCASIONAL", token)
                ?? throw new DirectionPlanException("DIRECTION_PLAN_NOT_FOUND", 404);
            var day = WeekContract.LocalToday(input.StartsAt);
            if (day < plan.StartsOn || day > plan.EndsOn) throw new DirectionPlanException("OCCASION_INVALID", 422);
        }
        else
        {
            plan = DirectionPreparedPlan.ForOccasion(Guid.CreateVersion7(), BranchScope.LorettaId, actor, now,
                input.Name, WeekContract.LocalToday(input.StartsAt), WeekContract.LocalToday(input.StartsAt));
        }
        var policy = input.PlanId == null ? await OperationalSchedulingPolicyService.CurrentAsync(db, token) : plan.SchedulingPolicyId is {} policyId ? await db.Set<OperationalSchedulingPolicy>().AsNoTracking().SingleAsync(p => p.Id == policyId, token) : null;
        if (input.PlanId == null && policy != null) plan.CaptureSchedulingPolicy(policy.Id);
        var ledger = await OperationalCapacityService.LedgerAsync(db, now, token);
        var assignments = OccasionAssignments(task, plan.Id, key, policy, actorRole, input, ledger, now, specialty?.Name);
        if (policy != null && (input.SchedulingFingerprint != OccasionFingerprint(task, policy, input, ledger, assignments) || assignments.Any(a => a.StartsAt < clock.UtcNow))) throw new DirectionPlanException("PLAN_CAPACITY_STALE", 409);
        if (input.PlanId == null)
        {
            db.DirectionPreparedPlans.Add(plan);
            db.DirectionPreparedPlanItems.Add(new(plan.Id, task.TaskCode, task.Name, 0));
        }
        db.Set<OperationalOccasion>().Add(new() { Id = key, PlanId = plan.Id, CreatedBy = actor, CreatedAt = now,
            Name = input.Name, Instructions = input.Instructions, PublicationText = input.PublicationText, Channels = input.Channels, RequestHash = hash });
        db.Set<OperationalReferenceFile>().AddRange(material);
        db.OperationalAssignments.AddRange(assignments);
        Audit(actor, "OPERATIONAL_OCCASION_ASSIGNED", key, new { PlanId = plan.Id, TaskId = task.Id, input.Name, input.StartsAt, input.DueAt,
            input.Automatic, Recipients = assignments.Select(a => a.PersonId), MaterialCount = material.Count });
        await db.SaveChangesAsync(token); await tx.CommitAsync(token);
        return plan.Id;
    }

    internal async Task<(byte[] Bytes, string Media, string Name)> VerifiedFileAsync(IFormFile file, string? requirement, CancellationToken token)
    {
        if (file.Length is < 1 or > EvidenceFileLimits.MaximumBytes) throw new DirectionPlanException("TASK_FILE_REQUIRED", 422);
        await using var content = file.OpenReadStream();
        var result = await validator.ValidateAsync(content, file.ContentType, file.FileName, token);
        if (!result.IsValid || result.File is null) throw new DirectionPlanException("TASK_FILE_INVALID", 422);
        await using var valid = result.File;
        if (requirement == "CAPTURA_PANTALLA" && valid.MediaType is not (EvidenceMediaType.Jpeg or EvidenceMediaType.Png) ||
            requirement == "DOCUMENTO" && valid.MediaType != EvidenceMediaType.Pdf ||
            valid.MediaType is not (EvidenceMediaType.Jpeg or EvidenceMediaType.Png or EvidenceMediaType.Pdf))
            throw new DirectionPlanException("TASK_FILE_INVALID", 422);
        if ((await scanner.ScanAsync(valid.Content, valid.SizeBytes, token)).Result != EvidenceScanResult.Limpio)
            throw new DirectionPlanException("TASK_FILE_SCAN_FAILED", 422);
        valid.Content.Position = 0;
        using var buffer = new MemoryStream(); await valid.Content.CopyToAsync(buffer, token);
        var name = Path.GetFileName(file.FileName);
        if (name.Length is < 1 or > 255 || name.Any(char.IsControl)) throw new DirectionPlanException("TASK_FILE_INVALID", 422);
        return (buffer.ToArray(), valid.MediaType.ToMediaType(), name);
    }

    private async Task<Guid?> AuthorizedOccasionAsync(Guid actor, Guid assignmentId, CancellationToken token)
    {
        var person = await PersonAsync(actor, token);
        if (!await db.OperationalAssignments.AnyAsync(a => a.Id == assignmentId && a.PersonId == person, token))
            await new OperationalSupervisionService(db, clock, direction).AuthorizeAssignmentAsync(actor, assignmentId, token);
        return await db.OperationalAssignments.Where(a => a.Id == assignmentId).Select(a => a.OccasionId).SingleAsync(token);
    }

    public async Task<OccasionDetails?> OccasionAsync(Guid actor, Guid assignmentId, CancellationToken token)
    {
        var id = await AuthorizedOccasionAsync(actor, assignmentId, token);
        if (id == null) return null;
        var occasion = await db.Set<OperationalOccasion>().AsNoTracking().SingleAsync(x => x.Id == id, token);
        var files = await db.Set<OperationalReferenceFile>().AsNoTracking().Where(x => x.OccasionId == id).OrderBy(x => x.Position)
            .Select(x => new ReferenceFileDetails(x.Id, x.FileName, x.ContentType)).ToArrayAsync(token);
        return new(occasion.Name, occasion.Instructions, occasion.PublicationText, occasion.Channels, files);
    }

    public async Task<OperationalReferenceFile> ReferenceFileAsync(Guid actor, Guid assignmentId, Guid fileId, CancellationToken token)
    {
        var id = await AuthorizedOccasionAsync(actor, assignmentId, token);
        return await db.Set<OperationalReferenceFile>().AsNoTracking().SingleOrDefaultAsync(f => f.Id == fileId && f.OccasionId == id, token)
            ?? throw new DirectionPlanException("TASK_NOT_FOUND", 404);
    }
}
