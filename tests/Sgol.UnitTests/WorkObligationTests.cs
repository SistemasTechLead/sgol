using Sgol.Generation.Contracts;
using Xunit;

namespace Sgol.UnitTests;

public sealed class WorkObligationTests
{
    [Fact]
    public void CreationPreservesApprovedSnapshotAndStartsPending()
    {
        var obligationId = Guid.CreateVersion7();
        var taskVersionId = Guid.CreateVersion7();
        var branchId = Guid.CreateVersion7();
        var periodId = Guid.CreateVersion7();
        var requestId = Guid.CreateVersion7();

        var obligation = new WorkObligation(
            obligationId,
            taskVersionId,
            branchId,
            periodId,
            requestId,
            "approved-origin");

        Assert.Equal(obligationId, obligation.Id);
        Assert.Equal(taskVersionId, obligation.TaskDefinitionVersionId);
        Assert.Equal(branchId, obligation.BranchId);
        Assert.Equal(periodId, obligation.PeriodId);
        Assert.Equal(requestId, obligation.GenerationRequestId);
        Assert.Equal("approved-origin", obligation.OriginReference);
        Assert.Equal(WorkObligationStatuses.Pending, obligation.ExecutionStatus);
        Assert.Null(obligation.InputPayload);
        Assert.Null(obligation.DueAt);
        Assert.Null(obligation.ConcludedAt);
        Assert.Null(obligation.ConcludedBy);
        Assert.Equal(1, obligation.RowVersion);
    }

    [Fact]
    public void GenerationRequestLinkIsIdempotentButCannotDiverge()
    {
        var request = new GenerationRequest(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            new string('a', 64),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "MANUAL_REFERENCE_V1",
            "approved-origin",
            Guid.CreateVersion7(),
            DateTimeOffset.UtcNow);
        var obligationId = Guid.CreateVersion7();

        request.LinkObligation(obligationId);
        request.LinkObligation(obligationId);

        Assert.Equal(obligationId, request.ObligationId);
        Assert.Throws<GenerationRequestAlreadyMaterializedException>(() =>
            request.LinkObligation(Guid.CreateVersion7()));
    }

    [Fact]
    public void ResponsibleConclusionTransitionsPendingOnceAndAdvancesVersion()
    {
        var actor = Guid.CreateVersion7();
        var concludedAt = new DateTimeOffset(2026, 9, 8, 22, 30, 0, TimeSpan.Zero);
        var obligation = NewObligation();

        obligation.Conclude(actor, concludedAt, 1);

        Assert.Equal(WorkObligationStatuses.Concluded, obligation.ExecutionStatus);
        Assert.Equal(actor, obligation.ConcludedBy);
        Assert.Equal(concludedAt, obligation.ConcludedAt);
        Assert.Equal(2, obligation.RowVersion);
        Assert.Throws<InvalidOperationException>(() => obligation.Conclude(actor, concludedAt, 2));
    }

    [Fact]
    public void ConclusionRejectsStaleVersionWithoutChangingPendingObligation()
    {
        var obligation = NewObligation();

        Assert.Throws<InvalidOperationException>(() =>
            obligation.Conclude(Guid.CreateVersion7(), DateTimeOffset.UtcNow, 2));

        Assert.Equal(WorkObligationStatuses.Pending, obligation.ExecutionStatus);
        Assert.Null(obligation.ConcludedBy);
        Assert.Null(obligation.ConcludedAt);
        Assert.Equal(1, obligation.RowVersion);
    }

    private static WorkObligation NewObligation() => new(
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        "approved-origin",
        Guid.CreateVersion7());
}
