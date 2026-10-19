namespace Sgol.Planning.Contracts;

public static class WorkPlanStatuses
{
    public const string Draft = "BORRADOR";
    public const string Published = "PUBLICADO";
}

public static class WorkPlanResults
{
    public const string Created = "CREADA";
    public const string Recovered = "RECUPERADA";
}

public sealed class WorkPlan
{
    private WorkPlan()
    {
    }

    public WorkPlan(Guid id, Guid branchId, Guid periodId)
    {
        if (id == Guid.Empty || branchId == Guid.Empty || periodId == Guid.Empty)
        {
            throw new ArgumentException("Work-plan identifiers are required.");
        }

        Id = id;
        BranchId = branchId;
        PeriodId = periodId;
        Status = WorkPlanStatuses.Draft;
        RowVersion = 1;
    }

    public Guid Id { get; private init; }

    public Guid BranchId { get; private init; }

    public Guid PeriodId { get; private init; }

    public string Status { get; private set; } = null!;

    public long RowVersion { get; private set; }

    public void ApplyPublication()
    {
        if (Status is not WorkPlanStatuses.Draft and not WorkPlanStatuses.Published)
        {
            throw new InvalidOperationException("Only a draft or published work plan can be published.");
        }

        Status = WorkPlanStatuses.Published;
        RowVersion = checked(RowVersion + 1);
    }
}

public sealed record EnsureWorkPlanCommand(
    Guid ActorUserId,
    Guid IdempotencyKey,
    Guid CorrelationId,
    Guid BranchId,
    int IsoYear,
    int IsoWeek);

public sealed record WorkPlanEnsureResult(
    string Result,
    Guid PlanId,
    Guid BranchId,
    string BranchCode,
    Guid PeriodId,
    int IsoYear,
    int IsoWeek,
    string Status,
    long RowVersion);

public interface IWorkPlanService
{
    Task<WorkPlanEnsureResult> EnsureAsync(
        EnsureWorkPlanCommand command,
        CancellationToken cancellationToken = default);
}

public abstract class WorkPlanException(string errorCode, int responseCode, string message)
    : Exception(message)
{
    public string ErrorCode { get; } = errorCode;

    public int ResponseCode { get; } = responseCode;
}

public sealed class WorkPlanAccessDeniedException()
    : WorkPlanException("ACCESO_DENEGADO", 403, "PER-PLAN-VER is required for LOR-001.");

public sealed class WorkPlanIsoWeekInvalidException()
    : WorkPlanException("SEMANA_ISO_INVALIDA", 422, "The ISO week is invalid.");

public sealed class WorkPlanPeriodNotFoundException()
    : WorkPlanException("PERIODO_NO_ENCONTRADO", 404, "The week period does not exist in LOR-001.");

public sealed class WorkPlanPeriodIncompatibleException()
    : WorkPlanException("PERIODO_INCOMPATIBLE", 409, "The week period is incompatible with the route.");

public sealed class WorkPlanIdempotencyConflictException()
    : WorkPlanException("IDEMPOTENCY_CONFLICT", 409, "The idempotency key was used with different content.");

public sealed class WorkPlanConcurrencyConflictException()
    : WorkPlanException("WORK_PLAN_CONCURRENCY_CONFLICT", 409, "The work plan could not be ensured concurrently.");

public sealed class WorkPlanConflictException()
    : WorkPlanException("WORK_PLAN_CONFLICT", 409, "The work plan conflicts with persisted integrity.");
