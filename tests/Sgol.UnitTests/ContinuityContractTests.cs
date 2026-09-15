using Sgol.Continuity.Contracts;
using System.Text.Json;
using Xunit;

namespace Sgol.UnitTests;

public sealed class ContinuityContractTests
{
    [Fact]
    public void CanonicalRecord_IsIndependentOfFieldOrderAndNormalizesText()
    {
        var first = FunctionalSnapshotContract.CreateRecord("person", "1",
        [
            new("name", "Cafe\u0301"),
            new("id", Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")),
        ]);
        var second = FunctionalSnapshotContract.CreateRecord("person", "1",
        [
            new("id", Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")),
            new("name", "Café"),
        ]);

        Assert.Equal(first.RowSha256, second.RowSha256);
        Assert.Equal(first.Fields.Select(item => item.Name), second.Fields.Select(item => item.Name));
    }

    [Fact]
    public void CanonicalJson_NormalizesEquivalentNumbersAndObjectOrder()
    {
        using var first = JsonDocument.Parse("{\"b\":1.000,\"a\":1e0}");
        using var second = JsonDocument.Parse("{\"a\":1,\"b\":1}");

        Assert.Equal(FunctionalSnapshotContract.HashValue(first), FunctionalSnapshotContract.HashValue(second));
    }

    [Fact]
    public void Reconcile_ReportsMissingAuditAndChangedVersionDeterministically()
    {
        var id = Guid.NewGuid();
        var branch = Guid.NewGuid();
        var at = UtcNoon();
        var expected = Snapshot(id, branch, at,
            ("audit_event", "audit-1", new Dictionary<string, object?> { ["action"] = "CREATED" }),
            ("employment_version", "employment-1", new Dictionary<string, object?> { ["row_version"] = 1L }));
        var actual = Snapshot(id, branch, at,
            ("employment_version", "employment-1", new Dictionary<string, object?> { ["row_version"] = 2L }));

        var result = FunctionalSnapshotReconciler.Compare(expected, actual);

        Assert.Equal(RecoveryReconciliationStatuses.Different, result.Status);
        Assert.Contains(result.Differences, item => item.Kind == RecoveryDifferenceKinds.AuditMissing);
        Assert.Contains(result.Differences, item => item.Kind == RecoveryDifferenceKinds.VersionChanged);
        Assert.Equal(result.Differences.OrderBy(item => item.Ordinal), result.Differences);
    }

    [Fact]
    public void Reconcile_IdenticalSnapshotsAreMatched()
    {
        var id = Guid.NewGuid();
        var branch = Guid.NewGuid();
        var at = UtcNoon();
        var snapshot = Snapshot(id, branch, at,
            ("person", "person-1", new Dictionary<string, object?> { ["id"] = Guid.NewGuid() }));

        var result = FunctionalSnapshotReconciler.Compare(snapshot, snapshot);

        Assert.Equal(RecoveryReconciliationStatuses.Matched, result.Status);
        Assert.Empty(result.Differences);
    }

    [Theory]
    [InlineData("person", "display_name", false, RecoveryDifferenceKinds.ValueChanged)]
    [InlineData("assignment_version", "person_id", false, RecoveryDifferenceKinds.LinkChanged)]
    [InlineData("employment_version", "row_version", false, RecoveryDifferenceKinds.VersionChanged)]
    [InlineData("file_object", "sha256", false, RecoveryDifferenceKinds.EvidenceCorrupt)]
    [InlineData("audit_event", "outcome", false, RecoveryDifferenceKinds.AuditAltered)]
    [InlineData("person", "display_name", true, RecoveryDifferenceKinds.IdentityMissing)]
    [InlineData("assignment_version", "person_id", true, RecoveryDifferenceKinds.LinkMissing)]
    [InlineData("evidence_item", "status", true, RecoveryDifferenceKinds.EvidenceMissing)]
    [InlineData("audit_event", "outcome", true, RecoveryDifferenceKinds.AuditMissing)]
    public void Reconcile_ReportsTheApprovedNegativeMatrix(
        string table, string field, bool removeActual, string expectedKind)
    {
        var id = Guid.NewGuid();
        var branch = Guid.NewGuid();
        var at = UtcNoon();
        var expected = Snapshot(id, branch, at,
            (table, "stable-1", new Dictionary<string, object?> { [field] = "expected" }));
        var actual = removeActual
            ? Snapshot(id, branch, at)
            : Snapshot(id, branch, at,
                (table, "stable-1", new Dictionary<string, object?> { [field] = "actual" }));

        var result = FunctionalSnapshotReconciler.Compare(expected, actual);

        Assert.Equal(RecoveryReconciliationStatuses.Different, result.Status);
        Assert.Contains(result.Differences, item => item.Kind == expectedKind);
        if (removeActual)
            Assert.Contains(result.Differences, item => item.Kind == RecoveryDifferenceKinds.CountChanged);
    }

    [Fact]
    public void Reconcile_ReportsAdditionalIdentityAndCount()
    {
        var id = Guid.NewGuid();
        var branch = Guid.NewGuid();
        var at = UtcNoon();
        var expected = Snapshot(id, branch, at);
        var actual = Snapshot(id, branch, at,
            ("person", "additional", new Dictionary<string, object?> { ["id"] = Guid.NewGuid() }));

        var result = FunctionalSnapshotReconciler.Compare(expected, actual);

        Assert.Contains(result.Differences, item => item.Kind == RecoveryDifferenceKinds.IdentityAdditional);
        Assert.Contains(result.Differences, item => item.Kind == RecoveryDifferenceKinds.CountChanged);
    }

    [Fact]
    public void Reconcile_RejectsDifferentBuildIdentity()
    {
        var snapshot = Snapshot(Guid.NewGuid(), Guid.NewGuid(), UtcNoon());
        var incompatible = snapshot with { Revision = new string('c', 40) };

        var error = Assert.Throws<RecoveryContractException>(() =>
            FunctionalSnapshotReconciler.Compare(snapshot, incompatible));

        Assert.Equal("REFERENCE_VERSION_UNSUPPORTED", error.ErrorCode);
    }

    [Fact]
    public void Reconcile_RejectsCorruptRootAndFailsWhenDifferenceLimitIsExceeded()
    {
        var id = Guid.NewGuid();
        var branch = Guid.NewGuid();
        var expected = Snapshot(id, branch, UtcNoon(),
            ("person", "one", new Dictionary<string, object?> { ["status"] = "ACTIVE" }),
            ("person", "two", new Dictionary<string, object?> { ["status"] = "ACTIVE" }));
        var corrupt = expected with { RootSha256 = new string('0', 64) };
        Assert.Equal("REFERENCE_CORRUPT", Assert.Throws<RecoveryContractException>(() =>
            FunctionalSnapshotReconciler.Compare(expected, corrupt)).ErrorCode);

        var actual = Snapshot(id, branch, UtcNoon());
        var limited = FunctionalSnapshotReconciler.Compare(expected, actual, maximumDifferences: 1);
        Assert.Equal(RecoveryReconciliationStatuses.Failed, limited.Status);
        Assert.True(limited.Truncated);
        Assert.Single(limited.Differences);
        Assert.True(limited.TotalDifferences > limited.Differences.Count);
    }

    [Fact]
    public void RecoveryObjectives_UseWorstRpoAndRejectExceededThresholds()
    {
        var target = UtcNoon();
        var result = RecoveryObjectiveCalculator.Calculate(
            target, target, target.AddMinutes(-61), target, target.AddHours(4).AddSeconds(1));

        Assert.Equal(3660, result.ObservedRpoSeconds);
        Assert.False(result.MeetsRpo);
        Assert.False(result.MeetsRto);
    }

    [Fact]
    public void RecoveryObjectives_RejectFutureArtifacts()
    {
        var target = UtcNoon();
        var exception = Assert.Throws<RecoveryContractException>(() => RecoveryObjectiveCalculator.Calculate(
            target, target.AddSeconds(1), target, target, target));
        Assert.Equal("RECOVERY_CLOCK_INCONSISTENT", exception.ErrorCode);
    }

    private static FunctionalSnapshot Snapshot(Guid id, Guid branch, DateTimeOffset at,
        params (string Table, string Key, Dictionary<string, object?> Fields)[] rows)
    {
        var records = rows.Select(row => FunctionalSnapshotContract.CreateRecord(row.Table, row.Key, row.Fields));
        return FunctionalSnapshotContract.CreateSnapshot(id, branch, at, new string('a', 40),
            "sha256:" + new string('b', 64), "20260912213000_AddPortableDataProtectionKeyRing", records);
    }

    private static DateTimeOffset UtcNoon() => new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);
}
