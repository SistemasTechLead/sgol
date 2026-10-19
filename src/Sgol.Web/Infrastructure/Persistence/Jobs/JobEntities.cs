namespace Sgol.JobInfrastructure;

public sealed class OutboxEvent
{
    public Guid Id { get; set; }

    public string EventType { get; set; } = string.Empty;

    public Guid? AggregateId { get; set; }

    public string Payload { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset AvailableAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public int AttemptCount { get; set; }

    public string? LastError { get; set; }
}

public sealed class ScheduledJobRun
{
    public Guid Id { get; set; }

    public string JobName { get; set; } = string.Empty;

    public DateTimeOffset ScheduledFor { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? EndedAt { get; set; }

    public string Status { get; set; } = ScheduledJobStatuses.Running;

    public string? Checkpoint { get; set; }

    public string? Error { get; set; }
}

public static class ScheduledJobStatuses
{
    public const string Running = "RUNNING";
    public const string Succeeded = "SUCCEEDED";
    public const string Failed = "FAILED";
}
