using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgol.Assignment.Contracts;
using Sgol.Configuration.Contracts;
using Sgol.Evidence.Contracts;
using Sgol.Generation.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Validation.Contracts;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;

namespace Sgol.Web.Infrastructure.Persistence.Validation;

public sealed class ValidationRequirementConfiguration : IEntityTypeConfiguration<ValidationRequirement>
{
    public const string ObligationIndex = "UX_validation_requirement_obligation";
    public void Configure(EntityTypeBuilder<ValidationRequirement> builder)
    {
        builder.ToTable("validation_requirement", table =>
        {
            table.HasCheckConstraint("CK_validation_requirement_status", "status IN ('PENDIENTE','RESUELTA')");
            table.HasCheckConstraint("CK_validation_requirement_state", "(status = 'PENDIENTE' AND resolved_at IS NULL) OR (status = 'RESUELTA' AND resolved_at IS NOT NULL)");
            table.HasCheckConstraint("CK_validation_requirement_row_version", "row_version > 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.ObligationId).HasColumnName("obligation_id");
        builder.Property(x => x.PolicyVersionId).HasColumnName("policy_version_id");
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(16);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        builder.Property(x => x.ResolvedAt).HasColumnName("resolved_at").HasColumnType("timestamp with time zone");
        builder.Property(x => x.RowVersion).HasColumnName("row_version").IsConcurrencyToken();
        builder.HasIndex(x => x.ObligationId).IsUnique().HasDatabaseName(ObligationIndex);
        builder.HasIndex(x => new { x.Status, x.CreatedAt, x.Id });
        builder.HasIndex(x => new { x.PolicyVersionId, x.CreatedAt, x.Id });
        builder.HasOne<WorkObligation>().WithMany().HasForeignKey(x => x.ObligationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ValidationPolicyVersion>().WithMany().HasForeignKey(x => x.PolicyVersionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ValidationDecisionVersionConfiguration : IEntityTypeConfiguration<ValidationDecisionVersion>
{
    public const string CurrentIndex = "UX_validation_decision_version_current_requirement";
    public void Configure(EntityTypeBuilder<ValidationDecisionVersion> builder)
    {
        builder.ToTable("validation_decision_version", table =>
        {
            table.HasCheckConstraint("CK_validation_decision_result", "result IN ('CUMPLIDA','INCOMPLETA','NO_CUMPLIDA')");
            table.HasCheckConstraint("CK_validation_decision_status", "status IN ('VIGENTE','SUSTITUIDA')");
            table.HasCheckConstraint("CK_validation_decision_authority", "authority_type IN ('ORDINARIA','ESCALAMIENTO','AUTOVALIDACION_DIRECCION','SUSTITUCION_ORIGINAL','SUSTITUCION_SUPERIOR')");
            table.HasCheckConstraint("CK_validation_decision_version", "version_no > 0 AND ((version_no = 1 AND supersedes_id IS NULL) OR (version_no > 1 AND supersedes_id IS NOT NULL))");
            table.HasCheckConstraint("CK_validation_decision_foundation", "char_length(foundation) BETWEEN 1 AND 1000 AND foundation = btrim(foundation) AND replace(foundation, chr(9), '') !~ '[<>[:cntrl:]]'");
            table.HasCheckConstraint("CK_validation_decision_reason", "((authority_type IN ('ORDINARIA','AUTOVALIDACION_DIRECCION') AND reason IS NULL) OR (authority_type NOT IN ('ORDINARIA','AUTOVALIDACION_DIRECCION') AND char_length(reason) BETWEEN 1 AND 500 AND reason = btrim(reason) AND replace(reason, chr(9), '') !~ '[<>[:cntrl:]]'))");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.RequirementId).HasColumnName("requirement_id");
        builder.Property(x => x.VersionNo).HasColumnName("version_no");
        builder.Property(x => x.Result).HasColumnName("result").HasMaxLength(16);
        builder.Property(x => x.Foundation).HasColumnName("foundation").HasMaxLength(1000);
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(16);
        builder.Property(x => x.AuthorityType).HasColumnName("authority_type").HasMaxLength(32);
        builder.Property(x => x.ValidatorUserId).HasColumnName("validator_user_id");
        builder.Property(x => x.ValidatorPersonId).HasColumnName("validator_person_id");
        builder.Property(x => x.ValidatorRole).HasColumnName("validator_role").HasMaxLength(32);
        builder.Property(x => x.AssignmentVersionId).HasColumnName("assignment_version_id");
        builder.Property(x => x.ResponsiblePersonId).HasColumnName("responsible_person_id");
        builder.Property(x => x.DecidedAt).HasColumnName("decided_at").HasColumnType("timestamp with time zone");
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(500);
        builder.Property(x => x.SupersedesId).HasColumnName("supersedes_id");
        builder.Property(x => x.EvidenceReviewSnapshotId).HasColumnName("evidence_review_snapshot_id");
        builder.HasIndex(x => new { x.RequirementId, x.VersionNo }).IsUnique();
        builder.HasIndex(x => x.RequirementId).IsUnique().HasFilter("status = 'VIGENTE'").HasDatabaseName(CurrentIndex);
        builder.HasIndex(x => x.SupersedesId).IsUnique().HasFilter("supersedes_id IS NOT NULL");
        builder.HasIndex(x => new { x.ValidatorUserId, x.DecidedAt, x.Id });
        builder.HasOne<ValidationRequirement>().WithMany().HasForeignKey(x => x.RequirementId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(x => x.ValidatorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(x => x.ValidatorPersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AssignmentVersion>().WithMany().HasForeignKey(x => x.AssignmentVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>().WithMany().HasForeignKey(x => x.ResponsiblePersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ValidationDecisionVersion>().WithMany().HasForeignKey(x => x.SupersedesId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<EvidenceReviewSnapshot>().WithMany().HasForeignKey(x => x.EvidenceReviewSnapshotId).OnDelete(DeleteBehavior.Restrict);
    }
}
