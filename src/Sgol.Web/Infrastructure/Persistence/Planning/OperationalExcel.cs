using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using Sgol.Evidence.Contracts;
using Sgol.Planning.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

internal static class OperationalExcel
{
    internal const string MediaType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    internal static async Task<byte[]> ReadAsync(IFormFile file, CancellationToken token)
    {
        if (file.Length is < 1 or > EvidenceFileLimits.MaximumBytes)
            throw new DirectionPlanException("TASK_FILE_REQUIRED", 422);
        if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase) ||
            file.ContentType is not (MediaType or "application/octet-stream"))
            throw new DirectionPlanException("TASK_FILE_INVALID", 422);
        await using var input = file.OpenReadStream();
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await input.ReadAsync(chunk, token)) > 0)
        {
            if (buffer.Length + read > EvidenceFileLimits.MaximumBytes)
                throw new DirectionPlanException("TASK_FILE_INVALID", 422);
            await buffer.WriteAsync(chunk.AsMemory(0, read), token);
        }
        buffer.Position = 0;
        try
        {
            using var zip = new ZipArchive(buffer, ZipArchiveMode.Read, leaveOpen: true);
            if (zip.Entries.Count > 4096 || zip.Entries.Select(e => e.FullName).Distinct(StringComparer.Ordinal).Count() != zip.Entries.Count ||
                zip.Entries.Sum(e => e.Length) > 60L * 1024 * 1024)
                throw new InvalidDataException();
            // Read all entries with a bound, so malformed or oversized compressed payloads cannot bypass validation.
            long expanded = 0;
            foreach (var entry in zip.Entries)
            {
                if (entry.FullName.Contains("vbaProject", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException();
                await using var stream = entry.Open();
                while ((read = await stream.ReadAsync(chunk, token)) > 0)
                    if ((expanded += read) > 60L * 1024 * 1024) throw new InvalidDataException();
            }
            XDocument Xml(string path)
            {
                using var stream = (zip.GetEntry(path) ?? throw new InvalidDataException()).Open();
                using var reader = XmlReader.Create(stream, new XmlReaderSettings {
                    DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 60L * 1024 * 1024 });
                return XDocument.Load(reader);
            }
            XNamespace types = "http://schemas.openxmlformats.org/package/2006/content-types";
            XNamespace sheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            XNamespace rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            XNamespace packageRel = "http://schemas.openxmlformats.org/package/2006/relationships";
            var contentTypes = Xml("[Content_Types].xml");
            if (!contentTypes.Descendants(types + "Override").Any(e => (string?)e.Attribute("PartName") == "/xl/workbook.xml" &&
                (string?)e.Attribute("ContentType") == "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"))
                throw new InvalidDataException();
            var workbook = Xml("xl/workbook.xml");
            var relationships = Xml("xl/_rels/workbook.xml.rels");
            var sheets = workbook.Root?.Element(sheet + "sheets")?.Elements(sheet + "sheet").ToArray();
            if (workbook.Root?.Name != sheet + "workbook" || sheets is not { Length: > 0 }) throw new InvalidDataException();
            foreach (var item in sheets)
            {
                var relationship = relationships.Descendants(packageRel + "Relationship").SingleOrDefault(e =>
                    (string?)e.Attribute("Id") == (string?)item.Attribute(rel + "id")) ?? throw new InvalidDataException();
                if ((string?)relationship.Attribute("TargetMode") == "External") throw new InvalidDataException();
                var target = (string?)relationship.Attribute("Target") ?? throw new InvalidDataException();
                var path = new Uri(new Uri("https://xlsx.local/xl/workbook.xml"), target).AbsolutePath.TrimStart('/');
                if (Xml(path).Root?.Name != sheet + "worksheet") throw new InvalidDataException();
            }
        }
        catch (Exception e) when (e is InvalidDataException or XmlException or InvalidOperationException or UriFormatException or OverflowException)
        {
            throw new DirectionPlanException("TASK_FILE_INVALID", 422);
        }
        return buffer.ToArray();
    }
}
