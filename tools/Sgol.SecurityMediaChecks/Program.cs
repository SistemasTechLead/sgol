using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Sgol.Evidence.Technical;
using Sgol.Web.Infrastructure.Persistence;

// Bounded synthetic inputs only. No application server, database or storage connection.
var results = new List<object>();
var type = typeof(SgolDbContext).Assembly.GetType("Sgol.Web.Infrastructure.Persistence.Planning.OperationalExcel")!;
var read = type.GetMethod("ReadAsync", BindingFlags.Static | BindingFlags.NonPublic)!;
async Task Excel(string name, byte[] bytes, bool expectedAccept)
{
    using var stream = new MemoryStream(bytes);
    var file = new FormFile(stream, 0, bytes.Length, "file", "probe.xlsx") { Headers = new HeaderDictionary(), ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" };
    bool accepted; string? error = null;
    try { await (Task<byte[]>)read.Invoke(null, new object[] { file, CancellationToken.None })!; accepted = true; }
    catch (Exception ex) { accepted = false; error = (ex is TargetInvocationException ? ex.InnerException : ex)?.GetType().Name; }
    results.Add(new { name, accepted, expectedAccept, passed = accepted == expectedAccept, inputBytes = bytes.Length, error });
}
byte[] Workbook(string? xml = null, string? target = null, bool duplicate = false, bool large = false, bool macro = false)
{
    using var bytes = new MemoryStream();
    using (var zip = new ZipArchive(bytes, ZipArchiveMode.Create, true))
    {
        void Entry(string path, string text) { using var writer = new StreamWriter(zip.CreateEntry(path).Open(), new UTF8Encoding(false)); writer.Write(text); }
        Entry("[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/></Types>");
        Entry("xl/workbook.xml", "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"S1\" sheetId=\"1\" r:id=\"r1\"/>" + (duplicate ? "<sheet name=\"S2\" sheetId=\"2\" r:id=\"r2\"/>" : "") + "</sheets></workbook>");
        string Rel(string id) => $"<Relationship Id=\"{id}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"{target ?? "worksheets/sheet1.xml"}\"/>";
        Entry("xl/_rels/workbook.xml.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" + Rel("r1") + (duplicate ? Rel("r2") : "") + "</Relationships>");
        Entry("xl/worksheets/sheet1.xml", xml ?? "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData/></worksheet>");
        if (macro) Entry("xl/vbaProject.bin", "synthetic-not-executable");
        if (large)
        {
            using var sink = zip.CreateEntry("padding.bin", CompressionLevel.SmallestSize).Open();
            byte[] padding = new byte[1024 * 1024];
            for (int i = 0; i < 61; i++) sink.Write(padding);
        }
    }
    return bytes.ToArray();
}
await Excel("valid minimal workbook", Workbook(), true);
await Excel("XXE local-file declaration", Workbook("<!DOCTYPE worksheet [<!ENTITY x SYSTEM 'file:///nonexistent-sgol-security-sentinel'>]><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">&x;</worksheet>"), false);
await Excel("external worksheet URL", Workbook(target: "https://127.0.0.1:1/probe"), false);
await Excel("zip path traversal worksheet", Workbook(target: "../../../../outside.xml"), false);
await Excel("repeated worksheet amplification", Workbook(duplicate: true), false);
await Excel("61 MiB compressed expansion", Workbook(large: true), false);
await Excel("embedded VBA member", Workbook(macro: true), false);
var validator = new FileTechnicalValidator();
foreach (var (name, body, media, fileName) in new[] {
    ("HTML disguised as JPEG", "<html><script>alert(1)</script></html>", "image/jpeg", "photo.jpg"),
    ("SVG disguised as PNG", "<svg xmlns='http://www.w3.org/2000/svg' onload='alert(1)'/>", "image/png", "photo.png"),
    ("executable extension", "MZ synthetic", "image/jpeg", "photo.exe") })
{
    await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(body));
    var result = await validator.ValidateAsync(stream, media, fileName, default);
    if (result.File != null) await result.File.DisposeAsync();
    results.Add(new { name, accepted = result.IsValid, expectedAccept = false, passed = !result.IsValid });
}
var json = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
await File.WriteAllTextAsync(Path.GetFullPath("artifacts/security-audit/implementation/media-results.json"), json);
Console.WriteLine(json);

if (results.Any(r => JsonSerializer.SerializeToElement(r).GetProperty("passed").GetBoolean() == false)) Environment.ExitCode = 1;
