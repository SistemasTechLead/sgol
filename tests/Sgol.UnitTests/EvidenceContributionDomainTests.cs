using System.Text.Json;
using Sgol.Evidence.Contracts;
using Xunit;

namespace Sgol.UnitTests;

public sealed class EvidenceContributionDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 7, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CleanFileCanBeLinkedExactlyOnce()
    {
        var file = NewFile();
        file.ConfirmUpload(Now.AddMinutes(1));
        file.MarkClean("image/png", "ClamAV", Now.AddMinutes(2));
        var itemId = Guid.CreateVersion7();

        file.Link(itemId, Now.AddMinutes(3));

        Assert.Equal(EvidenceFileStatuses.Clean, file.ScanStatus);
        Assert.Equal(EvidenceBucketClasses.Clean, file.BucketClass);
        Assert.Equal(itemId, file.LinkedEvidenceItemId);
        Assert.Throws<EvidenceFileNotCleanException>(() => file.Link(Guid.CreateVersion7(), Now.AddMinutes(4)));
    }

    [Theory]
    [InlineData(EvidenceFileStatuses.Infected)]
    [InlineData(EvidenceFileStatuses.Invalid)]
    [InlineData(EvidenceFileStatuses.ScanError)]
    public void NonCleanTerminalFileCanNeverBeLinked(string status)
    {
        var file = NewFile();
        file.ConfirmUpload(Now.AddMinutes(1));
        file.MarkTerminal(status, null, "ClamAV", "SAFE_ERROR", Now.AddMinutes(2));

        Assert.Throws<EvidenceFileNotCleanException>(() => file.Link(Guid.CreateVersion7(), Now.AddMinutes(3)));
    }

    [Fact]
    public void ReplacementPreservesPredecessorAndLinearVersionNumbers()
    {
        var itemId = Guid.CreateVersion7();
        var first = new EvidenceVersion(Guid.CreateVersion7(), itemId, 1, Guid.CreateVersion7(), Guid.CreateVersion7(), Now);
        first.Supersede();
        var second = new EvidenceVersion(Guid.CreateVersion7(), itemId, 2, Guid.CreateVersion7(), Guid.CreateVersion7(),
            Now.AddMinutes(1), "Motivo válido", first.Id);

        Assert.Equal(EvidenceVersionStatuses.Superseded, first.Status);
        Assert.Equal(EvidenceVersionStatuses.Current, second.Status);
        Assert.Equal(first.Id, second.SupersedesId);
        Assert.Equal(2, second.VersionNo);
    }

    [Fact]
    public void ItemEtagRejectsStaleReplacement()
    {
        var item = new EvidenceItem(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            Guid.CreateVersion7(), "FOTOGRAFIA_FINAL", "FOTOGRAFIA", Now);
        item.Advance(1);

        Assert.Equal(2, item.RowVersion);
        Assert.Throws<EvidenceVersionConflictException>(() => item.Advance(1));
    }

    [Fact]
    public void StructuredReplacementPreservesImmutablePayloadAndChain()
    {
        var itemId = Guid.CreateVersion7();
        using var firstPayload = JsonDocument.Parse("""{"schemaVersion":1,"merchandiseReference":"MER-01"}""");
        using var secondPayload = JsonDocument.Parse("""{"schemaVersion":1,"merchandiseReference":"MER-02"}""");
        var first = new EvidenceVersion(Guid.CreateVersion7(), itemId, 1, firstPayload, Guid.CreateVersion7(), Now);
        first.Supersede();
        var second = new EvidenceVersion(Guid.CreateVersion7(), itemId, 2, secondPayload, Guid.CreateVersion7(),
            Now.AddMinutes(1), null, first.Id);

        Assert.Null(first.FileObjectId);
        Assert.Equal("MER-01", first.StructuredPayload!.RootElement.GetProperty("merchandiseReference").GetString());
        Assert.Equal(EvidenceVersionStatuses.Superseded, first.Status);
        Assert.Equal(EvidenceVersionStatuses.Current, second.Status);
        Assert.Equal(first.Id, second.SupersedesId);
    }

    private static FileObject NewFile() => new(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
        Guid.CreateVersion7(), Guid.CreateVersion7(), "FOTOGRAFIA_FINAL", "FOTOGRAFIA", null,
        "v1/00/01/000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f",
        "evidence.png", "image/png", 128, new string('a', 64), Guid.CreateVersion7(), Now);
}
