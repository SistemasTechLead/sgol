using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sgol.Configuration.Contracts;
using Sgol.Generation.Contracts;
using Sgol.JobInfrastructure;
using Xunit;

namespace Sgol.UnitTests;

public sealed class RecurringGenerationTests
{
    private static readonly TimeZoneInfo TimeZone =
        TimeZoneInfo.FindSystemTimeZoneById(RecurringGenerationContract.TimeZone);

    [Fact]
    public void ProductionCompositionRegistersOnlyTheApprovedFunctionalJob()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Sgol"] = "Host=localhost;Database=not_opened;Username=not_opened"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSgolJobInfrastructure(configuration);
        services.AddSingleton(TimeZoneInfo.Utc);
        services.AddSgolRecurringGeneration();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var jobs = scope.ServiceProvider.GetServices<IScheduledJob>().ToArray();

        var job = Assert.Single(jobs);
        Assert.Equal(RecurringGenerationContract.JobName, job.Name);
        Assert.Equal("RECURRENCE_POSTGRES_CONCURRENCY_EXHAUSTED", job.ConcurrencyExhaustedErrorCode);
        Assert.Same(TimeZoneInfo.Utc, scope.ServiceProvider.GetRequiredService<TimeZoneInfo>());
        Assert.False(scope.ServiceProvider.GetRequiredService<ScheduledJobRegistry>()
            .TryGet("UNREGISTERED_JOB", out _));
    }

    [Fact]
    public void DueWindowsUseMexicoCityAndInclusiveUpperBoundary()
    {
        var from = new DateTimeOffset(2026, 9, 4, 16, 59, 59, TimeSpan.Zero);
        var through = new DateTimeOffset(2026, 9, 4, 23, 0, 0, TimeSpan.Zero);

        var windows = RecurringGenerationContract.DueWindows(from, through, TimeZone);

        Assert.Collection(
            windows,
            noon =>
            {
                Assert.Equal(new DateOnly(2026, 9, 4), noon.LocalDate);
                Assert.Equal(new TimeOnly(12, 0), noon.Window);
                Assert.Equal(new DateTimeOffset(2026, 9, 4, 18, 0, 0, TimeSpan.Zero), noon.OccurrenceInstant);
            },
            evening =>
            {
                Assert.Equal(new TimeOnly(17, 0), evening.Window);
                Assert.Equal(through, evening.OccurrenceInstant);
            });
    }

    [Fact]
    public void BeforeWindowProducesNoneAndMillisecondsAfterProduceOne()
    {
        var start = new DateTimeOffset(2026, 9, 4, 6, 0, 0, TimeSpan.Zero);
        var before = new DateTimeOffset(2026, 9, 4, 17, 59, 59, 999, TimeSpan.Zero);
        var after = new DateTimeOffset(2026, 9, 4, 18, 0, 0, 1, TimeSpan.Zero);

        Assert.Empty(RecurringGenerationContract.DueWindows(start, before, TimeZone));
        Assert.Single(RecurringGenerationContract.DueWindows(start, after, TimeZone));
    }

    [Fact]
    public void DayAndIsoWeekBoundaryAreCalculatedFromLocalTimeNotServerTime()
    {
        var from = new DateTimeOffset(2027, 1, 3, 23, 30, 0, TimeSpan.Zero);
        var through = new DateTimeOffset(2027, 1, 4, 23, 0, 0, TimeSpan.Zero);

        var windows = RecurringGenerationContract.DueWindows(from, through, TimeZone);

        Assert.Equal(2, windows.Count);
        Assert.All(windows, window => Assert.Equal(new DateOnly(2027, 1, 4), window.LocalDate));
        Assert.Equal(1, System.Globalization.ISOWeek.GetWeekOfYear(
            windows[0].LocalDate.ToDateTime(TimeOnly.MinValue)));
    }

    [Fact]
    public void FunctionalIdentityIsStableAndChangesByWindowRuleAndPeriod()
    {
        var rule = Guid.Parse("019d3a10-0005-7000-8000-000000000505");
        var branch = Guid.Parse("019d3a10-0000-7000-8000-000000000001");
        var period = Guid.Parse("019d3a10-0010-7000-8000-000000000001");
        var first = RecurringGenerationContract.Identity(
            rule, branch, period, new DateOnly(2026, 9, 4), new TimeOnly(12, 0));
        var replay = RecurringGenerationContract.Identity(
            rule, branch, period, new DateOnly(2026, 9, 4), new TimeOnly(12, 0));

        Assert.Equal(first, replay);
        Assert.Equal(8, first.IdempotencyKey.Version);
        Assert.Equal("LOR-001|2026-09-04|12:00", first.OriginReference);
        Assert.Equal(64, first.RequestHash.Length);
        Assert.NotEqual(first.IdempotencyKey, RecurringGenerationContract.Identity(
            rule, branch, period, new DateOnly(2026, 9, 4), new TimeOnly(17, 0)).IdempotencyKey);
        Assert.NotEqual(first.IdempotencyKey, RecurringGenerationContract.Identity(
            Guid.CreateVersion7(), branch, period, new DateOnly(2026, 9, 4), new TimeOnly(12, 0)).IdempotencyKey);
        Assert.NotEqual(first.IdempotencyKey, RecurringGenerationContract.Identity(
            rule, branch, Guid.CreateVersion7(), new DateOnly(2026, 9, 4), new TimeOnly(12, 0)).IdempotencyKey);
    }

    [Fact]
    public void CatalogKeepsManualTasksOutAndDoesNotInventTar0026Facts()
    {
        Assert.Equal(
            ActivationOriginSchemas.WorkingDayWindow,
            ActivationPolicyCatalog.Require("TAR-0005").OriginKeySchema);
        Assert.Equal(
            ActivationOriginSchemas.ServiceDueDateReference,
            ActivationPolicyCatalog.Require("TAR-0026").OriginKeySchema);
        Assert.DoesNotContain(
            ActivationPolicyCatalog.All.Where(item => item.Value.Mode == ActivationModes.Manual),
            item => item.Key == RecurringGenerationContract.TaskCode);
        Assert.Equal("TAR-0005", RecurringGenerationContract.TaskCode);
    }
}
