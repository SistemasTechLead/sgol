using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sgol.BuildingBlocks.Time;
using Sgol.BuildingBlocks.Versioning;
using Sgol.Configuration.Contracts;
using Sgol.Generation.Contracts;
using Sgol.Identity.Contracts;
using Sgol.JobInfrastructure;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Generation;
using Testcontainers.PostgreSql;
using Xunit;

namespace Sgol.IntegrationTests;

public sealed class RecurringGenerationPersistenceTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 4, 23, 15, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset EffectiveFrom =
        new(2026, 9, 1, 6, 0, 0, TimeSpan.Zero);
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18.6-alpine3.23")
        .WithDatabase("sgol_recurring_tests")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();
    private readonly EventLogger<RecurringGenerationJob> recurringLogger = new();
    private ServiceProvider? provider;

    public async Task InitializeAsync()
    {
        await postgres.StartAsync();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Sgol"] = postgres.GetConnectionString()
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ILogger<RecurringGenerationJob>>(recurringLogger);
        services.AddSingleton<IClock>(new FixedClock(Now));
        services.AddSgolJobInfrastructure(configuration);
        services.AddSgolRecurringGeneration();
        services.AddSingleton<FaultGate>();
        services.AddScoped<EfRecurringOccurrenceProcessor>();
        services.AddScoped<IRecurringOccurrenceProcessor>(serviceProvider => new FaultingProcessor(
            serviceProvider.GetRequiredService<EfRecurringOccurrenceProcessor>(),
            serviceProvider.GetRequiredService<FaultGate>(),
            serviceProvider.GetRequiredService<SgolDbContext>()));
        provider = services.BuildServiceProvider();
        await using var scope = provider!.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        await context.Database.MigrateAsync();
        await SeedConfigurationAsync(context);
    }

    public async Task DisposeAsync()
    {
        if (provider is not null)
        {
            await provider.DisposeAsync();
        }

        await postgres.DisposeAsync();
    }

    [Fact]
    public async Task WorkingOccurrenceCreatesChainThenRecoversSameIdentities()
    {
        var window = Window(new DateOnly(2026, 9, 4), new TimeOnly(12, 0));
        var first = await ProcessAsync(window);
        var replay = await ProcessAsync(window);

        Assert.Equal(RecurringGenerationContract.Generated, first!.Result);
        Assert.Equal(RecurringGenerationContract.Recovered, replay!.Result);
        Assert.Equal(first.RuleVersionId, replay.RuleVersionId);
        Assert.Equal(first.PeriodId, replay.PeriodId);
        Assert.Equal(first.ObligationId, replay.ObligationId);

        await using var scope = provider!.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        Assert.Single(await context.GenerationRequests.AsNoTracking().ToListAsync());
        Assert.Single(await context.WorkObligations.AsNoTracking().ToListAsync());
        Assert.Single(await context.EligibilityEvaluations.AsNoTracking().ToListAsync());
        Assert.Empty(await context.AssignmentVersions.AsNoTracking().ToListAsync());
        Assert.Single(await context.WorkPlans.AsNoTracking().ToListAsync());
        var request = await context.GenerationRequests.AsNoTracking().SingleAsync();
        Assert.Null(request.RequestedBy);
        Assert.Equal(RecurringGenerationContract.OriginType, request.OriginType);
        Assert.Equal("LOR-001|2026-09-04|12:00", request.OriginReference);
        Assert.Contains(await context.AuditEvents.AsNoTracking().ToListAsync(),
            item => item.ActorType == "SYSTEM" && item.Action == "RECURRENCE_RECOVERED");
        Assert.Empty(await context.PlanVersions.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task NonWorkingOccurrenceIsRepeatablyOmittedWithoutFunctionalEffects()
    {
        var window = Window(new DateOnly(2026, 9, 5), new TimeOnly(12, 0));

        var first = await ProcessAsync(window);
        var replay = await ProcessAsync(window);

        Assert.Equal(RecurringGenerationContract.Omitted, first!.Result);
        Assert.Equal(RecurringGenerationContract.Omitted, replay!.Result);
        await using var scope = provider!.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        Assert.Empty(await context.GenerationRequests.AsNoTracking().ToListAsync());
        Assert.Empty(await context.WorkObligations.AsNoTracking().ToListAsync());
        Assert.Empty(await context.AssignmentVersions.AsNoTracking().ToListAsync());
        Assert.Empty(await context.WorkPlans.AsNoTracking().ToListAsync());
        Assert.Equal(2, await context.AuditEvents.CountAsync(item => item.Action == "RECURRENCE_OMITTED"));
    }

    [Fact]
    public async Task TwoProcessorsConvergeOnOnePostgreSqlOccurrence()
    {
        var window = Window(new DateOnly(2026, 9, 4), new TimeOnly(17, 0));
        await using var firstScope = provider!.CreateAsyncScope();
        await using var secondScope = provider!.CreateAsyncScope();
        var first = firstScope.ServiceProvider.GetRequiredService<IRecurringOccurrenceProcessor>();
        var second = secondScope.ServiceProvider.GetRequiredService<IRecurringOccurrenceProcessor>();

        var results = await Task.WhenAll(
            first.ProcessAsync(window, Guid.CreateVersion7()),
            second.ProcessAsync(window, Guid.CreateVersion7()));

        Assert.All(results, result => Assert.NotNull(result));
        Assert.Equal(results[0]!.ObligationId, results[1]!.ObligationId);
        await using var verificationScope = provider!.CreateAsyncScope();
        var context = verificationScope.ServiceProvider.GetRequiredService<SgolDbContext>();
        Assert.Single(await context.GenerationRequests.AsNoTracking().ToListAsync());
        Assert.Single(await context.WorkObligations.AsNoTracking().ToListAsync());
        Assert.Single(await context.EligibilityEvaluations.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task PostgreSqlAllowsOnlyApprovedSystemOriginAndKeepsManualActorRequired()
    {
        var generated = await ProcessAsync(Window(new DateOnly(2026, 9, 4), new TimeOnly(12, 0)));
        await using var scope = provider!.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            context.GenerationRequests.Add(new GenerationRequest(
                generated!.PeriodId!.Value,
                Guid.CreateVersion7(),
                new string('a', 64),
                RuleId,
                BranchScope.LorettaId,
                Guid.CreateVersion7(),
                ActivationOriginSchemas.ManualReference,
                "manual-reference",
                requestedBy: null,
                Now));
            await context.SaveChangesAsync();
        });
        context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task MissingCalendarRejectsWithoutCreatingAFunctionalFactAndClearsTracker()
    {
        await using var scope = provider!.CreateAsyncScope();
        var processor = scope.ServiceProvider.GetRequiredService<IRecurringOccurrenceProcessor>();
        var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();

        var result = await processor.ProcessAsync(
            Window(new DateOnly(2026, 9, 6), new TimeOnly(12, 0)),
            Guid.CreateVersion7());

        Assert.Equal(RecurringGenerationContract.Rejected, result!.Result);
        Assert.Equal("RECURRENCE_CALENDAR_NOT_CONFIGURED", result.ErrorCode);
        Assert.Empty(await context.GenerationRequests.AsNoTracking().ToListAsync());
        Assert.Empty(await context.WorkObligations.AsNoTracking().ToListAsync());
        Assert.Empty(await context.WorkPlans.AsNoTracking().ToListAsync());
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task InvalidRecurringDefinitionIsRejectedWithoutFunctionalFacts()
    {
        await using var scope = provider!.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        var definition = await context.TaskDefinitionVersions.SingleAsync(item => item.Id == TaskVersionId);
        definition.ApplyPublished(Current(TaskVersionId) with { RowVersion = 3 }, activeForNew: false);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await scope.ServiceProvider.GetRequiredService<IRecurringOccurrenceProcessor>()
            .ProcessAsync(Window(new DateOnly(2026, 9, 4), new TimeOnly(12, 0)), Guid.CreateVersion7());

        Assert.Equal(RecurringGenerationContract.Rejected, result!.Result);
        Assert.Equal("RECURRENCE_RULE_INVALID", result.ErrorCode);
        Assert.Empty(await context.GenerationRequests.AsNoTracking().ToListAsync());
        Assert.Empty(await context.WorkObligations.AsNoTracking().ToListAsync());
        Assert.Contains(await context.AuditEvents.AsNoTracking().ToListAsync(),
            item => item.Action == "RECURRENCE_REJECTED" && item.ActorType == "SYSTEM");
    }

    [Fact]
    public async Task IncompatibleExistingIsoPeriodFailsBeforeCreatingTheOccurrence()
    {
        await using var scope = provider!.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        await context.Database.ExecuteSqlInterpolatedAsync($$"""
            INSERT INTO week_period
                (id, branch_id, iso_year, iso_week, starts_on, ends_on, derived_status)
            VALUES
                ({{Guid.CreateVersion7()}}, {{BranchScope.LorettaId}}, 2026, 36,
                 DATE '2026-09-14', DATE '2026-09-20', 'VIGENTE')
            """);

        var error = await Assert.ThrowsAsync<JobExecutionException>(() =>
            scope.ServiceProvider.GetRequiredService<IRecurringOccurrenceProcessor>()
                .ProcessAsync(Window(new DateOnly(2026, 9, 4), new TimeOnly(12, 0)), Guid.CreateVersion7()));

        Assert.Equal("RECURRENCE_PERIOD_CONFLICT", error.ErrorCode);
        Assert.Empty(await context.GenerationRequests.AsNoTracking().ToListAsync());
        Assert.Empty(await context.WorkObligations.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task SupersededRuleIsNotSelectedAfterItsEffectiveInterval()
    {
        await using var scope = provider!.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        var rule = await context.ActivationRuleVersions.SingleAsync(item => item.Id == RuleId);
        rule.ApplySuperseded(new VersionRecord(
            RuleId,
            VersionStatuses.Superseded,
            EffectiveFrom,
            new DateTimeOffset(2026, 9, 4, 20, 0, 0, TimeSpan.Zero),
            "synthetic substitution",
            SupersedesId: null,
            RowVersion: 3));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await scope.ServiceProvider.GetRequiredService<IRecurringOccurrenceProcessor>()
            .ProcessAsync(
                Window(new DateOnly(2026, 9, 4), new TimeOnly(17, 0)),
                Guid.CreateVersion7());

        Assert.Null(result);
        Assert.Empty(await context.GenerationRequests.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task ScheduledJobCreatesTwoWindowsAndPersistsVersionedCheckpoint()
    {
        await using var scope = provider!.CreateAsyncScope();
        var runner = scope.ServiceProvider.GetRequiredService<ScheduledJobRunner>();

        var first = await runner.RunAsync(
            RecurringGenerationContract.JobName,
            Now,
            Guid.CreateVersion7());
        var replay = await runner.RunAsync(
            RecurringGenerationContract.JobName,
            Now,
            Guid.CreateVersion7());

        Assert.Equal(ScheduledJobResult.Completed, first);
        Assert.Equal(ScheduledJobResult.AlreadyCompleted, replay);
        await using var verificationScope = provider!.CreateAsyncScope();
        var context = verificationScope.ServiceProvider.GetRequiredService<SgolDbContext>();
        var run = await context.ScheduledJobRuns.AsNoTracking().SingleAsync();
        Assert.Equal(ScheduledJobStatuses.Succeeded, run.Status);
        using var checkpoint = JsonDocument.Parse(run.Checkpoint!);
        Assert.Equal(RecurringGenerationContract.CheckpointKind,
            checkpoint.RootElement.GetProperty("kind").GetString());
        Assert.Equal(2, checkpoint.RootElement.GetProperty("generated").GetInt32());
        Assert.Equal(0, checkpoint.RootElement.GetProperty("recovered").GetInt32());
        Assert.Equal(2, await context.GenerationRequests.CountAsync());
        Assert.Equal(2, await context.WorkObligations.CountAsync());
        Assert.Single(await context.WorkPlans.ToListAsync());
        Assert.Empty(await context.PlanVersions.ToListAsync());
    }

    [Fact]
    public async Task RetryAfterPartialBatchCompletesMissingWindowWithoutDuplicates()
    {
        provider!.GetRequiredService<FaultGate>().FailSecondOccurrenceOnce = true;
        await using var firstScope = provider!.CreateAsyncScope();
        var failed = await firstScope.ServiceProvider.GetRequiredService<ScheduledJobRunner>().RunAsync(
            RecurringGenerationContract.JobName,
            Now,
            Guid.CreateVersion7());

        await using var retryScope = provider!.CreateAsyncScope();
        var recovered = await retryScope.ServiceProvider.GetRequiredService<ScheduledJobRunner>().RunAsync(
            RecurringGenerationContract.JobName,
            Now,
            Guid.CreateVersion7());

        Assert.Equal(ScheduledJobResult.Failed, failed);
        Assert.Equal(ScheduledJobResult.Completed, recovered);
        await using var verificationScope = provider!.CreateAsyncScope();
        var context = verificationScope.ServiceProvider.GetRequiredService<SgolDbContext>();
        Assert.Equal(2, await context.GenerationRequests.CountAsync());
        Assert.Equal(2, await context.WorkObligations.CountAsync());
        Assert.Equal(2, await context.EligibilityEvaluations.CountAsync());
        Assert.Single(await context.WorkPlans.ToListAsync());
        var run = await context.ScheduledJobRuns.AsNoTracking().SingleAsync();
        Assert.Equal(ScheduledJobStatuses.Succeeded, run.Status);
    }

    [Theory]
    [InlineData("40001")]
    [InlineData("40P01")]
    public async Task PostgreSqlConcurrencyFailureRetriesWithStableOccurrenceAndAttempt(string sqlState)
    {
        var gate = provider!.GetRequiredService<FaultGate>();
        gate.RetryableFailuresRemaining = 1;
        gate.RetryableSqlState = sqlState;
        await using var scope = provider!.CreateAsyncScope();

        var result = await scope.ServiceProvider.GetRequiredService<ScheduledJobRunner>().RunAsync(
            RecurringGenerationContract.JobName,
            Now,
            Guid.CreateVersion7());

        Assert.Equal(ScheduledJobResult.Completed, result);
        await using var verificationScope = provider!.CreateAsyncScope();
        var context = verificationScope.ServiceProvider.GetRequiredService<SgolDbContext>();
        Assert.Equal(2, await context.GenerationRequests.CountAsync());
        Assert.Equal(2, await context.WorkObligations.CountAsync());
        Assert.Contains(recurringLogger.Messages, message =>
            message.Contains("attempt 2", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExhaustedPostgreSqlConcurrencyPersistsApprovedRecurringFailureCode()
    {
        var gate = provider!.GetRequiredService<FaultGate>();
        gate.RetryableFailuresRemaining = 3;
        gate.RetryableSqlState = "40001";
        await using var scope = provider!.CreateAsyncScope();

        var result = await scope.ServiceProvider.GetRequiredService<ScheduledJobRunner>().RunAsync(
            RecurringGenerationContract.JobName,
            Now,
            Guid.CreateVersion7());

        Assert.Equal(ScheduledJobResult.Failed, result);
        await using var verificationScope = provider!.CreateAsyncScope();
        var context = verificationScope.ServiceProvider.GetRequiredService<SgolDbContext>();
        var run = await context.ScheduledJobRuns.AsNoTracking().SingleAsync();
        Assert.Equal(ScheduledJobStatuses.Failed, run.Status);
        Assert.Equal("RECURRENCE_POSTGRES_CONCURRENCY_EXHAUSTED", run.Error);
        Assert.Empty(await context.GenerationRequests.AsNoTracking().ToListAsync());
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task PublishedPlanIsRecoveredWithoutRepublishingOrCreatingAPlanVersion()
    {
        var first = await ProcessAsync(Window(new DateOnly(2026, 9, 4), new TimeOnly(12, 0)));
        await using (var scope = provider!.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
            var plan = await context.WorkPlans.SingleAsync();
            plan.ApplyPublication();
            await context.SaveChangesAsync();
        }

        var replay = await ProcessAsync(Window(new DateOnly(2026, 9, 4), new TimeOnly(12, 0)));

        Assert.Equal(first!.ObligationId, replay!.ObligationId);
        Assert.Equal(RecurringGenerationContract.Recovered, replay.Result);
        await using var verificationScope = provider!.CreateAsyncScope();
        var verification = verificationScope.ServiceProvider.GetRequiredService<SgolDbContext>();
        Assert.Equal(WorkPlanStatuses.Published, (await verification.WorkPlans.SingleAsync()).Status);
        Assert.Empty(await verification.PlanVersions.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task FutureCutoffFailsWithoutFunctionalEffects()
    {
        await using var scope = provider!.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<ScheduledJobRunner>().RunAsync(
            RecurringGenerationContract.JobName,
            Now.AddTicks(1),
            Guid.CreateVersion7());

        Assert.Equal(ScheduledJobResult.Failed, result);
        await using var verificationScope = provider!.CreateAsyncScope();
        var context = verificationScope.ServiceProvider.GetRequiredService<SgolDbContext>();
        var run = await context.ScheduledJobRuns.AsNoTracking().SingleAsync();
        Assert.Equal("RECURRENCE_SCHEDULED_FOR_FUTURE", run.Error);
        Assert.Empty(await context.GenerationRequests.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task GapBeyondRecoveryHorizonFailsInsteadOfSilentlyTruncating()
    {
        await using (var seedScope = provider!.CreateAsyncScope())
        {
            var seedContext = seedScope.ServiceProvider.GetRequiredService<SgolDbContext>();
            seedContext.ScheduledJobRuns.Add(new ScheduledJobRun
            {
                Id = Guid.CreateVersion7(),
                JobName = RecurringGenerationContract.JobName,
                ScheduledFor = Now.AddDays(-8),
                StartedAt = Now.AddDays(-8),
                EndedAt = Now.AddDays(-8),
                Status = ScheduledJobStatuses.Succeeded,
            });
            await seedContext.SaveChangesAsync();
        }

        await using var scope = provider!.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<ScheduledJobRunner>().RunAsync(
            RecurringGenerationContract.JobName,
            Now,
            Guid.CreateVersion7());

        Assert.Equal(ScheduledJobResult.Failed, result);
        await using var verificationScope = provider!.CreateAsyncScope();
        var context = verificationScope.ServiceProvider.GetRequiredService<SgolDbContext>();
        Assert.Contains(await context.ScheduledJobRuns.AsNoTracking().ToListAsync(),
            run => run.Error == "RECURRENCE_RECOVERY_HORIZON_EXCEEDED");
        Assert.Empty(await context.GenerationRequests.AsNoTracking().ToListAsync());
    }

    private async Task<RecurringOccurrenceResult?> ProcessAsync(RecurringWindow window)
    {
        await using var scope = provider!.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IRecurringOccurrenceProcessor>()
            .ProcessAsync(window, Guid.CreateVersion7());
    }

    private static RecurringWindow Window(DateOnly date, TimeOnly time)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(RecurringGenerationContract.TimeZone);
        return new RecurringWindow(date, time, RecurringGenerationContract.ToUtc(date, time, zone));
    }

    private static readonly Guid ReleaseId = Guid.Parse("019d3a13-0000-7000-8000-000000000001");
    private static readonly Guid TaskVersionId = Guid.Parse("019d3a13-0005-7000-8000-000000000001");
    private static readonly Guid RuleId = Guid.Parse("019d3a13-0005-7000-8000-000000000002");
    private static readonly Guid PolicyId = Guid.Parse("019d3a13-0005-7000-8000-000000000003");

    private static async Task SeedConfigurationAsync(SgolDbContext context)
    {
        var release = new ConfigurationRelease(ReleaseId, BranchScope.LorettaId);
        var taskId = TaskDefinitionCatalog.Require(RecurringGenerationContract.TaskCode).Id;
        var taskVersion = new TaskDefinitionVersion(
            TaskVersionId,
            taskId,
            1,
            1,
            JsonDocument.Parse("{}"),
            ReleaseId);
        taskVersion.ApplyPublished(Current(TaskVersionId), activeForNew: true);
        using var schedule = JsonDocument.Parse(
            """{"kind":"WORKING_DAY_WINDOWS","workingDaysOnly":true,"localTimes":["12:00","17:00"],"timeZone":"America/Mexico_City"}""");
        var rule = new ActivationRuleVersion(
            RuleId,
            taskId,
            TaskVersionId,
            ReleaseId,
            basedOnId: null,
            versionNo: 1,
            ActivationModes.Recurring,
            schedule.RootElement,
            ActivationOriginSchemas.WorkingDayWindow);
        rule.ApplyPublished(Current(RuleId));
        var policy = new EligibilityPolicyVersion(
            PolicyId,
            taskId,
            TaskVersionId,
            ReleaseId,
            basedOnId: null,
            versionNo: 1,
            CanonicalRole.Subcoordination,
            requiresAvailability: true,
            requiredShift: null);
        policy.ApplyPublished(Current(PolicyId));
        var working = new CalendarDayVersion(
            Guid.Parse("019d3a13-0904-7000-8000-000000000001"),
            BranchScope.LorettaId,
            new DateOnly(2026, 9, 4),
            CalendarContract.WorkingDay,
            isWorkingDay: true,
            ReleaseId,
            "synthetic test calendar");
        working.ApplyPublished(Current(working.Id));
        var holiday = new CalendarDayVersion(
            Guid.Parse("019d3a13-0905-7000-8000-000000000001"),
            BranchScope.LorettaId,
            new DateOnly(2026, 9, 5),
            CalendarContract.Holiday,
            isWorkingDay: false,
            ReleaseId,
            "synthetic test calendar");
        holiday.ApplyPublished(Current(holiday.Id));

        context.AddRange(release, taskVersion, rule, policy, working, holiday);
        await context.SaveChangesAsync();
    }

    private static VersionRecord Current(Guid id) => new(
        id,
        VersionStatuses.Current,
        EffectiveFrom,
        EffectiveTo: null,
        "synthetic recurring test",
        SupersedesId: null,
        RowVersion: 2);

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }

    private sealed class EventLogger<T> : ILogger<T>
    {
        public ConcurrentBag<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }

    private sealed class FaultGate
    {
        public bool FailSecondOccurrenceOnce { get; set; }

        public int RetryableFailuresRemaining { get; set; }

        public string RetryableSqlState { get; set; } = "40001";

        public int Calls;
    }

    private sealed class FaultingProcessor(
        EfRecurringOccurrenceProcessor inner,
        FaultGate gate,
        SgolDbContext dbContext) : IRecurringOccurrenceProcessor
    {
        public async Task<RecurringOccurrenceResult?> ProcessAsync(
            RecurringWindow window,
            Guid correlationId,
            CancellationToken cancellationToken = default)
        {
            var call = Interlocked.Increment(ref gate.Calls);
            if (gate.FailSecondOccurrenceOnce && call == 2)
            {
                gate.FailSecondOccurrenceOnce = false;
                throw new JobExecutionException("SYNTHETIC_PARTIAL_BATCH_FAILURE");
            }

            if (gate.RetryableFailuresRemaining > 0)
            {
                gate.RetryableFailuresRemaining--;
                if (gate.RetryableSqlState == "40P01")
                {
                    await dbContext.Database.ExecuteSqlRawAsync(
                        "DO $$ BEGIN RAISE EXCEPTION 'synthetic retry' USING ERRCODE = '40P01'; END $$;",
                        cancellationToken);
                }
                else
                {
                    await dbContext.Database.ExecuteSqlRawAsync(
                        "DO $$ BEGIN RAISE EXCEPTION 'synthetic retry' USING ERRCODE = '40001'; END $$;",
                        cancellationToken);
                }
            }

            return await inner.ProcessAsync(window, correlationId, cancellationToken);
        }
    }
}
