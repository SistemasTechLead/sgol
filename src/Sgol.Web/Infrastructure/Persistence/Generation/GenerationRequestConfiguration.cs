using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgol.Configuration.Contracts;
using Sgol.Generation.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;

namespace Sgol.Web.Infrastructure.Persistence.Generation;

public sealed class GenerationRequestConfiguration : IEntityTypeConfiguration<GenerationRequest>
{
    public const string IdempotencyIndex = "UX_generation_request_idempotency_key";
    public const string FunctionalKeyIndex = "UX_generation_request_functional_key";

    public void Configure(EntityTypeBuilder<GenerationRequest> builder)
    {
        builder.ToTable(
            "generation_request",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_generation_request_result",
                    "result IN ('ACEPTADA','RECUPERADA','RECHAZADA')");
                table.HasCheckConstraint(
                    "CK_generation_request_origin",
                    "btrim(origin_type) <> '' AND btrim(origin_reference) <> ''");
            });

        builder.HasKey(request => request.Id);
        builder.Property(request => request.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(request => request.IdempotencyKey).HasColumnName("idempotency_key").ValueGeneratedNever();
        builder.Property(request => request.RequestHash).HasColumnName("request_hash").HasColumnType("character(64)");
        builder.Property(request => request.RuleVersionId).HasColumnName("rule_version_id");
        builder.Property(request => request.BranchId).HasColumnName("branch_id");
        builder.Property(request => request.PeriodId).HasColumnName("period_id");
        builder.Property(request => request.OriginType).HasColumnName("origin_type").HasMaxLength(64);
        builder.Property(request => request.OriginReference).HasColumnName("origin_reference");
        builder.Property(request => request.Result).HasColumnName("result").HasMaxLength(16);
        builder.Property(request => request.RequestedBy).HasColumnName("requested_by");
        builder.Property(request => request.RequestedAt).HasColumnName("requested_at").HasColumnType("timestamp with time zone");
        builder.Property(request => request.ObligationId).HasColumnName("obligation_id");
        builder.Property(request => request.ErrorCode).HasColumnName("error_code").HasMaxLength(64);

        builder.HasIndex(request => request.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName(IdempotencyIndex);
        builder.HasIndex(request => new
        {
            request.RuleVersionId,
            request.BranchId,
            request.PeriodId,
            request.OriginType,
            request.OriginReference,
        })
            .IsUnique()
            .HasDatabaseName(FunctionalKeyIndex);
        builder.HasIndex(request => request.ObligationId)
            .IsUnique()
            .HasDatabaseName("UX_generation_request_obligation_id")
            .HasFilter("obligation_id IS NOT NULL");

        builder.HasOne<ActivationRuleVersion>()
            .WithMany()
            .HasForeignKey(request => request.RuleVersionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(request => request.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WeekPeriod>()
            .WithMany()
            .HasForeignKey(request => request.PeriodId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(request => request.RequestedBy)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkObligation>()
            .WithOne()
            .HasForeignKey<GenerationRequest>(request => new { request.ObligationId, request.Id })
            .HasPrincipalKey<WorkObligation>(obligation => new { obligation.Id, obligation.GenerationRequestId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
