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
        using var timing = Sgol.Web.Infrastructure.Http.PerformanceWork.Measure("excel.total");
        using var admission = await Sgol.Web.Infrastructure.Http.PerformanceWork.Excel.EnterAsync(token);
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
            long parsedBytes = 0;
            var parsed = new HashSet<string>(StringComparer.Ordinal);
            XmlReader Reader(string path)
            {
                token.ThrowIfCancellationRequested();
                var entry = zip.GetEntry(path) ?? throw new InvalidDataException();
                if (!parsed.Add(path) || (parsedBytes += entry.Length) > 60L * 1024 * 1024) throw new InvalidDataException();
                return XmlReader.Create(entry.Open(), new XmlReaderSettings {
                    Async = true, CloseInput = true, DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
                    MaxCharactersInDocument = 60L * 1024 * 1024 });
            }
            async Task<XDocument> Xml(string path)
            {
                if ((zip.GetEntry(path)?.Length ?? long.MaxValue) > 4L * 1024 * 1024) throw new InvalidDataException();
                using var reader = Reader(path);
                return await XDocument.LoadAsync(reader, LoadOptions.None, token);
            }
            XNamespace types = "http://schemas.openxmlformats.org/package/2006/content-types";
            XNamespace sheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            XNamespace rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            XNamespace packageRel = "http://schemas.openxmlformats.org/package/2006/relationships";
            var contentTypes = await Xml("[Content_Types].xml");
            if (!contentTypes.Descendants(types + "Override").Any(e => (string?)e.Attribute("PartName") == "/xl/workbook.xml" &&
                (string?)e.Attribute("ContentType") == "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"))
                throw new InvalidDataException();
            var workbook = await Xml("xl/workbook.xml");
            var relationships = await Xml("xl/_rels/workbook.xml.rels");
            var sheets = workbook.Root?.Element(sheet + "sheets")?.Elements(sheet + "sheet").ToArray();
            if (workbook.Root?.Name != sheet + "workbook" || sheets is not { Length: > 0 }) throw new InvalidDataException();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var relationIds = new HashSet<string>(StringComparer.Ordinal);
            var paths = new HashSet<string>(StringComparer.Ordinal);
            var relations = relationships.Descendants(packageRel + "Relationship").ToDictionary(e =>
                (string?)e.Attribute("Id") ?? throw new InvalidDataException(), StringComparer.Ordinal);
            foreach (var item in sheets)
            {
                token.ThrowIfCancellationRequested();
                var relationId = (string?)item.Attribute(rel + "id") ?? throw new InvalidDataException();
                if (!ids.Add((string?)item.Attribute("sheetId") ?? throw new InvalidDataException()) ||
                    !names.Add((string?)item.Attribute("name") ?? throw new InvalidDataException()) || !relationIds.Add(relationId)) throw new DirectionPlanException("TASK_EXCEL_DUPLICATE_SHEETS", 422);
                var relationship = relations.GetValueOrDefault(relationId) ?? throw new InvalidDataException();
                if ((string?)relationship.Attribute("TargetMode") == "External") throw new InvalidDataException();
                if ((string?)relationship.Attribute("Type") != rel.NamespaceName + "/worksheet") throw new InvalidDataException();
                var target = (string?)relationship.Attribute("Target") ?? throw new InvalidDataException();
                var uri = new Uri(new Uri("https://xlsx.local/xl/workbook.xml"), target);
                if (uri.Host != "xlsx.local" || uri.Scheme != "https" || uri.Query.Length > 0 || uri.Fragment.Length > 0) throw new InvalidDataException();
                var path = Uri.UnescapeDataString(uri.AbsolutePath).TrimStart('/');
                if (!paths.Add(path)) throw new DirectionPlanException("TASK_EXCEL_DUPLICATE_SHEETS", 422);
            }
            // Validate worksheets without materializing cells. Each XML node observes cancellation.
            foreach (var path in paths)
            {
                using var reader = Reader(path);
                var root = false;
                var nodes = 0;
                while (await reader.ReadAsync())
                {
                    token.ThrowIfCancellationRequested();
                    if (++nodes % 256 == 0) Sgol.Web.Infrastructure.Http.PerformanceWork.ParsedNodes.Add(256);
                    if (!root && reader.NodeType == XmlNodeType.Element)
                    {
                        if (reader.LocalName != "worksheet" || reader.NamespaceURI != sheet.NamespaceName) throw new InvalidDataException();
                        root = true;
                    }
                }
                if (!root) throw new InvalidDataException();
            }
        }
        catch (Exception e) when (e is InvalidDataException or XmlException or InvalidOperationException or UriFormatException or OverflowException or ArgumentException)
        {
            throw new DirectionPlanException("TASK_FILE_INVALID", 422);
        }
        token.ThrowIfCancellationRequested();
        return buffer.ToArray();
    }
}
