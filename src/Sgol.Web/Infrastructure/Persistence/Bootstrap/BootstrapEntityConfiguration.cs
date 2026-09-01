using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

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
        builder.HasData(new Branch
        {
            Id = BootstrapContract.LorettaBranchId,
            Code = BootstrapContract.LorettaBranchCode,
            Name = "Loretta",
            Status = "ACTIVA",
            TimeZone = "America/Mexico_City",
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
            .HasFilter("status = 'ACTIVA'");
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
        builder.Ignore(user => user.RequiresFirstAccessSetup);
        builder.HasOne<Person>().WithMany().HasForeignKey(user => user.PersonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(user => user.PersonId).IsUnique();
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
        builder.HasOne<AppUser>().WithMany().HasForeignKey(role => role.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Branch>().WithMany().HasForeignKey(role => role.BranchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<RoleAssignmentVersion>().WithMany().HasForeignKey(role => role.SupersedesId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(role => new { role.UserId, role.BranchId })
            .IsUnique()
            .HasFilter("status = 'ACTIVO'");
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_role_assignment_version_role_code",
            "role_code IN ('DIRECCION', 'ADMINISTRACION', 'SUBCOORDINACION', 'PISO_VENTAS')"));
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
