using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgol.Configuration.Contracts;
using Sgol.Evidence.Contracts;
using Sgol.Generation.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;

namespace Sgol.Web.Infrastructure.Persistence.Evidence;

public sealed class FileObjectConfiguration : IEntityTypeConfiguration<FileObject>
{
    public void Configure(EntityTypeBuilder<FileObject> builder)
    {
        builder.ToTable("file_object", table =>
        {
            table.HasCheckConstraint("CK_file_object_branch", $"branch_id = '{BranchScope.LorettaId:D}'::uuid");
            table.HasCheckConstraint("CK_file_object_key", "object_key ~ '^v1/[0-9a-f]{2}/[0-9a-f]{2}/[0-9a-f]{64}$'");
            table.HasCheckConstraint("CK_file_object_size", "size_bytes BETWEEN 1 AND 15728640");
            table.HasCheckConstraint("CK_file_object_sha256", "sha256 ~ '^[0-9a-f]{64}$'");
            table.HasCheckConstraint("CK_file_object_kind", "requirement_kind IN ('FOTOGRAFIA','DOCUMENTO_REFERENCIADO')");
            table.HasCheckConstraint("CK_file_object_media", "declared_media_type IN ('image/jpeg','image/png','application/pdf')");
            table.HasCheckConstraint("CK_file_object_kind_media", "(requirement_kind = 'FOTOGRAFIA' AND declared_media_type IN ('image/jpeg','image/png')) OR (requirement_kind = 'DOCUMENTO_REFERENCIADO' AND declared_media_type = 'application/pdf')");
            table.HasCheckConstraint("CK_file_object_subtype", "(requirement_code = 'DOCUMENTO_RECEPCION' AND document_subtype IN ('NOTA','REMISION','FACTURA')) OR (requirement_code <> 'DOCUMENTO_RECEPCION' AND document_subtype IS NULL)");
            table.HasCheckConstraint("CK_file_object_original_name", "char_length(original_name) BETWEEN 1 AND 255 AND original_name = btrim(original_name) AND original_name !~ '[\\\\/]' AND original_name !~ '[[:cntrl:]]'");
            table.HasCheckConstraint("CK_file_object_error_code", "scan_error_code IS NULL OR scan_error_code ~ '^[A-Z][A-Z0-9_]{0,63}$'");
            table.HasCheckConstraint("CK_file_object_status", "scan_status IN ('PENDIENTE','LIMPIO','INFECTADO','INVALIDO','ERROR_ESCANEO')");
            table.HasCheckConstraint("CK_file_object_bucket", "bucket_class IN ('QUARANTINE','CLEAN')");
            table.HasCheckConstraint("CK_file_object_timeline", "upload_expires_at = created_at + interval '10 minutes' AND (upload_completed_at IS NULL OR upload_completed_at BETWEEN created_at AND upload_expires_at) AND (scanned_at IS NULL OR scanned_at >= created_at)");
            table.HasCheckConstraint("CK_file_object_state", "(scan_status = 'PENDIENTE' AND bucket_class = 'QUARANTINE' AND detected_media_type IS NULL AND scanned_at IS NULL AND link_expires_at IS NULL) OR (scan_status = 'LIMPIO' AND bucket_class = 'CLEAN' AND detected_media_type = declared_media_type AND scanned_at IS NOT NULL AND link_expires_at = scanned_at + interval '24 hours') OR (scan_status IN ('INFECTADO','INVALIDO','ERROR_ESCANEO') AND bucket_class = 'QUARANTINE' AND scanned_at IS NOT NULL AND scan_error_code IS NOT NULL AND link_expires_at IS NULL)");
            table.HasCheckConstraint("CK_file_object_link", "linked_evidence_item_id IS NULL OR (scan_status = 'LIMPIO' AND bucket_class = 'CLEAN')");
            table.HasCheckConstraint("CK_file_object_replication_reserved", "replicated_at IS NULL");
            table.HasCheckConstraint("CK_file_object_row_version", "row_version > 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.BranchId).HasColumnName("branch_id");
        builder.Property(x => x.ObligationId).HasColumnName("obligation_id");
        builder.Property(x => x.EvidencePolicyVersionId).HasColumnName("evidence_policy_version_id");
        builder.Property(x => x.RequirementVersionId).HasColumnName("requirement_version_id");
        builder.Property(x => x.RequirementCode).HasColumnName("requirement_code").HasMaxLength(64);
        builder.Property(x => x.RequirementKind).HasColumnName("requirement_kind").HasMaxLength(32);
        builder.Property(x => x.DocumentSubtype).HasColumnName("document_subtype").HasMaxLength(16);
        builder.Property(x => x.BucketClass).HasColumnName("bucket_class").HasMaxLength(16);
        builder.Property(x => x.ObjectKey).HasColumnName("object_key").HasMaxLength(73);
        builder.Property(x => x.OriginalName).HasColumnName("original_name").HasMaxLength(255);
        builder.Property(x => x.DeclaredMediaType).HasColumnName("declared_media_type").HasMaxLength(32);
        builder.Property(x => x.DetectedMediaType).HasColumnName("detected_media_type").HasMaxLength(32);
        builder.Property(x => x.SizeBytes).HasColumnName("size_bytes");
        builder.Property(x => x.Sha256).HasColumnName("sha256").HasColumnType("character(64)");
        builder.Property(x => x.ScanStatus).HasColumnName("scan_status").HasMaxLength(16);
        builder.Property(x => x.ScanEngine).HasColumnName("scan_engine").HasMaxLength(64);
        builder.Property(x => x.ScanErrorCode).HasColumnName("scan_error_code").HasMaxLength(64);
        builder.Property(x => x.ScannedAt).HasColumnName("scanned_at").HasColumnType("timestamp with time zone");
        builder.Property(x => x.UploadedBy).HasColumnName("uploaded_by");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        builder.Property(x => x.UploadExpiresAt).HasColumnName("upload_expires_at").HasColumnType("timestamp with time zone");
        builder.Property(x => x.UploadCompletedAt).HasColumnName("upload_completed_at").HasColumnType("timestamp with time zone");
        builder.Property(x => x.LinkExpiresAt).HasColumnName("link_expires_at").HasColumnType("timestamp with time zone");
        builder.Property(x => x.LinkedEvidenceItemId).HasColumnName("linked_evidence_item_id");
        builder.Property(x => x.ReplicatedAt).HasColumnName("replicated_at").HasColumnType("timestamp with time zone");
        builder.Property(x => x.RowVersion).HasColumnName("row_version").IsConcurrencyToken();
        builder.HasIndex(x => x.ObjectKey).IsUnique();
        builder.HasIndex(x => x.LinkedEvidenceItemId).HasFilter("linked_evidence_item_id IS NOT NULL");
        builder.HasIndex(x => new { x.ScanStatus, x.CreatedAt });
        builder.HasIndex(x => new { x.ObligationId, x.RequirementVersionId, x.ScanStatus });
        builder.HasIndex(x => new { x.UploadedBy, x.CreatedAt });
        builder.HasIndex(x => x.LinkExpiresAt).HasFilter("linked_evidence_item_id IS NULL AND link_expires_at IS NOT NULL");
        builder.HasOne<Branch>().WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkObligation>().WithMany().HasForeignKey(x => x.ObligationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<EvidencePolicyVersion>().WithMany().HasForeignKey(x => x.EvidencePolicyVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<EvidenceRequirementVersion>().WithMany().HasForeignKey(x => x.RequirementVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UploadedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<EvidenceItem>().WithMany().HasForeignKey(x => x.LinkedEvidenceItemId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class EvidenceItemConfiguration : IEntityTypeConfiguration<EvidenceItem>
{
    public const string UniqueRequirementIndex = "UX_evidence_item_obligation_requirement";
    public void Configure(EntityTypeBuilder<EvidenceItem> builder)
    {
        builder.ToTable("evidence_item", table => table.HasCheckConstraint("CK_evidence_item_row_version", "row_version > 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.ObligationId).HasColumnName("obligation_id");
        builder.Property(x => x.EvidencePolicyVersionId).HasColumnName("evidence_policy_version_id");
        builder.Property(x => x.RequirementVersionId).HasColumnName("requirement_version_id");
        builder.Property(x => x.RequirementCode).HasColumnName("requirement_code").HasMaxLength(64);
        builder.Property(x => x.RequirementKind).HasColumnName("requirement_kind").HasMaxLength(32);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        builder.Property(x => x.RowVersion).HasColumnName("row_version").IsConcurrencyToken();
        builder.HasIndex(x => new { x.ObligationId, x.RequirementVersionId }).IsUnique().HasDatabaseName(UniqueRequirementIndex);
        builder.HasIndex(x => x.ObligationId);
        builder.HasIndex(x => x.RequirementVersionId);
        builder.HasOne<WorkObligation>().WithMany().HasForeignKey(x => x.ObligationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<EvidencePolicyVersion>().WithMany().HasForeignKey(x => x.EvidencePolicyVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<EvidenceRequirementVersion>().WithMany().HasForeignKey(x => x.RequirementVersionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class EvidenceVersionConfiguration : IEntityTypeConfiguration<EvidenceVersion>
{
    public const string CurrentVersionIndex = "UX_evidence_version_current_item";
    public void Configure(EntityTypeBuilder<EvidenceVersion> builder)
    {
        builder.ToTable("evidence_version", table =>
        {
            table.HasCheckConstraint("CK_evidence_version_number", "version_no > 0");
            table.HasCheckConstraint("CK_evidence_version_status", "status IN ('VIGENTE','SUSTITUIDA')");
            table.HasCheckConstraint("CK_evidence_version_binary_only", "structured_payload IS NULL");
            table.HasCheckConstraint("CK_evidence_version_chain", "(version_no = 1 AND supersedes_id IS NULL) OR (version_no > 1 AND supersedes_id IS NOT NULL)");
            table.HasCheckConstraint("CK_evidence_version_row_version", "row_version > 0");
            table.HasCheckConstraint("CK_evidence_version_reason", "reason IS NULL OR (char_length(reason) BETWEEN 1 AND 500 AND reason = btrim(reason) AND reason !~ '[<>[:cntrl:]]')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.EvidenceItemId).HasColumnName("evidence_item_id");
        builder.Property(x => x.VersionNo).HasColumnName("version_no");
        builder.Property(x => x.FileObjectId).HasColumnName("file_object_id");
        builder.Property(x => x.StructuredPayload).HasColumnName("structured_payload").HasColumnType("jsonb");
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(16);
        builder.Property(x => x.SubmittedBy).HasColumnName("submitted_by");
        builder.Property(x => x.SubmittedAt).HasColumnName("submitted_at").HasColumnType("timestamp with time zone");
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(500);
        builder.Property(x => x.SupersedesId).HasColumnName("supersedes_id");
        builder.Property(x => x.RowVersion).HasColumnName("row_version").IsConcurrencyToken();
        builder.HasIndex(x => new { x.EvidenceItemId, x.VersionNo }).IsUnique();
        builder.HasIndex(x => x.EvidenceItemId).IsUnique().HasFilter("status = 'VIGENTE'").HasDatabaseName(CurrentVersionIndex);
        builder.HasIndex(x => x.FileObjectId).IsUnique();
        builder.HasIndex(x => x.SupersedesId).IsUnique().HasFilter("supersedes_id IS NOT NULL");
        builder.HasOne<EvidenceItem>().WithMany().HasForeignKey(x => x.EvidenceItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FileObject>().WithMany().HasForeignKey(x => x.FileObjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(x => x.SubmittedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<EvidenceVersion>().WithMany().HasForeignKey(x => x.SupersedesId).OnDelete(DeleteBehavior.Restrict);
    }
}
