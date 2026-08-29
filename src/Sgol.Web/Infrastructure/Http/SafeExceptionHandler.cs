using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

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
        LogFailure(
            logger,
            StatusCodes.Status500InternalServerError,
            httpContext.GetCorrelationId(),
            null);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred."
            },
            Exception = exception
        });
    }
}
