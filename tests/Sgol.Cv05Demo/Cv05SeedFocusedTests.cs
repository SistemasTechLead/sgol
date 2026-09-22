using Xunit;

namespace Sgol.Cv05Demo;

[Trait("Category", "CV05_FOCUSED")]
public sealed class Cv05SeedFocusedTests
{
    [Fact]
    public async Task HostedAccountsAndSyntheticSeedCreateOnlyPreconditions()
    {
        var infrastructure = new Cv05Infrastructure("sha256:" + new string('a', 64));
        string? failure = null;
        try
        {
            await infrastructure.StartAsync(CancellationToken.None);
            var engine = new Cv05ScenarioEngine(infrastructure);
            await engine.PrepareSeedAsync(CancellationToken.None);
            Assert.Contains(engine.CurrentEvidence, item => item.Phase == "SEED" && item.State == "PASSED");
        }
        catch (DemoFailureException exception)
        {
            failure = $"{exception.Data["Stage"] ?? "NONE"}:{exception.Data["SqlState"] ?? "NONE"}:" +
                $"{exception.Data["Constraint"] ?? "NONE"}";
        }
        catch
        {
            failure = "CV05_UNEXPECTED_FAILURE";
        }
        finally
        {
            if (!await infrastructure.CleanupAsync())
                failure = failure is null ? "CV05_CLEANUP_FAILED" : $"{failure};CV05_CLEANUP_FAILED";
        }
        Assert.Null(failure);
    }
}
