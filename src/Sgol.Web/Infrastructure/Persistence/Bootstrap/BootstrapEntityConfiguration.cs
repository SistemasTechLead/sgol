using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgol.Organization.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Bootstrap;

internal sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("branch");
        builder.HasKey(branch => branch.Id);
        builder.Property(branch => branch.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(branch => branch.Code).HasColumnName("code");
        builder.Property(branch => branch.Name).HasColumnName("name");
        builder.Property(branch => branch.Status).HasColumnName("status");
        builder.Property(branch => branch.TimeZone).HasColumnName("timezone");
        builder.HasIndex(branch => branch.Code).IsUnique();
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_branch_mvp_code",
            "code = 'LOR-001'"));
        builder.HasData(new Branch
        {
            Id = BranchScope.LorettaId,
            Code = BranchScope.LorettaCode,
            Name = BranchScope.LorettaName,
            Status = BranchScope.ActiveStatus,
            TimeZone = BranchScope.TimeZone,
        });
    }
}

internal sealed class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.ToTable("person");
        builder.HasKey(person => person.Id);
        builder.Property(person => person.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(person => person.StableCode).HasColumnName("stable_code");
        builder.Property(person => person.DisplayName).HasColumnName("display_name");
        builder.Property(person => person.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        builder.HasIndex(person => person.StableCode).IsUnique();
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_person_stable_code_not_blank",
            "btrim(stable_code) <> ''"));
    }
}

internal sealed class EmploymentVersionConfiguration : IEntityTypeConfiguration<EmploymentVersion>
{
    public void Configure(EntityTypeBuilder<EmploymentVersion> builder)
    {
        builder.ToTable("employment_version");
        builder.HasKey(employment => employment.Id);
        builder.Property(employment => employment.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(employment => employment.PersonId).HasColumnName("person_id");
        builder.Property(employment => employment.BranchId).HasColumnName("branch_id");
        builder.Property(employment => employment.Status).HasColumnName("status");
        builder.Property(employment => employment.PositionText).HasColumnName("position_text");
        builder.Property(employment => employment.ShiftText).HasColumnName("shift_text");
        builder.Property(employment => employment.ValidFrom).HasColumnName("valid_from").HasColumnType("timestamp with time zone");
        builder.Property(employment => employment.ValidTo).HasColumnName("valid_to").HasColumnType("timestamp with time zone");
        builder.Property(employment => employment.SupersedesId).HasColumnName("supersedes_id");
        builder.Property(employment => employment.RowVersion).HasColumnName("row_version").IsConcurrencyToken();
        builder.HasOne<Person>().WithMany().HasForeignKey(employment => employment.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(employment => employment.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<EmploymentVersion>().WithMany().HasForeignKey(employment => employment.SupersedesId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(employment => new { employment.PersonId, employment.BranchId })
            .IsUnique()
            .HasFilter("valid_to IS NULL");
        builder.HasIndex(employment => employment.SupersedesId)
            .IsUnique()
            .HasFilter("supersedes_id IS NOT NULL");
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_employment_version_status",
            "status IN ('ACTIVA', 'INACTIVA')"));
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_employment_version_interval",
            "valid_to IS NULL OR valid_to >= valid_from"));
    }
}

internal sealed class AvailabilityDayVersionConfiguration : IEntityTypeConfiguration<AvailabilityDayVersion>
{
    public void Configure(EntityTypeBuilder<AvailabilityDayVersion> builder)
    {
        builder.ToTable("availability_day_version");
        builder.HasKey(availability => availability.Id);
        builder.Property(availability => availability.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(availability => availability.PersonId).HasColumnName("person_id");
        builder.Property(availability => availability.BranchId).HasColumnName("branch_id");
        builder.Property(availability => availability.LocalDate).HasColumnName("local_date").HasColumnType("date");
        builder.Property(availability => availability.IsAvailable).HasColumnName("is_available");
        builder.Property(availability => availability.Status).HasColumnName("status");
        builder.Property(availability => availability.SupersedesId).HasColumnName("supersedes_id");
        builder.Property(availability => availability.ChangedBy).HasColumnName("changed_by");
        builder.Property(availability => availability.RowVersion).HasColumnName("row_version").HasDefaultValue(1L).IsConcurrencyToken();
        builder.HasOne<Person>().WithMany().HasForeignKey(availability => availability.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(availability => availability.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(availability => availability.ChangedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AvailabilityDayVersion>().WithMany().HasForeignKey(availability => availability.SupersedesId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(availability => new { availability.PersonId, availability.BranchId, availability.LocalDate })
            .IsUnique()
            .HasFilter("status = 'VIGENTE'");
        builder.HasIndex(availability => availability.SupersedesId)
            .IsUnique()
            .HasFilter("supersedes_id IS NOT NULL");
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_availability_day_version_status",
            "status IN ('VIGENTE', 'HISTORICA')"));
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_availability_day_version_row_version",
            "row_version >= 1"));
    }
}

public sealed class IdempotencyRecord
{
    public required string Scope { get; init; }

    public Guid Key { get; init; }

    public required string RequestHash { get; init; }

    public required string Status { get; init; }

    public required string ResourceType { get; init; }

    public Guid ResourceId { get; init; }

    public int ResponseCode { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset ExpiresAt { get; init; }

    public short? ProtocolVersion { get; init; }

    public JsonDocument? ResponsePayload { get; init; }

    public string? ResponseEtag { get; init; }

    public string? ResponseLocation { get; init; }
}

internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("idempotency_record");
        builder.HasKey(record => new { record.Scope, record.Key });
        builder.Property(record => record.Scope).HasColumnName("scope");
        builder.Property(record => record.Key).HasColumnName("key").ValueGeneratedNever();
        builder.Property(record => record.RequestHash).HasColumnName("request_hash").HasColumnType("character(64)");
        builder.Property(record => record.Status).HasColumnName("status");
        builder.Property(record => record.ResourceType).HasColumnName("resource_type");
        builder.Property(record => record.ResourceId).HasColumnName("resource_id");
        builder.Property(record => record.ResponseCode).HasColumnName("response_code");
        builder.Property(record => record.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        builder.Property(record => record.ExpiresAt).HasColumnName("expires_at").HasColumnType("timestamp with time zone");
        builder.Property(record => record.ProtocolVersion).HasColumnName("protocol_version");
        builder.Property(record => record.ResponsePayload).HasColumnName("response_payload").HasColumnType("jsonb");
        builder.Property(record => record.ResponseEtag).HasColumnName("response_etag");
        builder.Property(record => record.ResponseLocation).HasColumnName("response_location");
        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_idempotency_record_protocol",
                "protocol_version IS NULL OR (protocol_version = 1 AND status = 'COMPLETED' AND response_payload IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_idempotency_record_response_location",
                "response_location IS NULL OR (response_location LIKE '/api/v1/%' AND response_location !~ '://')");
        });
    }
}

internal sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("app_user");
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(user => user.PersonId).HasColumnName("person_id");
        builder.Property(user => user.Status).HasColumnName("status");
        builder.Property(user => user.MustChangePassword).HasColumnName("must_change_password");
        builder.Property(user => user.MfaEnrolledAt).HasColumnName("mfa_enrolled_at").HasColumnType("timestamp with time zone");
        builder.Property(user => user.SecurityStamp).HasColumnName("security_stamp");
        builder.Property(user => user.AccessFailedCount).HasColumnName("access_failed_count").HasDefaultValue(0);
        builder.Property(user => user.LockoutLevel).HasColumnName("lockout_level").HasDefaultValue(0);
        builder.Property(user => user.LockoutEndUtc).HasColumnName("lockout_end_utc").HasColumnType("timestamp with time zone");
        builder.Property(user => user.AuthenticationRowVersion).HasColumnName("authentication_row_version").HasDefaultValue(1L).IsConcurrencyToken();
        builder.Ignore(user => user.RequiresFirstAccessSetup);
        builder.HasOne<Person>().WithMany().HasForeignKey(user => user.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(user => user.PersonId).IsUnique();
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_app_user_access_failed_count", "access_failed_count >= 0");
            table.HasCheckConstraint("CK_app_user_lockout_level", "lockout_level >= 0");
            table.HasCheckConstraint("CK_app_user_authentication_row_version", "authentication_row_version >= 1");
        });
    }
}

internal sealed class AuthenticationChallengeConfiguration : IEntityTypeConfiguration<AuthenticationChallenge>
{
    public void Configure(EntityTypeBuilder<AuthenticationChallenge> builder)
    {
        builder.ToTable("authentication_challenge");
        builder.HasKey(challenge => challenge.Id);
        builder.Property(challenge => challenge.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(challenge => challenge.UserId).HasColumnName("user_id");
        builder.Property(challenge => challenge.Purpose).HasColumnName("purpose");
        builder.Property(challenge => challenge.Status).HasColumnName("status");
        builder.Property(challenge => challenge.SecurityStampSnapshot).HasColumnName("security_stamp_snapshot");
        builder.Property(challenge => challenge.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        builder.Property(challenge => challenge.ExpiresAt).HasColumnName("expires_at").HasColumnType("timestamp with time zone");
        builder.Property(challenge => challenge.ConsumedAt).HasColumnName("consumed_at").HasColumnType("timestamp with time zone");
        builder.Property(challenge => challenge.FailedAttemptCount).HasColumnName("failed_attempt_count").HasDefaultValue(0);
        builder.Property(challenge => challenge.ProtectedTotpSecret).HasColumnName("protected_totp_secret");
        builder.Property(challenge => challenge.RowVersion).HasColumnName("row_version").HasDefaultValue(1L).IsConcurrencyToken();
        builder.HasOne<AppUser>().WithMany().HasForeignKey(challenge => challenge.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(challenge => new { challenge.UserId, challenge.Status, challenge.ExpiresAt });
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_authentication_challenge_purpose", "purpose IN ('CHANGE_PASSWORD', 'ENROLL_MFA', 'VERIFY_MFA', 'REGENERATE_RECOVERY_CODES')");
            table.HasCheckConstraint("CK_authentication_challenge_status", "status IN ('PENDING', 'CONSUMED', 'EXPIRED')");
            table.HasCheckConstraint("CK_authentication_challenge_failed_attempt_count", "failed_attempt_count >= 0");
            table.HasCheckConstraint("CK_authentication_challenge_interval", "expires_at > created_at");
            table.HasCheckConstraint("CK_authentication_challenge_row_version", "row_version >= 1");
        });
    }
}

internal sealed class MfaTotpCredentialConfiguration : IEntityTypeConfiguration<MfaTotpCredential>
{
    public void Configure(EntityTypeBuilder<MfaTotpCredential> builder)
    {
        builder.ToTable("mfa_totp_credential");
        builder.HasKey(credential => credential.Id);
        builder.Property(credential => credential.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(credential => credential.UserId).HasColumnName("user_id");
        builder.Property(credential => credential.ProtectedSecret).HasColumnName("protected_secret");
        builder.Property(credential => credential.ProtectionVersion).HasColumnName("protection_version");
        builder.Property(credential => credential.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        builder.Property(credential => credential.EnrolledAt).HasColumnName("enrolled_at").HasColumnType("timestamp with time zone");
        builder.Property(credential => credential.RevokedAt).HasColumnName("revoked_at").HasColumnType("timestamp with time zone");
        builder.Property(credential => credential.LastAcceptedTimeStep).HasColumnName("last_accepted_time_step");
        builder.Property(credential => credential.RowVersion).HasColumnName("row_version").HasDefaultValue(1L).IsConcurrencyToken();
        builder.HasOne<AppUser>().WithMany().HasForeignKey(credential => credential.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(credential => credential.UserId).IsUnique().HasFilter("revoked_at IS NULL");
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_mfa_totp_credential_protection_version", "protection_version = 1");
            table.HasCheckConstraint("CK_mfa_totp_credential_row_version", "row_version >= 1");
        });
    }
}

internal sealed class MfaRecoveryCodeConfiguration : IEntityTypeConfiguration<MfaRecoveryCode>
{
    public void Configure(EntityTypeBuilder<MfaRecoveryCode> builder)
    {
        builder.ToTable("mfa_recovery_code");
        builder.HasKey(code => code.Id);
        builder.Property(code => code.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(code => code.UserId).HasColumnName("user_id");
        builder.Property(code => code.CodeHash).HasColumnName("code_hash");
        builder.Property(code => code.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        builder.Property(code => code.ConsumedAt).HasColumnName("consumed_at").HasColumnType("timestamp with time zone");
        builder.Property(code => code.RevokedAt).HasColumnName("revoked_at").HasColumnType("timestamp with time zone");
        builder.Property(code => code.RowVersion).HasColumnName("row_version").HasDefaultValue(1L).IsConcurrencyToken();
        builder.HasOne<AppUser>().WithMany().HasForeignKey(code => code.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(code => new { code.UserId, code.ConsumedAt, code.RevokedAt });
        builder.ToTable(table => table.HasCheckConstraint("CK_mfa_recovery_code_row_version", "row_version >= 1"));
    }
}

internal sealed class IdentityCredentialConfiguration : IEntityTypeConfiguration<IdentityCredential>
{
    public void Configure(EntityTypeBuilder<IdentityCredential> builder)
    {
        builder.ToTable("identity_credential");
        builder.HasKey(credential => credential.UserId);
        builder.Property(credential => credential.UserId).HasColumnName("user_id").ValueGeneratedNever();
        builder.Property(credential => credential.UserName).HasColumnName("user_name");
        builder.Property(credential => credential.NormalizedUserName).HasColumnName("normalized_user_name");
        builder.Property(credential => credential.PasswordHash).HasColumnName("password_hash");
        builder.HasOne<AppUser>().WithOne().HasForeignKey<IdentityCredential>(credential => credential.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(credential => credential.NormalizedUserName).IsUnique();
    }
}

internal sealed class RoleAssignmentVersionConfiguration : IEntityTypeConfiguration<RoleAssignmentVersion>
{
    public void Configure(EntityTypeBuilder<RoleAssignmentVersion> builder)
    {
        builder.ToTable("role_assignment_version");
        builder.HasKey(role => role.Id);
        builder.Property(role => role.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(role => role.UserId).HasColumnName("user_id");
        builder.Property(role => role.BranchId).HasColumnName("branch_id");
        builder.Property(role => role.RoleCode).HasColumnName("role_code");
        builder.Property(role => role.Status).HasColumnName("status");
        builder.Property(role => role.ValidFrom).HasColumnName("valid_from").HasColumnType("timestamp with time zone");
        builder.Property(role => role.ValidTo).HasColumnName("valid_to").HasColumnType("timestamp with time zone");
        builder.Property(role => role.SupersedesId).HasColumnName("supersedes_id");
        builder.Property(role => role.RowVersion).HasColumnName("row_version").HasDefaultValue(1L).IsConcurrencyToken();
        builder.HasOne<AppUser>().WithMany().HasForeignKey(role => role.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(role => role.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<RoleAssignmentVersion>().WithMany().HasForeignKey(role => role.SupersedesId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(role => new { role.UserId, role.BranchId })
            .IsUnique()
            .HasFilter("status = 'ACTIVO'");
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_role_assignment_version_role_code",
            "role_code IN ('DIRECCION', 'ADMINISTRACION', 'SUBCOORDINACION', 'PISO_VENTAS')"));
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_role_assignment_version_status",
            "status IN ('ACTIVO', 'SUSTITUIDO')"));
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_role_assignment_version_interval",
            "(status = 'ACTIVO' AND valid_to IS NULL) OR (status = 'SUSTITUIDO' AND valid_to IS NOT NULL AND valid_to >= valid_from)"));
    }
}

internal sealed class DirectionBootstrapMarkerConfiguration : IEntityTypeConfiguration<DirectionBootstrapMarker>
{
    public void Configure(EntityTypeBuilder<DirectionBootstrapMarker> builder)
    {
        builder.ToTable("direction_bootstrap");
        builder.HasKey(marker => marker.Singleton);
        builder.Property(marker => marker.Singleton).HasColumnName("singleton").ValueGeneratedNever();
        builder.Property(marker => marker.CompletedAt).HasColumnName("completed_at").HasColumnType("timestamp with time zone");
        builder.ToTable(table => table.HasCheckConstraint("CK_direction_bootstrap_singleton", "singleton"));
    }
}
