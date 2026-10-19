using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed class OperationalSequence
{
    public static readonly Guid GeneralId = Guid.Parse("00000000-0000-0000-0000-000000000010");
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsGeneral { get; set; }
    public Guid? PublishedVersionId { get; set; }
    public bool Enabled { get; set; }
    public long Revision { get; set; } = 1;
}

// A published version is immutable. Editing creates another version.
public sealed class OperationalSequenceVersion
{
    public Guid Id { get; set; }
    public Guid SequenceId { get; set; }
    public int Number { get; set; }
    public string StartKind { get; set; } = "SEMESTRAL";
    public int FirstMonth { get; set; } = 2;
    public int WindowFromDay { get; set; } = 20;
    public int WindowThroughDay { get; set; } = 25;
    public int PreferredWeekDay { get; set; } = 1;
    public int StartHour { get; set; }
    public bool StartHourConfirmed { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTimeOffset? PublishedAt { get; set; }
    public Guid? PublishedBy { get; set; }
    public long Revision { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
}

public sealed class OperationalSequenceStep
{
    public Guid VersionId { get; set; }
    public int Position { get; set; }
    public Guid TaskId { get; set; }
    public string Action { get; set; } = "ENTREGAR";
    public string ReleaseCondition { get; set; } = "ENTREGA";
    public string DeadlineKind { get; set; } = "RELATIVO";
    public int DeadlineHours { get; set; }
    public int? MinimumStartOffsetHours { get; set; }
    public int? ReturnToPosition { get; set; }
    public string Instructions { get; set; } = "";
    public string? TaskSnapshot { get; set; }
}

public sealed class OperationalSequenceRun
{
    public Guid Id { get; set; }
    public Guid VersionId { get; set; }
    public Guid PlanId { get; set; }
    public string StartKey { get; set; } = "";
    public string Name { get; set; } = "";
    public string Status { get; set; } = "EN_CURSO";
    public int ActivePosition { get; set; } = 1;
    public int Round { get; set; } = 1;
    public DateTimeOffset PlannedStartsAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public Guid? ClosedBy { get; set; }
    public string? ClosureOutcome { get; set; }
    public string? ClosureComment { get; set; }
    public Guid? AuthorizedDocumentId { get; set; }
    public long Revision { get; set; } = 1;
}

// Estimates and effective dates deliberately have distinct fields. No fake assignment
// is inserted while a stage is waiting or has an expired fixed deadline.
public sealed class OperationalSequenceStage
{
    public string? Description { get; set; }
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public Guid VersionId { get; set; }
    public int Position { get; set; }
    public Guid TaskId { get; set; }
    public long TaskRevision { get; set; }
    public string TaskCode { get; set; } = "";
    public string Name { get; set; } = "";
    public string Role { get; set; } = "";
    public string Requirement { get; set; } = "";
    public Guid? SpecializationId { get; set; }
    public string? SpecializationName { get; set; }
    public int LoadMinutes { get; set; }
    public int MinimumPhotos { get; set; } = 1;
    public int MaximumPhotos { get; set; } = 20;
    public long MaximumPhotoBytes { get; set; } = 31457280;
    public string Status { get; set; } = "EN_ESPERA";
    public Guid? CurrentAttemptId { get; set; }
    public Guid? ReservedPersonId { get; set; }
    public string? ReservedPersonName { get; set; }
    public DateTimeOffset PlannedStartsAt { get; set; }
    public DateTimeOffset PlannedDueAt { get; set; }
    public DateTimeOffset? MinimumStartsAt { get; set; }
    public DateTimeOffset? FixedDueAt { get; set; }
    public DateTimeOffset? DependencySatisfiedAt { get; set; }
    public Guid? DependencyEventId { get; set; }
    public DateTimeOffset? EligibleAt { get; set; }
    public DateTimeOffset? EnabledAt { get; set; }
    public DateTimeOffset? EffectiveDueAt { get; set; }
    public string? BlockReason { get; set; }
    public long Revision { get; set; } = 1;
}

public sealed class OperationalSequenceAttempt
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public Guid StageId { get; set; }
    public Guid PlanId { get; set; }
    public int Number { get; set; }
    public int Round { get; set; }
    public Guid? AssignmentId { get; set; }
    public Guid? InputDocumentId { get; set; }
    public Guid? OutputDocumentId { get; set; }
    public string Status { get; set; } = "PREPARADO";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string? Comment { get; set; }
    public string? UnresolvedDifferences { get; set; }
    public long Revision { get; set; } = 1;
}

// Bytes remain in the existing verified attachment. A version points to its producer.
public sealed class OperationalSequenceDocument
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public Guid ProducerAttemptId { get; set; }
    public Guid AssignmentId { get; set; }
    public int Number { get; set; }
    public Guid? PreviousDocumentId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public string Sha256 { get; set; } = "";
}

public sealed class OperationalSequenceEvent
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public Guid StageId { get; set; }
    public Guid AttemptId { get; set; }
    public Guid? DocumentId { get; set; }
    public string Kind { get; set; } = "";
    public string EventKey { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
    public Guid? ActorId { get; set; }
    public string ActorKind { get; set; } = "USER";
    public string? Comment { get; set; }
}

internal static class OperationalSequenceSchema
{
    public static void Configure(ModelBuilder model)
    {
        var root = model.Entity<OperationalSequence>();
        root.ToTable("operational_sequence", t => t.HasCheckConstraint("CK_sequence_revision", "\"Revision\" > 0"));
        root.HasKey(x => x.Id); root.Property(x => x.Name).HasMaxLength(120);
        root.Property(x => x.Revision).IsConcurrencyToken();
        root.Property(x => x.Description).HasMaxLength(4000);
        root.HasIndex(x => x.IsGeneral).IsUnique().HasFilter("\"IsGeneral\"");
        root.HasData(new OperationalSequence { Id = OperationalSequence.GeneralId, Name = "General", IsGeneral = true });

        var version = model.Entity<OperationalSequenceVersion>();
        version.ToTable("operational_sequence_version", t => t.HasCheckConstraint("CK_sequence_calendar",
            "\"Number\" > 0 AND \"StartKind\" IN ('SEMESTRAL','OCASIONAL') AND \"FirstMonth\" BETWEEN 1 AND 6 AND \"WindowFromDay\" BETWEEN 1 AND 28 AND \"WindowThroughDay\" BETWEEN \"WindowFromDay\" AND 28 AND \"PreferredWeekDay\" BETWEEN 0 AND 6 AND \"StartHour\" BETWEEN 0 AND 23"));
        version.HasKey(x => x.Id); version.HasIndex(x => new { x.SequenceId, x.Number }).IsUnique();
        version.Property(x => x.StartKind).HasMaxLength(16);
        version.Property(x => x.Name).HasMaxLength(120);
        version.Property(x => x.Description).HasMaxLength(4000);
        version.Property(x => x.Revision).IsConcurrencyToken();
        version.HasAlternateKey(x => new { x.SequenceId, x.Id });
        version.HasIndex(x => x.SequenceId).IsUnique().HasFilter("\"PublishedAt\" IS NULL");
        version.HasOne<AppUser>().WithMany().HasForeignKey(x => x.PublishedBy).OnDelete(DeleteBehavior.Restrict);
        root.HasOne<OperationalSequenceVersion>().WithMany().HasForeignKey(x => new { x.Id, x.PublishedVersionId }).HasPrincipalKey(x => new { x.SequenceId, x.Id }).OnDelete(DeleteBehavior.NoAction);
        version.HasOne<OperationalSequence>().WithMany().HasForeignKey(x => x.SequenceId).OnDelete(DeleteBehavior.Restrict);
        version.HasOne<AppUser>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);

        var step = model.Entity<OperationalSequenceStep>();
        step.ToTable("operational_sequence_step", t => {
            t.HasCheckConstraint("CK_sequence_step_position", "\"Position\" BETWEEN 1 AND 32 AND (\"ReturnToPosition\" IS NULL OR \"ReturnToPosition\" BETWEEN 1 AND \"Position\" - 1)");
            t.HasCheckConstraint("CK_sequence_step_rule", "\"Action\" IN ('ENTREGAR','REVISAR','SELLAR','AUTORIZAR') AND ((\"Position\" = 1 AND \"ReleaseCondition\" = 'INICIO') OR (\"Position\" > 1 AND \"ReleaseCondition\" IN ('ENTREGA','APROBACION'))) AND \"DeadlineKind\" IN ('RELATIVO','FIJO') AND \"DeadlineHours\" BETWEEN 1 AND 8760 AND (\"MinimumStartOffsetHours\" IS NULL OR \"MinimumStartOffsetHours\" BETWEEN 0 AND 8760)");
        });
        step.HasKey(x => new { x.VersionId, x.Position });
        step.HasOne<OperationalSequenceVersion>().WithMany().HasForeignKey(x => x.VersionId).OnDelete(DeleteBehavior.Restrict);
        step.HasOne<OperationalTask>().WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Restrict);
        step.Property(x => x.Action).HasMaxLength(16); step.Property(x => x.ReleaseCondition).HasMaxLength(16);
        step.Property(x => x.DeadlineKind).HasMaxLength(16); step.Property(x => x.Instructions).HasMaxLength(4000);
        step.Property(x => x.TaskSnapshot).HasColumnType("jsonb");

        var run = model.Entity<OperationalSequenceRun>();
        run.ToTable("operational_sequence_run", t => {
            t.HasCheckConstraint("CK_sequence_run_state", "\"Status\" IN ('EN_CURSO','CONCLUIDA','CANCELADA') AND \"Round\" > 0 AND \"ActivePosition\" BETWEEN 1 AND 32 AND \"Revision\" > 0");
            t.HasCheckConstraint("CK_sequence_run_closure", "(\"Status\" = 'EN_CURSO' AND \"ClosedAt\" IS NULL AND \"ClosedBy\" IS NULL AND \"ClosureOutcome\" IS NULL AND \"AuthorizedDocumentId\" IS NULL) OR (\"Status\" = 'CONCLUIDA' AND \"ClosedAt\" IS NOT NULL AND \"ClosedBy\" IS NOT NULL AND \"AuthorizedDocumentId\" IS NOT NULL AND \"ClosureOutcome\" IS NOT NULL AND \"ClosureComment\" IS NOT NULL AND \"ClosureOutcome\" IN ('SIN_DIFERENCIAS','ACEPTADA_CON_DIFERENCIAS') AND length(trim(\"ClosureComment\")) > 0) OR (\"Status\" = 'CANCELADA' AND \"ClosedAt\" IS NOT NULL AND \"ClosedBy\" IS NOT NULL AND \"ClosureOutcome\" = 'CANCELACION' AND \"ClosureOutcome\" IS NOT NULL AND \"ClosureComment\" IS NOT NULL AND length(trim(\"ClosureComment\")) > 0 AND \"AuthorizedDocumentId\" IS NULL)");
        });
        run.HasKey(x => x.Id); run.HasAlternateKey(x => new { x.Id, x.VersionId }); run.HasAlternateKey(x => new { x.Id, x.PlanId });
        run.HasIndex(x => x.StartKey).IsUnique(); run.Property(x => x.StartKey).HasMaxLength(160);
        run.Property(x => x.Name).HasMaxLength(120); run.Property(x => x.Status).HasMaxLength(24);
        run.Property(x => x.ClosureOutcome).HasMaxLength(32); run.Property(x => x.ClosureComment).HasMaxLength(4000);
        run.Property(x => x.Revision).IsConcurrencyToken();
        run.HasOne<OperationalSequenceVersion>().WithMany().HasForeignKey(x => x.VersionId).OnDelete(DeleteBehavior.Restrict);
        run.HasOne<DirectionPreparedPlan>().WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
        run.HasOne<AppUser>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        run.HasOne<AppUser>().WithMany().HasForeignKey(x => x.ClosedBy).OnDelete(DeleteBehavior.Restrict);

        var stage = model.Entity<OperationalSequenceStage>();
        stage.ToTable("operational_sequence_stage", t => {
            t.HasCheckConstraint("CK_sequence_stage_snapshot", "\"TaskRevision\" > 0 AND \"LoadMinutes\" BETWEEN 1 AND 4800 AND \"Role\" IN ('PISO_VENTAS','SUBCOORDINACION','ADMINISTRACION','DIRECCION') AND \"Requirement\" IN ('FOTOGRAFIA','CAPTURA_PANTALLA','COMENTARIO','DOCUMENTO','EXCEL','NINGUNO') AND \"MinimumPhotos\" BETWEEN 1 AND 20 AND \"MaximumPhotos\" BETWEEN \"MinimumPhotos\" AND 20 AND \"MaximumPhotoBytes\" BETWEEN 1048576 AND 31457280 AND \"Revision\" > 0");
            t.HasCheckConstraint("CK_sequence_stage_dates", "\"PlannedDueAt\" > \"PlannedStartsAt\" AND ((\"EnabledAt\" IS NULL AND \"EffectiveDueAt\" IS NULL) OR (\"EnabledAt\" IS NOT NULL AND \"EligibleAt\" IS NOT NULL AND \"EffectiveDueAt\" IS NOT NULL AND \"EffectiveDueAt\" > \"EnabledAt\" AND \"EnabledAt\" >= \"EligibleAt\"))");
            t.HasCheckConstraint("CK_sequence_stage_state", "\"Status\" IN ('EN_ESPERA','PROGRAMADA','DISPONIBLE','PENDIENTE_ASIGNACION','FUERA_DE_PLAZO','CONCLUIDA','CANCELADA') AND (\"Status\" NOT IN ('DISPONIBLE','CONCLUIDA') OR (\"CurrentAttemptId\" IS NOT NULL AND \"EnabledAt\" IS NOT NULL))");
            t.HasCheckConstraint("CK_sequence_stage_dependency", "(\"DependencySatisfiedAt\" IS NULL AND \"DependencyEventId\" IS NULL) OR (\"DependencySatisfiedAt\" IS NOT NULL AND \"DependencyEventId\" IS NOT NULL)");
        });
        stage.HasKey(x => x.Id); stage.HasAlternateKey(x => new { x.RunId, x.Id });
        stage.HasIndex(x => new { x.RunId, x.Position }).IsUnique(); stage.HasIndex(x => x.CurrentAttemptId).IsUnique();
        stage.HasOne<OperationalSequenceRun>().WithMany().HasForeignKey(x => new { x.RunId, x.VersionId }).HasPrincipalKey(x => new { x.Id, x.VersionId }).OnDelete(DeleteBehavior.Restrict);
        stage.HasOne<OperationalSequenceStep>().WithMany().HasForeignKey(x => new { x.VersionId, x.Position }).OnDelete(DeleteBehavior.Restrict);
        stage.HasOne<OperationalTask>().WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Restrict);
        stage.HasOne<Person>().WithMany().HasForeignKey(x => x.ReservedPersonId).OnDelete(DeleteBehavior.Restrict);
        stage.HasOne<Sgol.Web.Infrastructure.Persistence.Organization.Specialization>().WithMany().HasForeignKey(x => x.SpecializationId).OnDelete(DeleteBehavior.Restrict);
        stage.Property(x => x.TaskCode).HasMaxLength(100); stage.Property(x => x.Name).HasMaxLength(500);
        stage.Property(x => x.Description).HasMaxLength(4000);
        stage.Property(x => x.Role).HasMaxLength(20); stage.Property(x => x.Requirement).HasMaxLength(24);
        stage.Property(x => x.Status).HasMaxLength(32); stage.Property(x => x.BlockReason).HasMaxLength(4000);
        stage.Property(x => x.ReservedPersonName).HasMaxLength(300); stage.Property(x => x.SpecializationName).HasMaxLength(120);
        stage.Property(x => x.Revision).IsConcurrencyToken();

        var attempt = model.Entity<OperationalSequenceAttempt>();
        attempt.ToTable("operational_sequence_attempt", t => {
            t.HasCheckConstraint("CK_sequence_attempt_state", "\"Number\" > 0 AND \"Round\" > 0 AND \"Revision\" > 0 AND \"Status\" IN ('PREPARADO','ACTIVO','ENTREGADO','APROBADO','SUSTITUIDO','CANCELADO') AND (\"Status\" NOT IN ('ACTIVO','ENTREGADO','APROBADO') OR \"AssignmentId\" IS NOT NULL)");
        });
        attempt.HasKey(x => x.Id); attempt.HasAlternateKey(x => new { x.RunId, x.StageId, x.Id });
        attempt.HasAlternateKey(x => new { x.RunId, x.Id });
        attempt.HasIndex(x => new { x.StageId, x.Number }).IsUnique(); attempt.HasIndex(x => x.AssignmentId).IsUnique();
        attempt.HasOne<OperationalSequenceStage>().WithMany().HasForeignKey(x => new { x.RunId, x.StageId }).HasPrincipalKey(x => new { x.RunId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        attempt.HasOne<OperationalSequenceRun>().WithMany().HasForeignKey(x => new { x.RunId, x.PlanId }).HasPrincipalKey(x => new { x.Id, x.PlanId }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<OperationalAssignment>().HasAlternateKey(x => new { x.Id, x.PlanId });
        attempt.HasOne<OperationalAssignment>().WithMany().HasForeignKey(x => new { x.AssignmentId, x.PlanId }).HasPrincipalKey(x => new { x.Id, x.PlanId }).OnDelete(DeleteBehavior.Restrict);
        attempt.Property(x => x.Status).HasMaxLength(24); attempt.Property(x => x.Comment).HasMaxLength(4000);
        attempt.Property(x => x.UnresolvedDifferences).HasMaxLength(4000); attempt.Property(x => x.Revision).IsConcurrencyToken();
        stage.HasOne<OperationalSequenceAttempt>().WithMany().HasForeignKey(x => new { x.RunId, x.Id, x.CurrentAttemptId }).HasPrincipalKey(x => new { x.RunId, x.StageId, x.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_sequence_stage_current_attempt");

        var doc = model.Entity<OperationalSequenceDocument>();
        doc.ToTable("operational_sequence_document", t => t.HasCheckConstraint("CK_sequence_document_version", "\"Number\" > 0 AND \"Sha256\" ~ '^[0-9a-f]{64}$' AND (\"PreviousDocumentId\" IS NULL OR \"PreviousDocumentId\" <> \"Id\")"));
        doc.HasKey(x => x.Id); doc.HasAlternateKey(x => new { x.RunId, x.Id });
        doc.HasIndex(x => new { x.RunId, x.Number }).IsUnique(); doc.HasIndex(x => x.AssignmentId).IsUnique();
        doc.Property(x => x.Sha256).HasMaxLength(64);
        doc.HasOne<OperationalSequenceAttempt>().WithMany().HasForeignKey(x => new { x.RunId, x.ProducerAttemptId }).HasPrincipalKey(x => new { x.RunId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        doc.HasOne<OperationalAttachment>().WithMany().HasForeignKey(x => x.AssignmentId).OnDelete(DeleteBehavior.Restrict);
        doc.HasOne<OperationalSequenceDocument>().WithMany().HasForeignKey(x => new { x.RunId, x.PreviousDocumentId }).HasPrincipalKey(x => new { x.RunId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        doc.HasOne<AppUser>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        attempt.HasOne<OperationalSequenceDocument>().WithMany().HasForeignKey(x => new { x.RunId, x.InputDocumentId }).HasPrincipalKey(x => new { x.RunId, x.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_sequence_attempt_input_document");
        attempt.HasOne<OperationalSequenceDocument>().WithMany().HasForeignKey(x => new { x.RunId, x.OutputDocumentId }).HasPrincipalKey(x => new { x.RunId, x.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_sequence_attempt_output_document");
        run.HasOne<OperationalSequenceDocument>().WithMany().HasForeignKey(x => new { x.Id, x.AuthorizedDocumentId }).HasPrincipalKey(x => new { x.RunId, x.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_sequence_run_authorized_document");

        var evt = model.Entity<OperationalSequenceEvent>();
        evt.ToTable("operational_sequence_event", t => {
            t.HasCheckConstraint("CK_sequence_event_actor", "(\"ActorKind\" = 'USER' AND \"ActorId\" IS NOT NULL) OR (\"ActorKind\" = 'SYSTEM' AND \"ActorId\" IS NULL)");
            t.HasCheckConstraint("CK_sequence_event_kind", "\"Kind\" IN ('INICIO','HABILITACION','ASIGNACION','ENTREGA','APROBACION','DEVOLUCION','REASIGNACION','CIERRE','CANCELACION','BLOQUEO')");
        });
        evt.HasKey(x => x.Id); evt.HasAlternateKey(x => new { x.RunId, x.Id });
        evt.HasIndex(x => new { x.RunId, x.EventKey }).IsUnique(); evt.HasIndex(x => new { x.RunId, x.OccurredAt });
        evt.HasOne<OperationalSequenceAttempt>().WithMany().HasForeignKey(x => new { x.RunId, x.StageId, x.AttemptId }).HasPrincipalKey(x => new { x.RunId, x.StageId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        evt.HasOne<OperationalSequenceDocument>().WithMany().HasForeignKey(x => new { x.RunId, x.DocumentId }).HasPrincipalKey(x => new { x.RunId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        evt.HasOne<AppUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
        evt.Property(x => x.Kind).HasMaxLength(24); evt.Property(x => x.ActorKind).HasMaxLength(16);
        evt.Property(x => x.EventKey).HasMaxLength(160); evt.Property(x => x.Comment).HasMaxLength(4000);
        stage.HasOne<OperationalSequenceEvent>().WithMany().HasForeignKey(x => new { x.RunId, x.DependencyEventId }).HasPrincipalKey(x => new { x.RunId, x.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_sequence_stage_dependency_event");
    }
}
