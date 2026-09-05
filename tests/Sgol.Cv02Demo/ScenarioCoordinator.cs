namespace Sgol.Cv02Demo;

internal sealed class ScenarioCoordinator(Cv02ScenarioEngine engine, DemoState state) : IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<DemoScenarioResult> RunAsync(string scenarioId, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        var startedAt = DateTimeOffset.UtcNow;
        try
        {
            var result = await engine.RunAsync(scenarioId, cancellationToken);
            state.Set(result);
            return result;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var code = exception is DemoSafetyException safety ? safety.ErrorCode : exception.Message;
            var result = DemoScenarioResult.Failed(scenarioId, startedAt, DateTimeOffset.UtcNow, code);
            state.Set(result);
            return result;
        }
        finally
        {
            gate.Release();
        }
    }

    public void Dispose() => gate.Dispose();
}
