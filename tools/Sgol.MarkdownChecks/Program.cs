using System.Text.Encodings.Web;
using System.Text.Json;
using Sgol.Web.Presentation.Components;

static string Render(string? source)
{
    using var writer = new StringWriter();
    MarkdownContent.Render(source).WriteTo(writer, HtmlEncoder.Default);
    return writer.ToString();
}

var cases = new Dictionary<string, string>
{
    ["format"] = "# Instrucciones\n\n**Negrita** y *cursiva*, ~~tachado~~, `código`.\n\n- Primero\n- Segundo\n\n1. Uno\n2. Dos\n\n> Cita\n\n```html\n<script>alert(1)</script>\n```\n\n[Consulta](https://example.com)\n\n| Campo | Valor |\n| --- | --- |\n| A | B |",
    ["plain"] = "Texto normal & caracteres < >\nSegunda línea",
    ["html"] = "<script>globalThis.pwned=1</script><img src=x onerror=globalThis.pwned=1><svg onload=globalThis.pwned=1></svg><iframe srcdoc='<script>alert(1)</script>'></iframe>",
    ["links"] = "[a](javascript:alert%281%29) [b](JaVaScRiPt:alert%281%29) [c](data:text/html,evil) [d](vbscript:evil) [e](file:///C:/Windows) [f](//evil.example) [g](java&#x73;cript:evil) [h](javascript&#58;evil) [i](java%73cript:evil) [j](https://example.com/\"onclick=evil)",
    ["autolinks"] = "<javascript:evil> <data:text/html,evil> <https://example.com>",
    ["images"] = "![Texto alternativo](https://example.com/tracker.png) ![x](data:image/svg+xml,evil)",
    ["reference"] = "[Peligro][ref]\n\n[ref]: javascript:evil\n\n[Correo](mailto:example@example.com)",
    ["raw"] = "@{System.IO.File.Delete(\"x\");} $(whoami) <% evil %> {{evil}}",
    ["empty"] = "",
    ["long"] = new string('x', 4000),
    ["malformed"] = "**sin cerrar [enlace]( <b> &lt;script&gt;evil&lt;/script&gt;"
};
var rendered = cases.ToDictionary(pair => pair.Key, pair => new { source = pair.Value, html = Render(pair.Value) });
void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
foreach (var tag in new[] { "strong", "em", "del", "code", "ul", "ol", "blockquote", "pre", "table" })
    Check(rendered["format"].html.Contains($"<{tag}>"), $"Missing formatting: {tag}");
foreach (var value in rendered.Values)
    foreach (var tag in new[] { "<script", "<img", "<svg", "<iframe", "<object", "<style" })
        Check(!value.html.Contains(tag, StringComparison.OrdinalIgnoreCase), $"Executable or remote element: {tag}");
Check(!rendered["links"].html.Contains("href=\"javascript", StringComparison.OrdinalIgnoreCase), "Unsafe link");
Check(rendered["plain"].html.Contains("<br />"), "Lost existing newline");
Check(Render(null) == "", "Null must render empty");
Check(rendered["images"].html.Contains("Texto alternativo"), "Lost image alternative text");
Check(rendered["raw"].html.Contains("System.IO.File.Delete"), "Code must remain text");
var folder = Path.GetFullPath("artifacts/markdown-checks");
Directory.CreateDirectory(folder);
File.WriteAllText(Path.Combine(folder, "cases.json"), JsonSerializer.Serialize(rendered));
Console.WriteLine($"PASS: {rendered.Count} Markdown and hostile-input cases. Browser fixtures: {folder}");
