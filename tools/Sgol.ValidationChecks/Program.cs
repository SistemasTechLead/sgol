using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.Rendering;
using Sgol.BuildingBlocks.Time;
using Sgol.Web.Infrastructure.Persistence.Planning;
using Sgol.Web.Pages.Validations;

// Render the actual compiled cards, with synthetic data and no database access.
var root = new DirectoryInfo(Directory.GetCurrentDirectory());
while (!File.Exists(Path.Combine(root.FullName, "src/Sgol.Web/Sgol.Web.csproj")))
    root = root.Parent ?? throw new Exception("Repository root not found");
Directory.SetCurrentDirectory(root.FullName);
var builder = WebApplication.CreateBuilder(new WebApplicationOptions {
    ApplicationName = typeof(IndexModel).Assembly.FullName,
    WebRootPath = Path.GetFullPath("src/Sgol.Web/wwwroot") });
builder.Logging.SetMinimumLevel(LogLevel.Error);
builder.Services.AddRazorPages();
builder.Services.Configure<RazorViewEngineOptions>(o => {
    o.ViewLocationFormats.Add("/Pages/Validations/{0}.cshtml");
    o.ViewLocationFormats.Add("/Pages/Shared/{0}.cshtml");
});
await using var app = builder.Build();
app.UseStaticFiles(new StaticFileOptions { FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(Path.Combine(root.FullName, "src/Sgol.Web/wwwroot")) });
var count = 0;
app.MapGet("/", async (HttpContext context, IRazorViewEngine views, ITempDataProvider tempData) => {
    var model = new IndexModel(null!, null!, null!, null!, new CheckClock(), null!) {
        Validation = true, PageContext = new PageContext { HttpContext = context }
    };
    var requirements = new[] { "NINGUNO", "COMENTARIO", "CHECKLIST", "FOTOGRAFIA", "CAPTURA_PANTALLA", "ARCHIVO" };
    var items = requirements.SelectMany(requirement => new string?[] {
        "Conclusión <script>alert(1)</script> & texto\nSegunda línea " + new string('x', 3900), null, "", "  "
    }.Select(conclusion => new SupervisedTask {
        Role = "PISO_VENTA", PlanName = "Prueba", Period = "SEMANAL",
        Assignment = new OperationalAssignment {
            Id = Guid.NewGuid(), Name = requirement, Requirement = requirement,
            PersonName = "Persona de prueba", Conclusion = conclusion,
            Comment = requirement == "COMENTARIO" ? "Evidencia escrita" : null,
            FileName = requirement is "ARCHIVO" or "FOTOGRAFIA" or "CAPTURA_PANTALLA" ? "evidencia.pdf" : null,
            StartsAt = DateTimeOffset.UtcNow.AddDays(-1), CompletedAt = DateTimeOffset.UtcNow,
            ReviewStatus = "CONCLUIDA"
        }
    })).ToArray();
    typeof(IndexModel).GetProperty(nameof(IndexModel.Sections))!.SetValue(model,
        new[] { new SupervisionSection("pending", "Pruebas", items, items) { Total = items.Length } });
    var action = new ActionContext(context, new RouteData(), new PageActionDescriptor { ViewEnginePath = "/Validations/Index" });
    var result = views.GetView(null, "/Pages/Validations/_ValidationCards.cshtml", false);
    if (!result.Success) throw new Exception(string.Join(",", result.SearchedLocations));
    using var output = new StringWriter();
    await result.View.RenderAsync(new ViewContext(action, result.View,
        new ViewDataDictionary<IndexModel>(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = model },
        new TempDataDictionary(context, tempData), output, new HtmlHelperOptions()));
    var html = output.ToString();
    foreach (var item in items) {
        var card = html.Split("id=\"review-card-" + item.Assignment.Id.ToString("N") + "\"")[1].Split("</article>")[0];
        var hasConclusion = !string.IsNullOrWhiteSpace(item.Assignment.Conclusion);
        if (card.Contains("validation-card__conclusion") != hasConclusion) throw new Exception("Missing/unexpected conclusion: " + item.Assignment.Requirement);
        if (hasConclusion && card.Split(HtmlEncoder.Default.Encode(item.Assignment.Conclusion!)).Length != 2)
            throw new Exception("Conclusion missing, duplicated or not encoded");
        if (card.Contains("<script>")) throw new Exception("Unescaped HTML");
        if (item.Assignment.Comment is {} comment && !card.Contains(comment)) throw new Exception("Lost evidence comment");
        count++;
    }
    return Results.Content("<!doctype html><html lang='es' data-theme='light'><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><link rel='stylesheet' href='/css/tokens.css'><link rel='stylesheet' href='/css/components.css'><link rel='stylesheet' href='/css/validation-cards.css'><body>" + html + "</body></html>", "text/html");
});
app.Urls.Add("http://127.0.0.1:7398");
await app.StartAsync();
using var client = new HttpClient();
var response = await client.GetAsync("http://127.0.0.1:7398/");
response.EnsureSuccessStatusCode();
Directory.CreateDirectory("artifacts/validation-conclusions");
await File.WriteAllTextAsync("artifacts/validation-conclusions/cards.html", await response.Content.ReadAsStringAsync());
Console.WriteLine($"PASS: {count} real Razor card cases; conclusions, empty values, encoding and retained evidence.");
if (args.Contains("--serve")) await app.WaitForShutdownAsync();
else await app.StopAsync();

sealed class CheckClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
