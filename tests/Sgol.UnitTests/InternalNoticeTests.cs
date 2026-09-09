using Sgol.Notifications.Contracts;
using Xunit;

namespace Sgol.UnitTests;

public sealed class InternalNoticeTests
{
    [Fact]
    public void AssignmentNoticeHasClosedCatalogAndTransitionsOnlyOnce()
    {
        var createdAt = new DateTimeOffset(2026, 9, 9, 18, 0, 0, TimeSpan.Zero);
        var readAt = createdAt.AddMinutes(2);
        var notice = new InternalNotice(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), createdAt);

        Assert.Equal(InternalNoticeTypes.ObligationAssigned, notice.NoticeType);
        Assert.Equal(InternalNoticeResourceTypes.AssignmentVersion, notice.ResourceType);
        Assert.True(notice.MarkRead(readAt));
        Assert.False(notice.MarkRead(readAt.AddMinutes(1)));
        Assert.Equal(readAt, notice.ReadAt);
    }

    [Fact]
    public void ReadBeforeCreationIsRejectedWithoutChangingState()
    {
        var createdAt = new DateTimeOffset(2026, 9, 9, 18, 0, 0, TimeSpan.Zero);
        var notice = new InternalNotice(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), createdAt);

        Assert.Throws<ArgumentOutOfRangeException>(() => notice.MarkRead(createdAt.AddTicks(-1)));
        Assert.Null(notice.ReadAt);
    }

    [Fact]
    public void EmptyIdentifiersAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new InternalNotice(
            Guid.Empty, Guid.CreateVersion7(), Guid.CreateVersion7(), DateTimeOffset.UtcNow));
    }

    [Theory]
    [InlineData(true, -1, -1, "CONCLUIDA")]
    [InlineData(false, -1, 0, "VENCIDA")]
    [InlineData(false, 0, 0, "DISPONIBLE")]
    [InlineData(false, 1, 1, "FUTURA")]
    [InlineData(false, 1, -1, "DISPONIBLE")]
    public void TaskStateUsesApprovedPrecedence(
        bool concluded, int dueOffsetMinutes, int periodStartOffsetDays, string expected)
    {
        var queriedAt = new DateTimeOffset(2026, 9, 9, 18, 0, 0, TimeSpan.Zero);
        var localToday = new DateOnly(2026, 9, 9);
        var dueAt = queriedAt.AddMinutes(dueOffsetMinutes);
        var periodStartsOn = localToday.AddDays(periodStartOffsetDays);

        Assert.Equal(expected, InboxTaskStates.Classify(concluded, dueAt, periodStartsOn, localToday, queriedAt));
        Assert.Equal(expected, InboxTaskStates.FromRank(InboxTaskStates.Rank(expected)));
    }
}
