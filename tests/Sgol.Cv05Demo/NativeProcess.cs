using System.Diagnostics;
using System.Text;

namespace Sgol.Cv05Demo;

internal sealed record NativeResult(int Exit, string Stdout, string Stderr);

internal static class NativeProcess
{
    public static async Task<NativeResult> RunAsync(string executable, IEnumerable<string> arguments,
        string workingDirectory, TimeSpan timeout, CancellationToken token)
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory,
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = start };
        if (!process.Start())
            throw new DemoFailureException("OCI", "NONE", "CV05_PRECONDITION_FAILED");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(timeout);
        var stdout = DrainAsync(process.StandardOutput, deadline.Token);
        var stderr = DrainAsync(process.StandardError, deadline.Token);
        try
        {
            await process.WaitForExitAsync(deadline.Token);
            var exit = DemoSafety.CaptureExit(process);
            var outputs = await Task.WhenAll(stdout, stderr);
            return new NativeResult(exit, outputs[0], outputs[1]);
        }
        catch (Exception exception)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
                _ = DemoSafety.CaptureExit(process);
            }
            var failure = new DemoFailureException("OCI", "NONE", "CV05_PRECONDITION_FAILED");
            failure.Data["NativeFailure"] = exception is OperationCanceledException &&
                !token.IsCancellationRequested ? "TIMEOUT" : exception.GetType().Name;
            throw failure;
        }
    }

    private static async Task<string> DrainAsync(StreamReader reader, CancellationToken token)
    {
        var text = new StringBuilder();
        var buffer = new char[4096];
        while (true)
        {
            var count = await reader.ReadAsync(buffer, token);
            if (count == 0) return text.ToString();
            if (text.Length < 262_144)
                text.Append(buffer, 0, Math.Min(count, 262_144 - text.Length));
        }
    }
}
