using System.Buffers.Binary;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Sgol.Evidence.Contracts;
using Sgol.Evidence.Technical;
using Sgol.Web.Infrastructure.Evidence;
using SkiaSharp;

/// <summary>Diagnostic A/B against the isolated scanner only; never uses shared port 3310.</summary>
public static class PerformanceScannerChecks
{
    public sealed record Sample(int Image, string Variant, int Bytes, string Sha256, double ElapsedMs,
        double CpuMs, long AllocatedBytes, string Result, string? ErrorCode);

    public static async Task RunHostComparisonAsync(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        // Match PerformanceLoadChecks.Photograph exactly, including SKBitmap's native color format.
        using var bitmap = new SKBitmap(1600, 1200);
        var pixels = new SKColor[1600 * 1200];
        var random = new Random(42);
        for (var i = 0; i < pixels.Length; i++)
            pixels[i] = new((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256));
        bitmap.Pixels = pixels;
        using var encoded = bitmap.Encode(SKEncodedImageFormat.Jpeg, 85);
        var original = encoded.ToArray();
        var measurements = new List<object>();
        foreach (var round in new[] { 1, 2 })
        foreach (var host in round == 1 ? new[] { "localhost", "127.0.0.1" } : new[] { "127.0.0.1", "localhost" })
        {
            var scanner = new ClamAvScanner(Options.Create(new EvidenceScannerOptions
                { Host = host, Port = 3347, ConnectTimeoutSeconds = 3, ScanTimeoutSeconds = 30 }));
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(70));
            using var source = new MemoryStream(original, false);
            var watch = Stopwatch.StartNew();
            var first = await scanner.ScanAsync(source, original.Length, timeout.Token);
            var firstMs = watch.Elapsed.TotalMilliseconds;
            watch.Restart();
            var normalized = await PhotographNormalizer.NormalizeAsync(source, timeout.Token);
            var normalizationMs = watch.Elapsed.TotalMilliseconds;
            using var secondSource = new MemoryStream(normalized, false);
            watch.Restart();
            var second = await scanner.ScanAsync(secondSource, normalized.Length, timeout.Token);
            var secondMs = watch.Elapsed.TotalMilliseconds;
            measurements.Add(new { round, host, originalBytes = original.Length,
                originalSha256 = Convert.ToHexStringLower(SHA256.HashData(original)),
                normalizedBytes = normalized.Length, firstMs, normalizationMs, secondMs,
                firstResult = first.Result.ToString(), first.ErrorCode,
                secondResult = second.Result.ToString(), secondError = second.ErrorCode });
            Console.WriteLine($"host comparison round={round} host={host}: first={firstMs:F1}ms; normalize={normalizationMs:F1}ms; second={secondMs:F1}ms; result={first.Result}/{second.Result}");
        }
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "performance-scanner-hosts.json"),
            JsonSerializer.Serialize(new { generatedUtc = DateTimeOffset.UtcNow,
                scope = "Isolated scanner port 3347. Exact PerformanceLoadChecks 1600x1200 Random42 RGB JPEG85; real ClamAvScanner and PhotographNormalizer. Alternating host order, sequential stages, no HTTP or database. Host effect includes DNS/connect behavior; stage timings alone do not identify IPv6 as the cause.", measurements },
                new JsonSerializerOptions { WriteIndented = true }));
    }

    public static async Task<IReadOnlyList<Sample>> RunAsync(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        var scanner = new ClamAvScanner(Options.Create(new EvidenceScannerOptions
            { Host = "127.0.0.1", Port = 3347, ConnectTimeoutSeconds = 3, ScanTimeoutSeconds = 30 }));
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        await scanner.CheckAvailabilityAsync(timeout.Token);
        var samples = new List<Sample>();
        for (var image = 0; image < 3; image++)
        {
            var bytes = Photograph(731 + image);
            var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
            // Alternate order to reduce the impact of scanner warming/cache on one variant.
            var variants = image % 2 == 0 ? new[] { false, true } : new[] { true, false };
            foreach (var noDelay in variants)
            {
                using var content = new MemoryStream(bytes, false);
                using var process = Process.GetCurrentProcess();
                var cpu = process.TotalProcessorTime;
                var allocation = GC.GetTotalAllocatedBytes(true);
                var watch = Stopwatch.StartNew();
                EvidenceScanOutcome outcome;
                try
                {
                    outcome = noDelay ? await ProbeAsync(content, timeout.Token)
                        : await scanner.ScanAsync(content, bytes.Length, timeout.Token);
                }
                catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException)
                {
                    outcome = new(EvidenceScanResult.ErrorEscaneo, ErrorCode: ex.GetType().Name);
                }
                watch.Stop();
                var sample = new Sample(image + 1, noDelay ? "probe-no-delay" : "production-default",
                    bytes.Length, hash, watch.Elapsed.TotalMilliseconds,
                    (process.TotalProcessorTime - cpu).TotalMilliseconds,
                    GC.GetTotalAllocatedBytes(true) - allocation, outcome.Result.ToString(), outcome.ErrorCode);
                samples.Add(sample);
                Console.WriteLine($"scanner image={sample.Image} {sample.Variant}: {sample.Result}; bytes={sample.Bytes}; {sample.ElapsedMs:F1}ms");
            }
        }
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "performance-scanner.json"),
            JsonSerializer.Serialize(new
            {
                generatedUtc = DateTimeOffset.UtcNow,
                target = "isolated 127.0.0.1:3347; concurrency=1",
                scope = "Three deterministic JPEG1280x960 noise images, same bytes per paired variant; production ClamAvScanner compared with diagnostic INSTREAM probe using TcpClient.NoDelay=true. Probe retains 64KiB chunk sizes and separate 4-byte length writes. Alternating order does not fully eliminate scanner cache effects. This does not change product configuration or implementation.",
                samples
            }, new JsonSerializerOptions { WriteIndented = true }));
        return samples;
    }

    private static async Task<EvidenceScanOutcome> ProbeAsync(Stream content, CancellationToken token)
    {
        using var scanTimeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        scanTimeout.CancelAfter(TimeSpan.FromSeconds(30));
        using var connectionTimeout = CancellationTokenSource.CreateLinkedTokenSource(scanTimeout.Token);
        connectionTimeout.CancelAfter(TimeSpan.FromSeconds(3));
        using var client = new TcpClient { NoDelay = true };
        await client.ConnectAsync("127.0.0.1", 3347, connectionTimeout.Token);
        await using var network = client.GetStream();
        await network.WriteAsync("zINSTREAM\0"u8.ToArray(), scanTimeout.Token);
        var buffer = new byte[EvidenceFileLimits.BufferBytes];
        int read;
        while ((read = await content.ReadAsync(buffer, scanTimeout.Token)) != 0)
        {
            var length = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(length, read);
            await network.WriteAsync(length, scanTimeout.Token);
            await network.WriteAsync(buffer.AsMemory(0, read), scanTimeout.Token);
        }
        await network.WriteAsync(new byte[4], scanTimeout.Token);
        await network.FlushAsync(scanTimeout.Token);
        var response = new byte[4097];
        for (var count = 0; count < response.Length; count++)
        {
            if (await network.ReadAsync(response.AsMemory(count, 1), scanTimeout.Token) == 0) break;
            if (response[count] == 0)
                return ClamAvScanner.ParseResponse(Encoding.ASCII.GetString(response, 0, count));
        }
        return new(EvidenceScanResult.ErrorEscaneo, ErrorCode: "PROTOCOL");
    }

    private static byte[] Photograph(int seed)
    {
        const int width = 1280, height = 960;
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque));
        var pixels = new byte[bitmap.ByteCount];
        new Random(seed).NextBytes(pixels);
        for (var i = 3; i < pixels.Length; i += 4) pixels[i] = 255;
        Marshal.Copy(pixels, 0, bitmap.GetPixels(), pixels.Length);
        using var image = SKImage.FromBitmap(bitmap);
        using var jpeg = image.Encode(SKEncodedImageFormat.Jpeg, 85);
        return jpeg.ToArray();
    }
}
