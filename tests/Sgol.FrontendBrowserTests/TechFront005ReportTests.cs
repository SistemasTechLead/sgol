using Xunit;

namespace Sgol.FrontendBrowserTests;

public sealed class TechFront005ReportTests
{
    private static Front005Run Run(int cycle = 1, string profile = "desktop") => new("TECH-FRONT-005", new('a', 40), cycle,
        profile, "Chromium 145.0", "Windows AMD64 / Docker Linux AMD64", true,
        TechFront005Report.Routes.Select(r => new Front005Step(r, "PASS", 1, TechFront005Report.Roles)).ToArray(), true, "NONE");

    [Fact]
    public void RequiresBothProfilesInBothCyclesOnTheSameHead()
    {
        var runs = new[] { Run(), Run(1, "mobile"), Run(2), Run(2, "mobile") };
        TechFront005Report.ValidateCycles(runs, new('a', 40));
        Assert.Throws<InvalidOperationException>(() => TechFront005Report.ValidateCycles(runs[..3], new('a', 40)));
        Assert.Throws<InvalidOperationException>(() => TechFront005Report.ValidateCycles(runs, new('b', 40)));
        runs[3] = Run(2);
        Assert.Throws<InvalidOperationException>(() => TechFront005Report.ValidateCycles(runs, new('a', 40)));
    }

    [Fact]
    public void CleanupFailureOrPartialJourneyCannotBecomeACompleteDemo()
    {
        var run = Run();
        TechFront005Report.Validate(run, true);
        Assert.Throws<InvalidOperationException>(() => TechFront005Report.Validate(run with { Cleanup = false }, true));
        Assert.Throws<InvalidOperationException>(() => TechFront005Report.Validate(run with { Failure = "HARNESS_FAILED" }, true));
        Assert.Throws<InvalidOperationException>(() => TechFront005Report.Validate(run with { Steps = run.Steps[..8] }, true));
        var failed = run.Steps.Select(s => s.Route == "R7" ? s with { State = "FAIL" } : s).ToArray();
        Assert.Throws<InvalidOperationException>(() => TechFront005Report.Validate(run with { Steps = failed }, true));
        TechFront005Report.Validate(run with { Failure = "HARNESS_FAILED", Steps = run.Steps[..3] }, false);
    }

    [Fact]
    public void ReportRejectsFreeTextAndUnknownIdentifiers()
    {
        var run = Run();
        foreach (var candidate in new[] { run with { Failure = "password=synthetic" }, run with { Browser = "https://example.invalid/token" },
            run with { Sha = "synthetic" }, run with { Cycle = 3 }, run with { Platform = "unverified" } })
            Assert.Throws<InvalidOperationException>(() => TechFront005Report.Validate(candidate, false));
        var unknown = run.Steps.Select(s => s with { Roles = ["UNAPPROVED_ROLE"] }).ToArray();
        Assert.Throws<InvalidOperationException>(() => TechFront005Report.Validate(run with { Steps = unknown }, false));
        Assert.Throws<InvalidOperationException>(() => TechFront005Report.Validate(run with { Steps = [run.Steps[0], run.Steps[0]] }, false));
    }
}
