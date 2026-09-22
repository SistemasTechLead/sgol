using Xunit;

namespace Sgol.Cv05Demo;

[Trait("Category", "CV05_FOCUSED")]
public sealed class Cv05RecoveryFocusedTests
{
    [Fact]
    public async Task HostedRecoveryPreservesIdentityHistoryAndNoEffect()
    {
        var root = Cv05DemoApplication.FindRepositoryRoot();
        var commit = (await NativeProcess.RunAsync("git", ["rev-parse", "HEAD"], root,
            TimeSpan.FromSeconds(30), CancellationToken.None)).Stdout.Trim();
        var previousCommit = Environment.GetEnvironmentVariable("CV05_COMMIT");
        Environment.SetEnvironmentVariable("CV05_COMMIT", commit);
        var image = new Cv05Image(root, commit);
        Cv05Infrastructure? infrastructure = null;
        string? failure = null;
        try
        {
            await image.BuildAsync(CancellationToken.None);
            infrastructure = new Cv05Infrastructure(image.ImageId);
            await infrastructure.StartAsync(CancellationToken.None);
            var engine = new Cv05ScenarioEngine(infrastructure);
            await engine.ExecuteRecoveryFocusedAsync(CancellationToken.None);
            Assert.All(engine.CurrentEvidence.Where(item => item.Scenario is not "NONE"),
                item => Assert.Equal("PASSED", item.State));
        }
        catch (DemoFailureException exception)
        {
            failure = $"{exception.Scenario}:{exception.Data["Check"] ?? exception.Data["Stage"] ?? exception.Data["NativeFailure"] ?? exception.Code}:" +
                $"{exception.Data["Category"] ?? "NONE"}";
        }
        catch
        {
            failure = "CV05_UNEXPECTED_FAILURE";
        }
        finally
        {
            if (infrastructure is not null && !await infrastructure.CleanupAsync())
                failure = failure is null ? "CV05_CLEANUP_FAILED" : $"{failure};CV05_CLEANUP_FAILED";
            if (!await image.CleanupAsync())
                failure = failure is null ? "CV05_IMAGE_CLEANUP_FAILED" : $"{failure};CV05_IMAGE_CLEANUP_FAILED";
            Environment.SetEnvironmentVariable("CV05_COMMIT", previousCommit);
        }
        Assert.Null(failure);
    }
}
