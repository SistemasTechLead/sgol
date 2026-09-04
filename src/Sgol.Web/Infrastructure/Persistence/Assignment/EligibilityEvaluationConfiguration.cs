using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgol.Assignment.Contracts;
using Sgol.Configuration.Contracts;
using Sgol.Generation.Contracts;
using Sgol.Organization.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Assignment;

public sealed class EligibilityEvaluationConfiguration : IEntityTypeConfiguration<EligibilityEvaluation>
{
    public const string RequestIndex = "UX_eligibility_evaluation_request";

    public void Configure(EntityTypeBuilder<EligibilityEvaluation> builder)
    {
        builder.ToTable(
            "eligibility_evaluation",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_eligibility_evaluation_date_source",
                    "eligibility_date_source IN ('MANUAL_REQUEST','SCHEDULED_OCCURRENCE')");
                table.HasCheckConstraint(
                    "CK_eligibility_evaluation_result",
                    "result IN ('CANDIDATOS_ELEGIBLES','SIN_CANDIDATO_ELEGIBLE')");
                table.HasCheckConstraint(
                    "CK_eligibility_evaluation_no_winner",
                    "winner_person_id IS NULL");
                table.HasCheckConstraint(
                    "CK_eligibility_evaluation_snapshot_object",
                    "jsonb_typeof(input_snapshot) = 'object'");
            });

        builder.HasKey(evaluation => evaluation.Id);
        builder.Property(evaluation => evaluation.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(evaluation => evaluation.EvaluationRequestId).HasColumnName("evaluation_request_id");
        builder.Property(evaluation => evaluation.ObligationId).HasColumnName("obligation_id");
        builder.Property(evaluation => evaluation.EvaluatedAt).HasColumnName("evaluated_at").HasColumnType("timestamp with time zone");
        builder.Property(evaluation => evaluation.EligibilityDate).HasColumnName("eligibility_date").HasColumnType("date");
        builder.Property(evaluation => evaluation.EligibilityDateSource).HasColumnName("eligibility_date_source").HasMaxLength(32);
        builder.Property(evaluation => evaluation.PolicyVersionId).HasColumnName("policy_version_id");
        builder.Property(evaluation => evaluation.InputSnapshot).HasColumnName("input_snapshot").HasColumnType("jsonb");
        builder.Property(evaluation => evaluation.Result).HasColumnName("result").HasMaxLength(32);
        builder.Property(evaluation => evaluation.WinnerPersonId).HasColumnName("winner_person_id");

        builder.HasIndex(evaluation => evaluation.EvaluationRequestId)
            .IsUnique()
            .HasDatabaseName(RequestIndex);
        builder.HasIndex(evaluation => new { evaluation.ObligationId, evaluation.EvaluatedAt, evaluation.Id });
        builder.HasOne<WorkObligation>()
            .WithMany()
            .HasForeignKey(evaluation => evaluation.ObligationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<EligibilityPolicyVersion>()
            .WithMany()
            .HasForeignKey(evaluation => evaluation.PolicyVersionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>()
            .WithMany()
            .HasForeignKey(evaluation => evaluation.WinnerPersonId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class EligibilityCandidateConfiguration : IEntityTypeConfiguration<EligibilityCandidate>
{
    public void Configure(EntityTypeBuilder<EligibilityCandidate> builder)
    {
        builder.ToTable(
            "eligibility_candidate",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_eligibility_candidate_hu016_fields",
                    "active_load IS NULL AND last_auto_assignment_at IS NULL AND rank IS NULL");
                table.HasCheckConstraint(
                    "CK_eligibility_candidate_stable_code",
                    "btrim(stable_code) <> ''");
                table.HasCheckConstraint(
                    "CK_eligibility_candidate_reasons",
                    "jsonb_typeof(reasons) = 'array' AND " +
                    "reasons <@ '[\"PERSONA_INACTIVA\",\"EMPLEO_NO_VIGENTE\",\"SUCURSAL_NO_COINCIDE\",\"ROL_ACTIVO_AUSENTE\",\"ROL_REQUERIDO_NO_COINCIDE\",\"DISPONIBILIDAD_AUSENTE\",\"DISPONIBILIDAD_NO_POSITIVA\",\"TURNO_NO_COINCIDE\"]'::jsonb AND " +
                    "((is_eligible AND jsonb_array_length(reasons) = 0) OR " +
                    "(NOT is_eligible AND jsonb_array_length(reasons) > 0))");
            });

        builder.HasKey(candidate => new { candidate.EvaluationId, candidate.PersonId });
        builder.Property(candidate => candidate.EvaluationId).HasColumnName("evaluation_id");
        builder.Property(candidate => candidate.PersonId).HasColumnName("person_id");
        builder.Property(candidate => candidate.IsEligible).HasColumnName("is_eligible");
        builder.Property(candidate => candidate.Reasons).HasColumnName("reasons").HasColumnType("jsonb");
        builder.Property(candidate => candidate.ActiveLoad).HasColumnName("active_load");
        builder.Property(candidate => candidate.LastAutoAssignmentAt).HasColumnName("last_auto_assignment_at").HasColumnType("timestamp with time zone");
        builder.Property(candidate => candidate.StableCode).HasColumnName("stable_code");
        builder.Property(candidate => candidate.Rank).HasColumnName("rank");

        builder.HasOne<EligibilityEvaluation>()
            .WithMany()
            .HasForeignKey(candidate => candidate.EvaluationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Person>()
            .WithMany()
            .HasForeignKey(candidate => candidate.PersonId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
