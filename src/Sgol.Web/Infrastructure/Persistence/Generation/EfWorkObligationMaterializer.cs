using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.Configuration.Contracts;
using Sgol.Generation.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence.Auditing;

namespace Sgol.Web.Infrastructure.Persistence.Generation;

public sealed class EfWorkObligationMaterializer(
    SgolDbContext dbContext,
    AuditTransaction auditTransaction,
    IClock clock,
    IUuidGenerator uuidGenerator) : IWorkObligationMaterializer
{
    public async Task<WorkObligationDetails> MaterializeAsync(
        MaterializeWorkObligationCommand command,
        CancellationToken cancellationToken = default)
    {
        WorkObligation? selected = null;
        var recovered = false;

        try
        {
            await auditTransaction.ExecuteAsync(
                async token =>
                {
                    var request = await dbContext.GenerationRequests
                        .FromSqlInterpolated($"SELECT * FROM generation_request WHERE id = {command.GenerationRequestId} FOR UPDATE")
                        .SingleOrDefaultAsync(token)
                        ?? throw new GenerationRequestNotFoundException();

                    if (!string.Equals(request.Result, GenerationRequestResults.Accepted, StringComparison.Ordinal))
                    {
                        throw new GenerationRequestNotAcceptedException();
                    }

                    if (request.ObligationId is Guid obligationId)
                    {
                        selected = await dbContext.WorkObligations.AsNoTracking()
                            .SingleAsync(item =>
                                item.Id == obligationId &&
                                item.GenerationRequestId == request.Id,
                                token);
                        recovered = true;
                    }
                    else
                    {
                        var rule = await dbContext.ActivationRuleVersions.AsNoTracking()
                            .SingleAsync(item => item.Id == request.RuleVersionId, token);
                        selected = new WorkObligation(
                            uuidGenerator.NewUuid(),
                            rule.TaskDefinitionVersionId,
                            request.BranchId,
                            request.PeriodId,
                            request.Id,
                            request.OriginReference);
                        dbContext.WorkObligations.Add(selected);
                        request.LinkObligation(selected.Id);
                    }

                    return NewAuditEvent(command, selected, recovered);
                },
                cancellationToken);
        }
        catch
        {
            dbContext.ChangeTracker.Clear();
            throw;
        }

        var result = ToDetails(selected!);
        dbContext.ChangeTracker.Clear();
        return result;
    }

    private AuditEvent NewAuditEvent(
        MaterializeWorkObligationCommand command,
        WorkObligation obligation,
        bool recovered) => new()
        {
            Id = uuidGenerator.NewUuid(),
            OccurredAt = clock.UtcNow,
            ActorType = "SYSTEM",
            Action = recovered ? "WORK_OBLIGATION_RECOVERED" : "WORK_OBLIGATION_CREATED",
            ResourceType = "WORK_OBLIGATION",
            ResourceId = obligation.Id,
            BranchId = BranchScope.LorettaId,
            CorrelationId = command.CorrelationId,
            AfterData = Serialize(obligation),
            Outcome = "SUCCESS",
        };

    private static JsonDocument Serialize(WorkObligation obligation) =>
        JsonSerializer.SerializeToDocument(new
        {
            schemaVersion = 1,
            obligationId = obligation.Id,
            obligation.GenerationRequestId,
            obligation.TaskDefinitionVersionId,
            obligation.BranchId,
            obligation.PeriodId,
            obligation.OriginReference,
            obligation.ExecutionStatus,
            obligation.RowVersion,
        });

    private static WorkObligationDetails ToDetails(WorkObligation obligation) => new(
        obligation.Id,
        obligation.GenerationRequestId,
        obligation.TaskDefinitionVersionId,
        obligation.BranchId,
        obligation.PeriodId,
        obligation.OriginReference,
        obligation.ExecutionStatus,
        obligation.RowVersion);
}
