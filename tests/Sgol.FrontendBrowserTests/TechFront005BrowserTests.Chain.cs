using Microsoft.EntityFrameworkCore;
using Sgol.Evidence.Contracts;
using Sgol.Validation.Contracts;
using Xunit;

namespace Sgol.FrontendBrowserTests;

public sealed partial class TechFront005BrowserTests
{
    private async Task VerifyPersistedChainAsync()
    {
        await using var db = fixture.TechFront005Context();
        Assert.Equal(7, obligations.Count);
        foreach (var (code, id) in obligations)
        {
            var obligation = await db.WorkObligations.SingleAsync(o => o.Id == id);
            var generation = await db.GenerationRequests.SingleAsync(g => g.Id == obligation.GenerationRequestId);
            Assert.Equal(id, generation.ObligationId);
            Assert.Equal("CONCLUIDA", obligation.ExecutionStatus);
            Assert.NotNull(obligation.ConcludedAt);
            Assert.NotNull(obligation.ConcludedBy);
            Assert.True(await db.EvidencePolicyVersions.AnyAsync(p => p.Id == obligation.EvidencePolicyVersionId));
            Assert.True(await db.ValidationPolicyVersions.AnyAsync(p => p.Id == obligation.ValidationPolicyVersionId));
            var execution = await db.ExecutionResults.SingleAsync(e => e.ObligationId == id);
            var review = await db.EvidenceReviewSnapshots.SingleAsync(r => r.Id == execution.EvidenceReviewId);
            Assert.Equal(id, review.ObligationId);
            Assert.Equal(EvidenceReviewResults.Complete, review.Result);
            Assert.NotEmpty(review.EvidenceVersionIds);
            var items = await db.EvidenceItems.Where(i => i.ObligationId == id).Select(i => i.Id).ToArrayAsync();
            var versions = await db.EvidenceVersions.Where(v => items.Contains(v.EvidenceItemId)).ToArrayAsync();
            Assert.All(review.EvidenceVersionIds, versionId => Assert.Contains(versions, v => v.Id == versionId));
            foreach (var version in versions.Where(v => v.FileObjectId.HasValue))
            {
                var file = await db.FileObjects.SingleAsync(f => f.Id == version.FileObjectId);
                Assert.Equal(EvidenceFileStatuses.Clean, file.ScanStatus);
                Assert.Equal(EvidenceBucketClasses.Clean, file.BucketClass);
                Assert.Equal(version.EvidenceItemId, file.LinkedEvidenceItemId);
                Assert.NotNull(file.ScannedAt);
                Assert.NotNull(file.DetectedMediaType);
                Assert.Equal(64, file.Sha256.Length);
            }
            var requirement = await db.ValidationRequirements.SingleAsync(r => r.ObligationId == id);
            Assert.Equal(obligation.ValidationPolicyVersionId, requirement.PolicyVersionId);
            var decisions = await db.ValidationDecisionVersions.Where(v => v.RequirementId == requirement.Id)
                .OrderBy(v => v.VersionNo).ToArrayAsync();
            Assert.Equal(code == "TAR-0007" ? 3 : 1, decisions.Length);
            var current = Assert.Single(decisions, v => v.Status == ValidationStatuses.Current);
            Assert.Equal(code == "TAR-0007" ? ValidationResults.NotFulfilled : ValidationResults.Fulfilled, current.Result);
            Assert.All(decisions.Take(decisions.Length - 1), d => Assert.Equal(ValidationStatuses.Superseded, d.Status));
            for (var index = 1; index < decisions.Length; index++)
                Assert.Equal(decisions[index - 1].Id, decisions[index].SupersedesId);
            foreach (var decision in decisions)
            {
                Assert.True(await db.AssignmentVersions.AnyAsync(a => a.Id == decision.AssignmentVersionId));
                Assert.True(await db.EvidenceReviewSnapshots.AnyAsync(r => r.Id == decision.EvidenceReviewSnapshotId && r.ObligationId == id));
            }
            Assert.True(await db.AuditEvents.AnyAsync(a => a.Action == "OBLIGATION_CONCLUDED" && a.ResourceId == id));
            Assert.True(await db.AuditEvents.AnyAsync(a => a.Action == "VALIDATION_DECISION_ISSUED" && a.ResourceId == requirement.Id));
            if (code == "TAR-0007")
                Assert.Equal(2, await db.AuditEvents.CountAsync(a => a.Action == "VALIDATION_DECISION_REPLACED" && a.ResourceId == requirement.Id));
        }
        output.WriteLine("TECH_FRONT005 PERSISTED_CHAIN seven obligations, immutable evidence and validation history verified");
    }
}
