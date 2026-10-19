using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sgol.BuildingBlocks.Versioning;

namespace Sgol.Web.Infrastructure.Persistence.Versioning;

public static partial class VersionedEntityConfigurationExtensions
{
    public static EntityTypeBuilder<TEntity> ConfigureVersioning<TEntity>(
        this EntityTypeBuilder<TEntity> builder,
        string tableName,
        params string[] objectAndScopePropertyNames)
        where TEntity : class, IVersionedEntity
    {
        ArgumentNullException.ThrowIfNull(builder);
        ValidateIdentifier(tableName, nameof(tableName));
        if (objectAndScopePropertyNames.Length == 0)
        {
            throw new ArgumentException(
                "At least one object or scope property is required.",
                nameof(objectAndScopePropertyNames));
        }

        foreach (var propertyName in objectAndScopePropertyNames)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                throw new ArgumentException(
                    "Object and scope property names cannot be empty.",
                    nameof(objectAndScopePropertyNames));
            }

            var property = builder.Metadata.FindProperty(propertyName);
            if (property is null || property.IsShadowProperty())
            {
                throw new ArgumentException(
                    $"'{propertyName}' must be a mapped non-shadow property.",
                    nameof(objectAndScopePropertyNames));
            }
        }

        builder.Property(entity => entity.Status).HasColumnName("status");
        builder.Property(entity => entity.EffectiveFrom)
            .HasColumnName("effective_from")
            .HasColumnType("timestamp with time zone");
        builder.Property(entity => entity.EffectiveTo)
            .HasColumnName("effective_to")
            .HasColumnType("timestamp with time zone");
        builder.Property(entity => entity.Reason).HasColumnName("reason");
        builder.Property(entity => entity.SupersedesId).HasColumnName("supersedes_id");
        builder.Property(entity => entity.RowVersion)
            .HasColumnName("row_version")
            .IsConcurrencyToken();

        builder.HasOne<TEntity>()
            .WithOne()
            .HasForeignKey<TEntity>(entity => entity.SupersedesId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(objectAndScopePropertyNames)
            .IsUnique()
            .HasDatabaseName($"IX_{tableName}_one_current")
            .HasFilter("status = 'VIGENTE'");
        builder.HasIndex(entity => entity.SupersedesId)
            .IsUnique()
            .HasDatabaseName($"IX_{tableName}_supersedes_id")
            .HasFilter("supersedes_id IS NOT NULL");

        builder.ToTable(
            tableName,
            table =>
            {
                table.HasCheckConstraint(
                    $"CK_{tableName}_status",
                    "status IN ('BORRADOR', 'VIGENTE', 'SUSTITUIDA')");
                table.HasCheckConstraint($"CK_{tableName}_row_version", "row_version > 0");
                table.HasCheckConstraint(
                    $"CK_{tableName}_lifecycle",
                    "(status = 'BORRADOR' AND effective_from IS NULL AND effective_to IS NULL " +
                    "AND reason IS NULL AND supersedes_id IS NULL) OR " +
                    "(status = 'VIGENTE' AND effective_from IS NOT NULL AND effective_to IS NULL " +
                    "AND btrim(reason) <> '') OR " +
                    "(status = 'SUSTITUIDA' AND effective_from IS NOT NULL " +
                    "AND effective_to > effective_from AND btrim(reason) <> '')");
                table.HasCheckConstraint(
                    $"CK_{tableName}_not_self_superseding",
                    "supersedes_id IS NULL OR supersedes_id <> id");
            });

        return builder;
    }

    private static void ValidateIdentifier(string identifier, string parameterName)
    {
        if (!PostgreSqlIdentifierRegex().IsMatch(identifier))
        {
            throw new ArgumentException("A lowercase PostgreSQL identifier is required.", parameterName);
        }
    }

    [GeneratedRegex("^[a-z][a-z0-9_]{0,31}$", RegexOptions.CultureInvariant)]
    private static partial Regex PostgreSqlIdentifierRegex();
}
