using Sgol.BuildingBlocks.Identifiers;

namespace Sgol.Web.Infrastructure.Http;

public sealed class CorrelationIdMiddleware(RequestDelegate next, IHostEnvironment environment)
{
    public const string HeaderName = "X-Correlation-ID";

    private const string ItemKey = "Sgol.CorrelationId";
    private const string ServiceName = "Sgol.Web";
    private static readonly string ServiceVersion =
        typeof(CorrelationIdMiddleware).Assembly.GetName().Version?.ToString() ?? "unknown";
    private static readonly Action<ILogger, string, int, string, Exception?> LogCompleted =
        LoggerMessage.Define<string, int, string>(
            LogLevel.Information,
            new EventId(1000, "HttpOperationCompleted"),
            "HTTP operation {Operation} completed with result {Result} and correlation {CorrelationId}");

    public async Task InvokeAsync(
        HttpContext context,
        IUuidGenerator uuidGenerator,
        ILogger<CorrelationIdMiddleware> logger)
    {
        var correlationId = ReadUuid7(context.Request.Headers[HeaderName]) ?? uuidGenerator.NewUuid();
        var correlationIdText = correlationId.ToString("D");

        context.Items[ItemKey] = correlationIdText;
        context.TraceIdentifier = correlationIdText;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationIdText;
            return Task.CompletedTask;
        });

        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["Service"] = ServiceName,
            ["Environment"] = environment.EnvironmentName,
            ["Version"] = ServiceVersion,
            ["CorrelationId"] = correlationIdText,
            ["TechnicalActor"] = "unauthenticated"
        });

        try
        {
            await next(context);
        }
        finally
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                var operation = context.GetEndpoint()?.DisplayName ?? "unmatched";
                LogCompleted(logger, operation, context.Response.StatusCode, correlationIdText, null);
            }
        }
    }

    internal static string GetCorrelationId(HttpContext context) =>
        context.Items.TryGetValue(ItemKey, out var value) && value is string correlationId
            ? correlationId
            : context.TraceIdentifier;

    private static Guid? ReadUuid7(string? value)
    {
        if (!Guid.TryParse(value, out var correlationId) || correlationId.Version != 7)
        {
            return null;
        }

        return correlationId;
    }
}
