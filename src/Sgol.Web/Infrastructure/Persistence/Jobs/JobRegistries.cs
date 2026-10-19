namespace Sgol.JobInfrastructure;

public sealed class OutboxHandlerRegistry(IEnumerable<IOutboxHandler> handlers)
{
    private readonly Dictionary<string, IOutboxHandler> handlers = handlers
        .ToDictionary(handler => handler.EventType, StringComparer.Ordinal);

    public bool TryGet(string eventType, out IOutboxHandler? handler) =>
        handlers.TryGetValue(eventType, out handler);
}

public sealed class ScheduledJobRegistry(IEnumerable<IScheduledJob> jobs)
{
    private readonly Dictionary<string, IScheduledJob> jobs = jobs
        .ToDictionary(job => job.Name, StringComparer.Ordinal);

    public bool TryGet(string jobName, out IScheduledJob? job) => jobs.TryGetValue(jobName, out job);
}
