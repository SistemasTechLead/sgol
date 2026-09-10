using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.Generation.Contracts;
using Sgol.Validation.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Validation;

public sealed class EfValidationRequirementWriter(SgolDbContext dbContext, IUuidGenerator uuidGenerator)
    : IValidationRequirementWriter
{
    public async Task EnsureAsync(EnsureValidationRequirementCommand command, CancellationToken cancellationToken = default)
    {
        if (command.PolicyVersionId is null) return;
        if (dbContext.Database.CurrentTransaction is null) throw new InvalidOperationException("A validation requirement requires the conclusion transaction.");
        var obligation = dbContext.WorkObligations.Local.SingleOrDefault(x => x.Id == command.ObligationId)
            ?? throw new InvalidOperationException("The concluded obligation must be tracked.");
        if (obligation.ExecutionStatus != WorkObligationStatuses.Concluded || obligation.ConcludedAt != command.ConcludedAt ||
            obligation.ValidationPolicyVersionId != command.PolicyVersionId)
            throw new InvalidOperationException("The validation requirement does not match the frozen conclusion.");
        if (await dbContext.ValidationRequirements.AnyAsync(x => x.ObligationId == command.ObligationId, cancellationToken)) return;
        dbContext.ValidationRequirements.Add(new(uuidGenerator.NewUuid(), command.ObligationId, command.PolicyVersionId.Value, command.ConcludedAt));
    }
}
