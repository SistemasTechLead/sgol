using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sgol.Assignment.Contracts;
using Sgol.Generation.Contracts;
using Sgol.Organization.Contracts;

namespace Sgol.JobInfrastructure;

public sealed class ManualObligationAssignmentOutboxHandler(IServiceScopeFactory scopeFactory) : IOutboxHandler
{
    public const string Type = "MANUAL_OBLIGATION_ASSIGNMENT_REQUESTED";
    public string EventType => Type;

    public bool IsPayloadValid(JsonElement data) => data.ValueKind == JsonValueKind.Object &&
        data.EnumerateObject().Count() == 1 && data.TryGetProperty("obligationId", out var value) &&
        value.ValueKind == JsonValueKind.String && Guid.TryParseExact(value.GetString(), "D", out var id) && id != Guid.Empty;

    public async Task HandleAsync(OutboxDeliveryContext context, CancellationToken cancellationToken)
    {
        if (!IsPayloadValid(context.Data) || context.EventType != Type || context.EventId == Guid.Empty ||
            context.AggregateId != context.Data.GetProperty("obligationId").GetGuid())
            throw new JobExecutionException("MANUAL_ASSIGNMENT_PAYLOAD_INVALID");

        var id = context.AggregateId!.Value;
        var source = await (from obligation in context.DbContext.WorkObligations.AsNoTracking()
                            join request in context.DbContext.GenerationRequests.AsNoTracking()
                                on obligation.GenerationRequestId equals request.Id
                            where obligation.Id == id && request.ObligationId == id &&
                                obligation.BranchId == BranchScope.LorettaId && request.BranchId == BranchScope.LorettaId &&
                                obligation.ManualTaskCode != null && obligation.ManualOriginKey != null && request.RequestedBy != null
                            select new { request.RequestedAt }).SingleOrDefaultAsync(cancellationToken)
            ?? throw new JobExecutionException("MANUAL_ASSIGNMENT_SOURCE_INVALID");
        var zone = TimeZoneInfo.FindSystemTimeZoneById(RecurringGenerationContract.TimeZone);
        var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(source.RequestedAt, zone).DateTime);

        // Business services own their transactions. A separate scope avoids nesting them in delivery's transaction.
        // The event ID is stable across a crash after either commit, so both services recover their original result.
        await using var scope = scopeFactory.CreateAsyncScope();
        var evaluation = await scope.ServiceProvider.GetRequiredService<IEligibilityEvaluationService>()
            .EvaluateAsync(new(context.EventId, context.CorrelationId, id, date, EligibilityDateSources.ManualRequest), cancellationToken);
        await scope.ServiceProvider.GetRequiredService<IAutomaticAssignmentService>()
            .AssignAsync(new(context.EventId, id, evaluation.EvaluationId, context.CorrelationId), cancellationToken);
    }
}
