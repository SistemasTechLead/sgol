using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Sgol.Web.Infrastructure.Persistence.Idempotency;

namespace Sgol.Web.Infrastructure.Http;

public sealed class SafeExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<SafeExceptionHandler> logger) : IExceptionHandler
{
    private static readonly Action<ILogger, int, string, Exception?> LogFailure =
        LoggerMessage.Define<int, string>(
            LogLevel.Error,
            new EventId(1001, "HttpOperationFailed"),
            "HTTP operation failed with result {Result} and correlation {CorrelationId}");

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var conflictAuditFailed = exception is IdempotencyConflictAuditException;
        var replayUnavailable = exception is IdempotencyReplayUnavailableException;
        var status = replayUnavailable
            ? StatusCodes.Status409Conflict
            : StatusCodes.Status500InternalServerError;
        LogFailure(
            logger,
            status,
            httpContext.GetCorrelationId(),
            null);

        httpContext.Response.StatusCode = status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = replayUnavailable
                    ? "No se puede reconstruir la respuesta idempotente"
                    : conflictAuditFailed
                        ? "No se pudo registrar el conflicto idempotente"
                        : "An unexpected error occurred.",
                Extensions = replayUnavailable
                    ? new Dictionary<string, object?>
                    {
                        ["code"] = "IDEMPOTENCY_REPLAY_NO_DISPONIBLE",
                    }
                    : conflictAuditFailed
                    ? new Dictionary<string, object?>
                    {
                        ["code"] = "IDEMPOTENCY_CONFLICT_AUDIT_FAILED",
                    }
                    : new Dictionary<string, object?>()
            },
            Exception = exception
        });
    }
}
