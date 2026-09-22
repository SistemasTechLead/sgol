using Xunit;

namespace Sgol.Cv05Demo;

[Trait("Category", "CV05_FOCUSED")]
public sealed class Cv05ContractFocusedTests
{
    [Fact]
    public async Task HostedIndicatorsAuditAndIdempotencySatisfyClosedContracts()
    {
        var infrastructure = new Cv05Infrastructure("sha256:" + new string('a', 64));
        string? failure = null;
        try
        {
            await infrastructure.StartAsync(CancellationToken.None);
            var engine = new Cv05ScenarioEngine(infrastructure);
            await engine.ExecutePreRecoveryAsync(CancellationToken.None);
            Assert.All(engine.CurrentEvidence.Where(item => item.Scenario is not "NONE"),
                item => Assert.Equal("PASSED", item.State));
        }
        catch (DemoFailureException exception)
        {
            failure = $"{exception.Scenario}:{exception.Data["Check"] ?? exception.Code}:" +
                $"{exception.Data["Observed"] ?? "NONE"}/{exception.Data["Denominator"] ?? "NONE"}";
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
