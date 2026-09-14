using Microsoft.Extensions.Logging;

namespace Sgol.Operations;

internal static partial class OperationsLogs
{
    [LoggerMessage(3100, LogLevel.Information,
        "Operation {operation} slot {scheduledFor} result {result}", EventName = "OperationCompleted")]
    internal static partial void Completed(
        ILogger logger,
        string operation,
        DateTimeOffset scheduledFor,
        string result);

    [LoggerMessage(3101, LogLevel.Error,
        "Operation {operation} result {result} error {errorClass}", EventName = "OperationFailed")]
    internal static partial void Failed(
        ILogger logger,
        string operation,
        string result,
        string errorClass);
}
