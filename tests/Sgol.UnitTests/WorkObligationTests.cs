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
}
