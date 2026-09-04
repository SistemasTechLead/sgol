using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.JobInfrastructure;
using Sgol.Worker;
using Xunit;

namespace Sgol.UnitTests;

public sealed class JobInfrastructureTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 4, 21, 30, 0, TimeSpan.Zero);

    [Fact]
    public void WorkerCommands_AcceptOnlyTheApprovedShapes()
    {
        Assert.True(WorkerCommandParser.TryParse(["outbox"], out var outbox));
        Assert.Equal(WorkerCommandKind.Outbox, outbox!.Kind);
        Assert.True(WorkerCommandParser.TryParse(["--help"], out var help));
        Assert.Equal(WorkerCommandKind.Help, help!.Kind);
        Assert.True(WorkerCommandParser.TryParse(
            ["run-job", "--job", "TECH_TEST_JOB", "--scheduled-for", "2026-09-04T21:00:00Z"],
            out var job));
        Assert.Equal(Now.AddMinutes(-30), job!.ScheduledFor);

        Assert.False(WorkerCommandParser.TryParse([], out _));
        Assert.False(WorkerCommandParser.TryParse(["outbox", "extra"], out _));
        Assert.False(WorkerCommandParser.TryParse(
            ["run-job", "--job", "hu-013", "--scheduled-for", "2026-09-04T21:00:00Z"],
            out _));
        Assert.False(WorkerCommandParser.TryParse(
            ["run-job", "--job", "TECH_TEST_JOB", "--scheduled-for", "2026-09-04T16:00:00-05:00"],
            out _));
    }

    [Fact]
    public async Task HelpStartsWithoutConfigurationOrHttpAndReturnsSuccess()
    {
        var output = new List<string>();

        var exitCode = await WorkerApplication.RunAsync(["--help"], writeOutput: output.Add);

        Assert.Equal(0, exitCode);
        Assert.Single(output);
        Assert.Contains("Sgol.Worker outbox", output[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingConfigurationFailsSafelyBeforeStartingWork()
    {
        var configuration = new ConfigurationBuilder().Build();

        var exitCode = await WorkerApplication.RunAsync(
            ["outbox"],
            writeOutput: _ => { },
            configuration: configuration);

        Assert.Equal(69, exitCode);

        var emptyConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Sgol"] = " "
            })
            .Build();

        exitCode = await WorkerApplication.RunAsync(
            ["outbox"],
            writeOutput: _ => { },
            configuration: emptyConfiguration);

        Assert.Equal(69, exitCode);
    }

    [Fact]
    public async Task ContinuousHostStartsAndCancellationStopsItCleanlyWithoutHttp()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Sgol"] = "Host=localhost;Database=not_opened;Username=not_opened"
            })
            .Build();
        using var cancellation = new CancellationTokenSource();
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(100));

        var exitCode = await WorkerApplication.RunAsync(
            ["outbox"],
            services =>
            {
                services.RemoveAll<IOutboxLoop>();
                services.AddSingleton<IOutboxLoop, CancellationAwareLoop>();
            },
            writeOutput: _ => { },
            configuration: configuration,
            cancellationToken: cancellation.Token);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public async Task HostLifetimeStoppingCancelsContinuousWorkCleanly()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Sgol"] = "Host=localhost;Database=not_opened;Username=not_opened"
            })
            .Build();

        var exitCode = await WorkerApplication.RunAsync(
            ["outbox"],
            services =>
            {
                services.RemoveAll<IOutboxLoop>();
                services.AddSingleton<IOutboxLoop, HostStoppingLoop>();
            },
            writeOutput: _ => { },
            configuration: configuration);

        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void OutboxWriterUsesOneClockInstantAndCreatesTheExactEnvelopeWithoutSaving()
    {
        var options = new DbContextOptionsBuilder<SgolDbContext>()
            .UseNpgsql("Host=localhost;Database=not_opened;Username=not_opened")
            .Options;
        using var context = new SgolDbContext(options);
        var clock = new CountingClock(Now);
        var handler = new TestHandler();
        var writer = new OutboxWriter(
            context,
            new OutboxHandlerRegistry([handler]),
            clock,
            new FixedUuidGenerator(Guid.Parse("0199a8aa-6000-7000-8000-000000000001")));
        using var document = JsonDocument.Parse("{\"value\":1}");
        var correlationId = Guid.Parse("0199a8aa-6000-7000-8000-000000000002");

        var item = writer.Enqueue(handler.EventType, null, document.RootElement, correlationId);

        Assert.Equal(1, clock.ReadCount);
        Assert.Equal(Now, item.CreatedAt);
        Assert.Equal(Now, item.AvailableAt);
        Assert.Equal(0, item.AttemptCount);
        Assert.Equal(EntityState.Added, context.Entry(item).State);
        using var envelope = JsonDocument.Parse(item.Payload);
        Assert.Equal(3, envelope.RootElement.EnumerateObject().Count());
        Assert.Equal(1, envelope.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(correlationId, envelope.RootElement.GetProperty("correlationId").GetGuid());
        Assert.Equal(1, envelope.RootElement.GetProperty("data").GetProperty("value").GetInt32());
    }

    [Fact]
    public void OutboxWriterRejectsUnregisteredTypesAndNonObjectPayloads()
    {
        var options = new DbContextOptionsBuilder<SgolDbContext>()
            .UseNpgsql("Host=localhost;Database=not_opened;Username=not_opened")
            .Options;
        using var context = new SgolDbContext(options);
        var handler = new TestHandler();
        var writer = new OutboxWriter(
            context,
            new OutboxHandlerRegistry([handler]),
            new CountingClock(Now),
            new FixedUuidGenerator(Guid.CreateVersion7()));
        using var objectDocument = JsonDocument.Parse("{}");
        using var arrayDocument = JsonDocument.Parse("[]");

        Assert.Throws<ArgumentException>(() => writer.Enqueue(
            "UNKNOWN.V1", null, objectDocument.RootElement, Guid.CreateVersion7()));
        Assert.Throws<ArgumentException>(() => writer.Enqueue(
            handler.EventType, null, arrayDocument.RootElement, Guid.CreateVersion7()));
    }

    private sealed class TestHandler : IOutboxHandler
    {
        public string EventType => "TECH.TEST_EVENT.V1";

        public bool IsPayloadValid(JsonElement data) => data.ValueKind == JsonValueKind.Object;

        public Task HandleAsync(OutboxDeliveryContext context, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class CountingClock(DateTimeOffset utcNow) : IClock
    {
        public int ReadCount { get; private set; }

        public DateTimeOffset UtcNow
        {
            get
            {
                ReadCount++;
                return utcNow;
            }
        }
    }

    private sealed class FixedUuidGenerator(Guid value) : IUuidGenerator
    {
        public Guid NewUuid() => value;
    }

    private sealed class CancellationAwareLoop : IOutboxLoop
    {
        public Task ProbeDatabaseAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RunAsync(CancellationToken cancellationToken) =>
            Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }

    private sealed class HostStoppingLoop(IHostApplicationLifetime lifetime) : IOutboxLoop
    {
        public Task ProbeDatabaseAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RunAsync(CancellationToken cancellationToken)
        {
            lifetime.StopApplication();
            return Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }
}
