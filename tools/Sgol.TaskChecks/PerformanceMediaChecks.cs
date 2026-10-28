using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Sgol.Evidence.Technical;
using Sgol.Web.Infrastructure.Persistence.Planning;
using SkiaSharp;

/// <summary>Bounded, synthetic media workloads; no HTTP, database or operational data.</summary>
public static class PerformanceMediaChecks
{
    public sealed record Sample(string Scenario, int Operations, int Concurrency, int InputBytes,
        double ElapsedMs, double CpuMs, long AllocatedBytes, long ManagedBytesBefore,
        long ManagedBytesAfter, long WorkingSetBefore, long WorkingSetAfter,
        int Gen0Collections, int Gen1Collections, int Gen2Collections, string Outcome);

    public static async Task<IReadOnlyList<Sample>> RunAsync(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        var ordinary = Workbook(1);
        var amplified = Workbook(500);
        var photograph = Photograph();
        // Warm the actual production implementations before measuring.
        await ReadExcel(ordinary, CancellationToken.None);
        await Normalize(photograph, CancellationToken.None);
        var samples = new List<Sample>();
        foreach (var concurrency in new[] { 1, 4 })
        {
            samples.Add(await Measure($"excel-normal-c{concurrency}", 8, concurrency, ordinary.Length,
                ct => ReadExcel(ordinary, ct)));
            samples.Add(await Measure($"photograph-1600x1200-c{concurrency}", 8, concurrency, photograph.Length,
                ct => Normalize(photograph, ct)));
        }
        samples.Add(await Measure("excel-repeated-worksheet-500", 1, 1, amplified.Length,
            ct => ReadExcel(amplified, ct)));
        samples.Add(await Measure("excel-repeated-worksheet-500-c4", 4, 4, amplified.Length,
            ct => ReadExcel(amplified, ct)));
        foreach (var (name, size, action) in new (string, int, Func<CancellationToken, Task>)[]
        {
            ("excel-pre-cancelled", ordinary.Length, ct => ReadExcel(ordinary, ct)),
            ("photograph-pre-cancelled", photograph.Length, ct => Normalize(photograph, ct))
        })
        {
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            samples.Add(await Measure(name, 1, 1, size, action, cancelled.Token));
        }
        // A dedicated thread prevents a busy worker pool from delaying the cancellation itself.
        using (var cancellation = new CancellationTokenSource())
        using (var started = new ManualResetEventSlim())
        {
            var canceller = new Thread(() => { started.Wait(); Thread.Sleep(20); cancellation.Cancel(); });
            canceller.Start();
            samples.Add(await Measure("excel-cancel-during-repeated-parse", 1, 1, amplified.Length,
                async ct => { started.Set(); await ReadExcel(amplified, ct); }, cancellation.Token));
            canceller.Join();
        }
        var report = new
        {
            generatedUtc = DateTimeOffset.UtcNow,
            environment = new { runtime = Environment.Version.ToString(), Environment.ProcessorCount },
            scope = "Production media methods with synthetic in-memory files; no HTTP, DB, antivirus or browser. AllocatedBytes is cumulative managed allocation, not peak memory, and excludes Skia native allocations. Process CPU and working set include other activity in this test process. Samples are diagnostic observations, not hardware-independent performance assertions.",
            samples
        };
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "performance-media.json"),
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        foreach (var sample in samples)
            Console.WriteLine($"{sample.Scenario}: {sample.Outcome}; {sample.ElapsedMs:F1}ms; allocated={sample.AllocatedBytes / 1048576d:F2}MiB");
        return samples;
    }

    private static async Task<Sample> Measure(string scenario, int operations, int concurrency,
        int inputBytes, Func<CancellationToken, Task> action, CancellationToken token = default)
    {
        using var process = Process.GetCurrentProcess();
        process.Refresh();
        var workingBefore = process.WorkingSet64;
        var managedBefore = GC.GetTotalMemory(false);
        var allocated = GC.GetTotalAllocatedBytes(true);
        var cpu = process.TotalProcessorTime;
        var counts = Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray();
        var watch = Stopwatch.StartNew();
        var outcomes = new System.Collections.Concurrent.ConcurrentBag<string>();
        var next = -1;
        await Task.WhenAll(Enumerable.Range(0, concurrency).Select(_ => Task.Run(async () =>
        {
            while (Interlocked.Increment(ref next) < operations)
            {
                try { await action(token); outcomes.Add(token.IsCancellationRequested ? "completed-after-cancellation" : "completed"); }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { outcomes.Add("cancelled"); }
                catch (Exception ex) { outcomes.Add("error:" + ex.GetType().Name); }
            }
        })));
        watch.Stop();
        process.Refresh();
        return new(scenario, operations, concurrency, inputBytes, watch.Elapsed.TotalMilliseconds,
            (process.TotalProcessorTime - cpu).TotalMilliseconds, GC.GetTotalAllocatedBytes(true) - allocated,
            managedBefore, GC.GetTotalMemory(false), workingBefore, process.WorkingSet64,
            GC.CollectionCount(0) - counts[0], GC.CollectionCount(1) - counts[1], GC.CollectionCount(2) - counts[2],
            string.Join(",", outcomes.GroupBy(x => x).OrderBy(x => x.Key).Select(x => $"{x.Key}:{x.Count()}")));
    }

    internal static async Task ReadExcel(byte[] bytes, CancellationToken token)
    {
        using var stream = new MemoryStream(bytes, false);
        var file = new FormFile(stream, 0, bytes.Length, "file", "synthetic.xlsx")
            { Headers = new HeaderDictionary(), ContentType = OperationalExcel.MediaType };
        await OperationalExcel.ReadAsync(file, token);
    }

    private static async Task Normalize(byte[] bytes, CancellationToken token)
    {
        using var stream = new MemoryStream(bytes, false);
        await PhotographNormalizer.NormalizeAsync(stream, token);
    }

    internal static byte[] Photograph()
    {
        using var bitmap = new SKBitmap(1600, 1200);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.CornflowerBlue);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        return encoded.ToArray();
    }

    internal static byte[] Workbook(int references, bool unique = false, int rows = 1000)
    {
        using var archive = new MemoryStream();
        using (var zip = new ZipArchive(archive, ZipArchiveMode.Create, true))
        {
            void Entry(string path, string content)
            {
                using var writer = new StreamWriter(zip.CreateEntry(path).Open(), new UTF8Encoding(false));
                writer.Write(content);
            }
            Entry("[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/></Types>");
            Entry("xl/workbook.xml", "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>" +
                string.Concat(Enumerable.Range(1, references).Select(i => $"<sheet name=\"S{i}\" sheetId=\"{i}\" r:id=\"r{i}\"/>")) + "</sheets></workbook>");
            Entry("xl/_rels/workbook.xml.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                string.Concat(Enumerable.Range(1, references).Select(i => $"<Relationship Id=\"r{i}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{(unique ? i : 1)}.xml\"/>")) + "</Relationships>");
            for (var page = 1; page <= (unique ? references : 1); page++)
            Entry($"xl/worksheets/sheet{page}.xml", "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>" +
                string.Concat(Enumerable.Range(1, rows).Select(i => $"<row r=\"{i}\"><c r=\"A{i}\"><v>12345678901234567890</v></c></row>")) + "</sheetData></worksheet>");
        }
        return archive.ToArray();
    }
}
