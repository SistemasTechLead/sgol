using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.JobInfrastructure;
using Testcontainers.PostgreSql;
using Xunit;

namespace Sgol.IntegrationTests;

public sealed class JobInfrastructurePersistenceTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 9, 4, 22, 0, 0, TimeSpan.Zero);
    private readonly PostgreSqlContainer _postgres = PostgreSqlPersistenceTests.CreateContainerForTests();

    public Task InitializeAsync() => _postgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task OutboxClaimsOneEventAcrossWorkersAndMarksItProcessedOnce()
    {
        await ResetAsync();
        var handler = new GatedHandler();
        var eventId = await InsertEventAsync(handler.EventType, Now, Now);
        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();
        var firstProcessor = Processor(firstContext, handler, new MutableClock(Now));
        var secondProcessor = Processor(secondContext, handler, new MutableClock(Now));

        var first = firstProcessor.ProcessNextAsync();
        await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var second = await secondProcessor.ProcessNextAsync();
        handler.Release.TrySetResult();

        Assert.Equal(OutboxProcessResult.NoWork, second);
        Assert.Equal(OutboxProcessResult.Processed, await first);
        await using var verification = CreateContext();
        var persisted = await verification.OutboxEvents.SingleAsync(item => item.Id == eventId);
        Assert.Equal(1, persisted.AttemptCount);
        Assert.Equal(Now, persisted.ProcessedAt);
        Assert.Null(persisted.LastError);
        Assert.Equal(1, handler.ExecutionCount);
    }

    [Fact]
    public async Task OutboxUsesStableOrderAndCompletesTheRemainderAfterPartialFailure()
    {
        await ResetAsync();
        var handler = new RecordingHandler(failFirstId: true);
        var firstId = Guid.Parse("0199a8c5-9c00-7000-8000-000000000001");
        var secondId = Guid.Parse("0199a8c5-9c00-7000-8000-000000000002");
        var thirdId = Guid.Parse("0199a8c5-9c00-7000-8000-000000000003");
        await InsertEventAsync(handler.EventType, Now.AddMinutes(-2), Now.AddMinutes(-2), secondId);
        await InsertEventAsync(handler.EventType, Now.AddMinutes(-2), Now.AddMinutes(-3), firstId);
        await InsertEventAsync(handler.EventType, Now.AddMinutes(-1), Now.AddMinutes(-1), thirdId);

        await using var context = CreateContext();
        var processor = Processor(context, handler, new MutableClock(Now));
        Assert.Equal(OutboxProcessResult.RetryScheduled, await processor.ProcessNextAsync());
        Assert.Equal(OutboxProcessResult.Processed, await processor.ProcessNextAsync());
        Assert.Equal(OutboxProcessResult.Processed, await processor.ProcessNextAsync());

        Assert.Equal([firstId, secondId, thirdId], handler.Seen);
        var failed = await context.OutboxEvents.AsNoTracking().SingleAsync(item => item.Id == firstId);
        Assert.Null(failed.ProcessedAt);
        Assert.Equal("SAFE_TEST_FAILURE", failed.LastError);
        Assert.Equal(Now.AddMinutes(1), failed.AvailableAt);
        Assert.Equal(2, await context.OutboxEvents.CountAsync(item => item.ProcessedAt != null));
    }

    [Fact]
    public async Task OutboxStopsAfterFiveFailuresWithSanitizedErrorAndNoInfiniteLoop()
    {
        await ResetAsync();
        var handler = new ThrowingHandler(new InvalidOperationException("Password=secret; SELECT private_data"));
        var eventId = await InsertEventAsync(handler.EventType, Now, Now);
        var clock = new MutableClock(Now);
        var logger = new EventLogger<OutboxProcessor>();

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            await using var context = CreateContext();
            var result = await Processor(context, handler, clock, logger).ProcessNextAsync();
            Assert.Equal(attempt == 5 ? OutboxProcessResult.Exhausted : OutboxProcessResult.RetryScheduled, result);
            var state = await context.OutboxEvents.AsNoTracking().SingleAsync(item => item.Id == eventId);
            Assert.Equal(attempt, state.AttemptCount);
            Assert.Equal("UNEXPECTED_HANDLER_FAILURE", state.LastError);
            clock.UtcNow = state.AvailableAt;
        }

        await using var finalContext = CreateContext();
        Assert.Equal(
            OutboxProcessResult.NoWork,
            await Processor(finalContext, handler, clock).ProcessNextAsync());
        Assert.Equal(5, handler.ExecutionCount);
        Assert.Contains(logger.Events, item => item.Id == 2113);
        Assert.All(logger.Messages, message =>
        {
            Assert.DoesNotContain("secret", message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("SELECT", message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task WriterRollbackLeavesNoOutboxStateAndIdempotentEffectSurvivesRepeatedDelivery()
    {
        await ResetAsync();
        var handler = new IdempotentEffectHandler();
        var clock = new MutableClock(Now);
        await using (var context = CreateContext())
        {
            await using var transaction = await context.Database.BeginTransactionAsync();
            var writer = new OutboxWriter(
                context,
                new OutboxHandlerRegistry([handler]),
                clock,
                new Uuid7Generator(clock));
            using var data = JsonDocument.Parse("{}");
            writer.Enqueue(handler.EventType, null, data.RootElement, Guid.CreateVersion7());
            await context.SaveChangesAsync();
            await transaction.RollbackAsync();
        }

        await using (var verification = CreateContext())
        {
            Assert.Empty(await verification.OutboxEvents.ToListAsync());
        }

        var eventId = await InsertEventAsync(handler.EventType, Now, Now);
        await using (var firstContext = CreateContext())
        {
            Assert.Equal(
                OutboxProcessResult.Processed,
                await Processor(firstContext, handler, clock).ProcessNextAsync());
        }

        await using (var resetContext = CreateContext())
        {
            var item = await resetContext.OutboxEvents.SingleAsync(candidate => candidate.Id == eventId);
            item.ProcessedAt = null;
            item.AttemptCount = 1;
            item.LastError = "SYNTHETIC_REDELIVERY";
            await resetContext.SaveChangesAsync();
        }

        await using (var secondContext = CreateContext())
        {
            Assert.Equal(
                OutboxProcessResult.Processed,
                await Processor(secondContext, handler, clock).ProcessNextAsync());
            Assert.Equal(1, await secondContext.ScheduledJobRuns.CountAsync(item => item.Id == eventId));
        }
    }

    [Fact]
    public async Task ScheduledRunUsesPostgresLockAndUniqueLogicalExecutionAcrossInstances()
    {
        await ResetAsync();
        var job = new GatedJob();
        var scheduledFor = Now.AddMinutes(-15);
        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();
        var firstRunner = Runner(firstContext, job);
        var secondRunner = Runner(secondContext, job);

        var first = firstRunner.RunAsync(job.Name, scheduledFor, Guid.CreateVersion7());
        await job.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var second = await secondRunner.RunAsync(job.Name, scheduledFor, Guid.CreateVersion7());
        job.Release.TrySetResult();

        Assert.Equal(ScheduledJobResult.LockBusy, second);
        Assert.Equal(ScheduledJobResult.Completed, await first);
        Assert.Equal(
            ScheduledJobResult.AlreadyCompleted,
            await secondRunner.RunAsync(job.Name, scheduledFor, Guid.CreateVersion7()));
        await using var verification = CreateContext();
        var run = await verification.ScheduledJobRuns.SingleAsync();
        Assert.Equal(ScheduledJobStatuses.Succeeded, run.Status);
        Assert.Equal(1, job.ExecutionCount);
    }

    [Fact]
    public async Task FailedScheduledRunIsRecoveredInTheSameRowAndEmitsAnOperationalAlert()
    {
        await ResetAsync();
        var job = new RecoveringJob();
        var scheduledFor = Now.AddMinutes(-30);
        var logger = new EventLogger<ScheduledJobRunner>();
        Guid originalId;
        await using (var firstContext = CreateContext())
        {
            Assert.Equal(
                ScheduledJobResult.Failed,
                await Runner(firstContext, job, logger).RunAsync(job.Name, scheduledFor, Guid.CreateVersion7()));
            var failed = await firstContext.ScheduledJobRuns.AsNoTracking().SingleAsync();
            originalId = failed.Id;
            Assert.Equal(ScheduledJobStatuses.Failed, failed.Status);
            Assert.Equal("SAFE_JOB_FAILURE", failed.Error);
        }

        await using (var secondContext = CreateContext())
        {
            Assert.Equal(
                ScheduledJobResult.Completed,
                await Runner(secondContext, job, logger).RunAsync(job.Name, scheduledFor, Guid.CreateVersion7()));
            var recovered = await secondContext.ScheduledJobRuns.AsNoTracking().SingleAsync();
            Assert.Equal(originalId, recovered.Id);
            Assert.Equal(ScheduledJobStatuses.Succeeded, recovered.Status);
            Assert.Null(recovered.Error);
        }

        Assert.Equal(2, job.ExecutionCount);
        Assert.Contains(logger.Events, item => item.Id == 2123);
    }

    [Fact]
    public async Task DatabaseRejectsDuplicateScheduledKeyAndInvalidOutboxEnvelope()
    {
        await ResetAsync();
        await using var context = CreateContext();
        var scheduledFor = Now.AddHours(-1);
        context.ScheduledJobRuns.AddRange(
            CompletedRun(Guid.CreateVersion7(), scheduledFor),
            CompletedRun(Guid.CreateVersion7(), scheduledFor));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        context.ChangeTracker.Clear();
        context.OutboxEvents.Add(new OutboxEvent
        {
            Id = Guid.CreateVersion7(),
            EventType = "TECH.INVALID_EVENT.V1",
            Payload = "{\"schemaVersion\":2,\"correlationId\":\"not-a-uuid\",\"data\":{}}",
            CreatedAt = Now,
            AvailableAt = Now
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task PostgreSqlDeadlockIsRetriedAndBothEventsReachOneCommittedEffect()
    {
        await ResetAsync();
        var left = Guid.Parse("0199a8c5-9c00-7000-8000-000000000011");
        var right = Guid.Parse("0199a8c5-9c00-7000-8000-000000000012");
        await InsertRunLockAsync(left, "TECH_LOCK_LEFT");
        await InsertRunLockAsync(right, "TECH_LOCK_RIGHT");
        var handler = new DeadlockingHandler(left, right);
        await InsertEventAsync(handler.EventType, Now, Now, aggregateId: left);
        await InsertEventAsync(handler.EventType, Now, Now, aggregateId: right);
        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();

        var results = await Task.WhenAll(
            Processor(firstContext, handler, new MutableClock(Now)).ProcessNextAsync(),
            Processor(secondContext, handler, new MutableClock(Now)).ProcessNextAsync());

        Assert.All(results, result => Assert.Equal(OutboxProcessResult.Processed, result));
        await using var verification = CreateContext();
        Assert.Equal(2, await verification.OutboxEvents.CountAsync(item => item.ProcessedAt != null));
        Assert.All(
            await verification.OutboxEvents.AsNoTracking().ToListAsync(),
            item => Assert.Equal(1, item.AttemptCount));
    }

    private async Task ResetAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
    }

    private async Task<Guid> InsertEventAsync(
        string eventType,
        DateTimeOffset availableAt,
        DateTimeOffset createdAt,
        Guid? id = null,
        Guid? aggregateId = null)
    {
        await using var context = CreateContext();
        var eventId = id ?? Guid.CreateVersion7();
        context.OutboxEvents.Add(new OutboxEvent
        {
            Id = eventId,
            EventType = eventType,
            AggregateId = aggregateId,
            Payload = JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                correlationId = Guid.CreateVersion7(),
                data = new { }
            }),
            CreatedAt = createdAt,
            AvailableAt = availableAt
        });
        await context.SaveChangesAsync();
        return eventId;
    }

    private async Task InsertRunLockAsync(Guid id, string name)
    {
        await using var context = CreateContext();
        context.ScheduledJobRuns.Add(new ScheduledJobRun
        {
            Id = id,
            JobName = name,
            ScheduledFor = Now.AddHours(-1),
            StartedAt = Now.AddHours(-1),
            EndedAt = Now.AddMinutes(-30),
            Status = ScheduledJobStatuses.Succeeded
        });
        await context.SaveChangesAsync();
    }

    private static OutboxProcessor Processor(
        SgolDbContext context,
        IOutboxHandler handler,
        IClock clock,
        Microsoft.Extensions.Logging.ILogger<OutboxProcessor>? logger = null) => new(
        context,
        new OutboxHandlerRegistry([handler]),
        clock,
        logger ?? NullLogger<OutboxProcessor>.Instance);

    private ScheduledJobRunner Runner(
        SgolDbContext context,
        IScheduledJob job,
        Microsoft.Extensions.Logging.ILogger<ScheduledJobRunner>? logger = null) => new(
        context,
        new ScheduledJobRegistry([job]),
        new MutableClock(Now),
        new Uuid7Generator(new MutableClock(Now)),
        new JobDatabaseOptions(_postgres.GetConnectionString()),
        logger ?? NullLogger<ScheduledJobRunner>.Instance);

    private static ScheduledJobRun CompletedRun(Guid id, DateTimeOffset scheduledFor) => new()
    {
        Id = id,
        JobName = "TECH_DUPLICATE_JOB",
        ScheduledFor = scheduledFor,
        StartedAt = scheduledFor,
        EndedAt = scheduledFor.AddMinutes(1),
        Status = ScheduledJobStatuses.Succeeded
    };

    private SgolDbContext CreateContext() => new(
        new DbContextOptionsBuilder<SgolDbContext>()
            .UseNpgsql(_postgres.GetConnectionString(), options => options.CommandTimeout(15))
            .Options);

    private sealed class MutableClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
    }

    private sealed class GatedHandler : IOutboxHandler
    {
        private int executionCount;
        public string EventType => "TECH.TEST_EVENT.V1";
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int ExecutionCount => executionCount;
        public bool IsPayloadValid(JsonElement data) => true;
        public async Task HandleAsync(OutboxDeliveryContext context, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref executionCount);
            Started.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
        }
    }

    private sealed class RecordingHandler(bool failFirstId) : IOutboxHandler
    {
        private int invocation;
        public string EventType => "TECH.ORDERED_EVENT.V1";
        public List<Guid> Seen { get; } = [];
        public bool IsPayloadValid(JsonElement data) => true;
        public Task HandleAsync(OutboxDeliveryContext context, CancellationToken cancellationToken)
        {
            Seen.Add(context.EventId);
            if (failFirstId && Interlocked.Increment(ref invocation) == 1)
            {
                throw new JobExecutionException("SAFE_TEST_FAILURE");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingHandler(Exception exception) : IOutboxHandler
    {
        private int executionCount;
        public string EventType => "TECH.THROWING_EVENT.V1";
        public int ExecutionCount => executionCount;
        public bool IsPayloadValid(JsonElement data) => true;
        public Task HandleAsync(OutboxDeliveryContext context, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref executionCount);
            throw exception;
        }
    }

    private sealed class IdempotentEffectHandler : IOutboxHandler
    {
        public string EventType => "TECH.IDEMPOTENT_EVENT.V1";
        public bool IsPayloadValid(JsonElement data) => true;
        public async Task HandleAsync(OutboxDeliveryContext context, CancellationToken cancellationToken)
        {
            await context.DbContext.Database.ExecuteSqlInterpolatedAsync($$"""
                INSERT INTO scheduled_job_run
                    (id, job_name, scheduled_for, started_at, ended_at, status, checkpoint, error)
                VALUES
                    ({{context.EventId}}, 'TECH_TEST_JOB', {{Now.AddMinutes(-1)}}, {{Now}}, {{Now}}, 'SUCCEEDED', NULL, NULL)
                ON CONFLICT (id) DO NOTHING
                """, cancellationToken);
        }
    }

    private sealed class GatedJob : IScheduledJob
    {
        private int executionCount;
        public string Name => "TECH_TEST_JOB";
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int ExecutionCount => executionCount;
        public async Task ExecuteAsync(ScheduledJobContext context, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref executionCount);
            Started.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
        }
    }

    private sealed class RecoveringJob : IScheduledJob
    {
        private int executionCount;
        public string Name => "TECH_RECOVERING_JOB";
        public int ExecutionCount => executionCount;
        public Task ExecuteAsync(ScheduledJobContext context, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref executionCount) == 1)
            {
                throw new JobExecutionException("SAFE_JOB_FAILURE");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class DeadlockingHandler(Guid left, Guid right) : IOutboxHandler
    {
        private readonly ConcurrentDictionary<Guid, int> attempts = new();
        private readonly TaskCompletionSource bothStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int started;
        public string EventType => "TECH.DEADLOCK_EVENT.V1";
        public bool IsPayloadValid(JsonElement data) => true;
        public async Task HandleAsync(OutboxDeliveryContext context, CancellationToken cancellationToken)
        {
            var firstAttempt = attempts.AddOrUpdate(context.EventId, 1, (_, value) => value + 1) == 1;
            var first = context.AggregateId == left ? left : right;
            var second = first == left ? right : left;
            await context.DbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT id FROM scheduled_job_run WHERE id = {first} FOR UPDATE",
                cancellationToken);
            if (firstAttempt)
            {
                if (Interlocked.Increment(ref started) == 2)
                {
                    bothStarted.TrySetResult();
                }

                await bothStarted.Task.WaitAsync(cancellationToken);
            }

            await context.DbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT id FROM scheduled_job_run WHERE id = {second} FOR UPDATE",
                cancellationToken);
        }
    }

    private sealed class EventLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
    {
        public ConcurrentBag<Microsoft.Extensions.Logging.EventId> Events { get; } = [];
        public ConcurrentBag<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Events.Add(eventId);
            Messages.Add(formatter(state, exception));
        }
    }
}
