using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Configuration.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Configuration;

public sealed class EfCapturedEvidencePolicyReader(SgolDbContext db) : ICapturedEvidencePolicyReader
{
    public async Task<CapturedEvidencePolicy> ReadAsync(Guid policyVersionId, Guid taskDefinitionVersionId,
        string taskCode, CancellationToken cancellationToken = default)
    {
        var definition = TaskDefinitionCatalog.Require(taskCode);
        var policy = await db.EvidencePolicyVersions.AsNoTracking().SingleOrDefaultAsync(
            p => p.Id == policyVersionId && p.TaskDefinitionVersionId == taskDefinitionVersionId &&
                p.TaskDefinitionId == definition.Id, cancellationToken);
        if (policy is null || policy.Status is not (VersionStatuses.Current or VersionStatuses.Superseded))
            throw new EvidencePolicyValidationException("Captured policy is inconsistent.");
        var rows = await db.EvidenceRequirementVersions.AsNoTracking().Where(r => r.PolicyVersionId == policyVersionId)
            .OrderBy(r => r.Ordinal).ToListAsync(cancellationToken);
        var expected = EvidencePolicyCatalog.Require(taskCode);
        if (rows.Count != expected.Count || rows.Select(r => r.Id).Distinct().Count() != rows.Count ||
            rows.Where((r, i) => !r.IsRequired || r.TaskDefinitionId != definition.Id ||
                r.RequirementCode != expected[i].Code || r.Kind != expected[i].Kind ||
                r.ConditionCode != expected[i].ConditionCode || r.Ordinal != expected[i].Ordinal).Any())
            throw new EvidencePolicyValidationException("Captured requirements are inconsistent.");
        return new(policy.Id, rows.Select(r => new CapturedEvidenceRequirement(r.Id, r.RequirementCode,
            r.Kind, r.ConditionCode, r.Ordinal)).ToArray());
    }
}
