using System.Buffers.Binary;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Options;
using Sgol.Evidence.Contracts;

namespace Sgol.Web.Infrastructure.Evidence;

public sealed class ClamAvScanner(IOptions<EvidenceScannerOptions> options) : IFileMalwareScanner
{
    private const int MaximumResponseBytes = 4 * 1024;
    private readonly EvidenceScannerOptions configuration = options.Value;

    public async Task<EvidenceScanOutcome> ScanAsync(
        Stream validatedContent,
        long sizeBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(validatedContent);
        if (!validatedContent.CanRead || !validatedContent.CanSeek ||
            sizeBytes is < EvidenceFileLimits.MinimumBytes or > EvidenceFileLimits.MaximumBytes)
        {
            return new EvidenceScanOutcome(EvidenceScanResult.Invalido, ErrorCode: "INTEGRITY");
        }

        validatedContent.Position = 0;
        var stopwatch = Stopwatch.StartNew();
        using var scanTimeout = CreateTimeout(TimeSpan.FromSeconds(configuration.ScanTimeoutSeconds), cancellationToken);
        try
        {
            using var client = new TcpClient();
            using var connectTimeout = CreateTimeout(
                TimeSpan.FromSeconds(configuration.ConnectTimeoutSeconds), scanTimeout.Token);
            await client.ConnectAsync(configuration.Host, configuration.Port, connectTimeout.Token);
            await using var network = client.GetStream();
            await network.WriteAsync("zINSTREAM\0"u8.ToArray(), scanTimeout.Token);

            var buffer = new byte[EvidenceFileLimits.BufferBytes];
            long sent = 0;
            while (true)
            {
                var read = await validatedContent.ReadAsync(buffer, scanTimeout.Token);
                if (read == 0)
                {
                    break;
                }

                sent += read;
                if (sent > sizeBytes || sent > EvidenceFileLimits.MaximumBytes)
                {
                    return new EvidenceScanOutcome(EvidenceScanResult.Invalido, ErrorCode: "INTEGRITY");
                }

                var length = new byte[4];
                BinaryPrimitives.WriteInt32BigEndian(length, read);
                await network.WriteAsync(length, scanTimeout.Token);
                await network.WriteAsync(buffer.AsMemory(0, read), scanTimeout.Token);
            }

            if (sent != sizeBytes)
            {
                return new EvidenceScanOutcome(EvidenceScanResult.Invalido, ErrorCode: "INTEGRITY");
            }

            await network.WriteAsync(new byte[4], scanTimeout.Token);
            await network.FlushAsync(scanTimeout.Token);
            var response = await ReadNullTerminatedResponseAsync(network, scanTimeout.Token);
            var outcome = ParseResponse(response);
            EvidenceTelemetry.Scans.Add(1, tag: new("result", outcome.Result.ToString()));
            if (outcome.Result == EvidenceScanResult.ErrorEscaneo)
            {
                EvidenceTelemetry.ScanFailures.Add(1, tag: new("result", "ERROR_ESCANEO"));
            }

            return outcome;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            EvidenceTelemetry.ScanFailures.Add(1, tag: new("result", "ERROR_ESCANEO"));
            return new EvidenceScanOutcome(EvidenceScanResult.ErrorEscaneo, ErrorCode: "TIMEOUT");
        }
        catch (Exception exception) when (exception is SocketException or IOException)
        {
            EvidenceTelemetry.ScanFailures.Add(1, tag: new("result", "ERROR_ESCANEO"));
            return new EvidenceScanOutcome(EvidenceScanResult.ErrorEscaneo, ErrorCode: "UNAVAILABLE");
        }
        finally
        {
            EvidenceTelemetry.ScanDuration.Record(stopwatch.Elapsed.TotalMilliseconds,
                tag: new("operation", "scan"));
        }
    }

    public async Task CheckAvailabilityAsync(CancellationToken cancellationToken)
    {
        using var timeout = CreateTimeout(
            TimeSpan.FromSeconds(configuration.ConnectTimeoutSeconds), cancellationToken);
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(configuration.Host, configuration.Port, timeout.Token);
            await using var stream = client.GetStream();
            await stream.WriteAsync("zPING\0"u8.ToArray(), timeout.Token);
            await stream.FlushAsync(timeout.Token);
            var response = await ReadNullTerminatedResponseAsync(stream, timeout.Token);
            if (!response.Equals("PONG", StringComparison.Ordinal))
            {
                throw new IOException("Unexpected scanner health response.");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is SocketException or IOException or OperationCanceledException)
        {
            throw new EvidenceInfrastructureExceptionImpl("The evidence scanner is unavailable.");
        }
    }

    internal static EvidenceScanOutcome ParseResponse(string response)
    {
        if (response.Equals("stream: OK", StringComparison.Ordinal))
        {
            return new EvidenceScanOutcome(EvidenceScanResult.Limpio, "ClamAV");
        }

        if (response.StartsWith("stream: ", StringComparison.Ordinal) &&
            response.EndsWith(" FOUND", StringComparison.Ordinal) &&
            response.Length > "stream:  FOUND".Length)
        {
            return new EvidenceScanOutcome(EvidenceScanResult.Infectado, "ClamAV");
        }

        return new EvidenceScanOutcome(EvidenceScanResult.ErrorEscaneo, "ClamAV", "PROTOCOL");
    }

    private static async Task<string> ReadNullTerminatedResponseAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[MaximumResponseBytes + 1];
        var count = 0;
        while (count < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(count, 1), cancellationToken);
            if (read == 0)
            {
                break;
            }

            if (buffer[count] == 0)
            {
                return Encoding.ASCII.GetString(buffer, 0, count);
            }

            count++;
        }

        return string.Empty;
    }

    private static CancellationTokenSource CreateTimeout(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        source.CancelAfter(timeout);
        return source;
    }

    private sealed class EvidenceInfrastructureExceptionImpl(string message)
        : EvidenceInfrastructureException(message);
}
