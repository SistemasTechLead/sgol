using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Web.Infrastructure.Persistence.Versioning;
using Xunit;

namespace Sgol.UnitTests;

public sealed class VersioningTests
{
    private static readonly DateTimeOffset September =
        new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PublicationAndSubstitution_PreserveHistoryAndUseApprovedStates()
    {
        var firstDraft = VersioningRules.CreateDraft(Guid.CreateVersion7());
        var firstPlan = VersioningRules.PlanPublication(
            firstDraft,
            current: null,
            publishedHistory: [],
            expectedRowVersion: 1,
            effectiveFrom: September,
            reason: " Publicación inicial ");
        var successorDraft = VersioningRules.CreateDraft(Guid.CreateVersion7());

        var successorPlan = VersioningRules.PlanPublication(
            successorDraft,
            firstPlan.Published,
            [firstPlan.Published],
            expectedRowVersion: 1,
            effectiveFrom: September.AddMonths(1),
            reason: "Nueva vigencia");

        Assert.Null(firstPlan.Superseded);
        Assert.Equal(VersionStatuses.Current, firstPlan.Published.Status);
        Assert.Equal("Publicación inicial", firstPlan.Published.Reason);
        Assert.Equal(2, firstPlan.Published.RowVersion);
        Assert.Equal(VersionStatuses.Superseded, successorPlan.Superseded!.Status);
        Assert.Equal(September.AddMonths(1), successorPlan.Superseded.EffectiveTo);
        Assert.Equal(3, successorPlan.Superseded.RowVersion);
        Assert.Equal(VersionStatuses.Current, successorPlan.Published.Status);
        Assert.Equal(firstPlan.Published.Id, successorPlan.Published.SupersedesId);
        Assert.Null(successorPlan.Published.EffectiveTo);
    }

    [Fact]
    public void InvalidStatesReasonAndOverlappingValidity_AreRejectedWithoutAPlan()
    {
        var invalidDraft = new VersionRecord(
            Guid.CreateVersion7(),
            VersionStatuses.Superseded,
            September,
            September.AddDays(1),
            "history",
            null,
            1);
        Assert.Throws<VersioningStateException>(() => VersioningRules.PlanPublication(
            invalidDraft,
            current: null,
            publishedHistory: [],
            expectedRowVersion: 1,
            effectiveFrom: September.AddDays(2),
            reason: "publish"));

        var draft = VersioningRules.CreateDraft(Guid.CreateVersion7());
        Assert.Throws<VersioningValidationException>(() => VersioningRules.PlanPublication(
            draft,
            current: null,
            publishedHistory: [],
            expectedRowVersion: 1,
            effectiveFrom: September,
            reason: " "));

        var historical = new VersionRecord(
            Guid.CreateVersion7(),
            VersionStatuses.Superseded,
            September,
            September.AddDays(10),
            "previous",
            null,
            2);
        Assert.Throws<VersioningOverlapException>(() => VersioningRules.PlanPublication(
            draft,
            current: null,
            publishedHistory: [historical],
            expectedRowVersion: 1,
            effectiveFrom: September.AddDays(5),
            reason: "overlap"));
    }

    [Fact]
    public void HalfOpenIntervals_AllowAdjacentValidityButRejectOverlap()
    {
        var first = new VersionInterval(September, September.AddDays(10));
        var adjacent = new VersionInterval(September.AddDays(10), September.AddDays(20));
        var overlapping = new VersionInterval(September.AddDays(9), September.AddDays(20));

        Assert.False(first.Overlaps(adjacent));
        Assert.True(first.Overlaps(overlapping));
        Assert.Throws<VersioningValidationException>(() =>
            new VersionInterval(September, September));
    }

    [Fact]
    public void ETag_RequiresQuotedPositiveCurrentRowVersion()
    {
        Assert.Equal("\"42\"", VersionEtag.Format(42));
        Assert.Equal(42, VersionEtag.ParseRequired("\"42\""));
        Assert.Throws<VersionIfMatchRequiredException>(() => VersionEtag.ParseRequired(null));
        Assert.Throws<VersionEtagInvalidException>(() => VersionEtag.ParseRequired("W/\"42\""));
        Assert.Throws<VersionConflictException>(() =>
            VersioningRules.RequireExpectedRowVersion(actualRowVersion: 43, expectedRowVersion: 42));
    }

    [Fact]
    public void Authorization_IsDeniedUnlessExplicitlyGranted()
    {
        Assert.Throws<VersioningAccessDeniedException>(() =>
            VersioningRules.RequireAuthorized(explicitlyAuthorized: false));

        VersioningRules.RequireAuthorized(explicitlyAuthorized: true);
    }

    [Fact]
    public void Errors_DoNotEchoReasonOrMalformedEtag()
    {
        const string secret = "synthetic-secret-value";
        var draft = VersioningRules.CreateDraft(Guid.CreateVersion7());

        var reasonError = Assert.Throws<VersioningValidationException>(() =>
            VersioningRules.PlanPublication(draft, null, [], 1, September, " "));
        var etagError = Assert.Throws<VersionEtagInvalidException>(() =>
            VersionEtag.ParseRequired(secret));

        Assert.DoesNotContain(secret, reasonError.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(secret, etagError.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void EfConfiguration_DefinesLifecycleConcurrencyAndDatabaseGuards()
    {
        using var context = new ProbeDbContext(
            new DbContextOptionsBuilder<ProbeDbContext>()
                .UseNpgsql("Host=localhost;Database=not_used;Username=not_used")
                .Options);

        var entity = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(VersionProbe))!;
        var table = entity.GetTableName()!;
        var checks = entity.GetCheckConstraints().Select(item => item.Name).ToArray();
        var indexes = entity.GetIndexes().Select(item => item.GetDatabaseName()).ToArray();

        Assert.Equal("technical_version_probe", table);
        Assert.True(entity.FindProperty(nameof(VersionProbe.RowVersion))!.IsConcurrencyToken);
        Assert.Contains("CK_technical_version_probe_lifecycle", checks);
        Assert.Contains("CK_technical_version_probe_not_self_superseding", checks);
        Assert.Contains("IX_technical_version_probe_one_current", indexes);
        Assert.Contains("IX_technical_version_probe_supersedes_id", indexes);
        Assert.Contains(entity.GetForeignKeys(), foreignKey => foreignKey.DeleteBehavior == DeleteBehavior.Restrict);
    }

    [Fact]
    public void PostgreSqlConstraintSql_UsesHalfOpenRangeAndRejectsUnsafeIdentifiers()
    {
        var sql = VersioningPostgreSql.AddNoOverlapConstraintSql(
            "technical_version_probe",
            "object_id",
            "scope_id");

        Assert.Contains("tstzrange(\"effective_from\", \"effective_to\", '[)')", sql, StringComparison.Ordinal);
        Assert.Contains("\"object_id\" WITH =", sql, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() =>
            VersioningPostgreSql.AddNoOverlapConstraintSql("probe; DROP TABLE audit_event", "object_id"));
    }

    private sealed class ProbeDbContext(DbContextOptions<ProbeDbContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var probe = modelBuilder.Entity<VersionProbe>();
            probe.HasKey(entity => entity.Id);
            probe.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedNever();
            probe.Property(entity => entity.ObjectId).HasColumnName("object_id");
            probe.Property(entity => entity.ScopeId).HasColumnName("scope_id");
            probe.ConfigureVersioning(
                "technical_version_probe",
                nameof(VersionProbe.ObjectId),
                nameof(VersionProbe.ScopeId));
        }
    }

    private sealed class VersionProbe : IVersionedEntity
    {
        public Guid Id { get; set; }

        public Guid ObjectId { get; set; }

        public Guid ScopeId { get; set; }

        public string Status { get; set; } = null!;

        public DateTimeOffset? EffectiveFrom { get; set; }

        public DateTimeOffset? EffectiveTo { get; set; }

        public string? Reason { get; set; }

        public Guid? SupersedesId { get; set; }

        public long RowVersion { get; set; }
    }
}
