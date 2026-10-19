using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Sgol.Identity.Contracts;
using Sgol.Web.Infrastructure.Http;
using Sgol.Web.Presentation.Authentication;
using Sgol.Web.Presentation.Navigation;

var root = Directory.GetCurrentDirectory();
while (!Directory.Exists(Path.Combine(root, "src", "Sgol.Web")))
    root = Directory.GetParent(root)?.FullName ?? throw new Exception("Run checks from the SistemaSGOL checkout.");
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = root, WebRootPath = Path.Combine(root, "src/Sgol.Web/wwwroot") });
var serve = args.Contains("--serve");
builder.WebHost.UseUrls(serve ? "http://127.0.0.1:7255" : "http://127.0.0.1:0");
builder.Logging.ClearProviders();
builder.Services.AddRazorPages().AddApplicationPart(typeof(SystemErrorPage).Assembly);
builder.Services.AddSgolHttpPrimitives();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IRazorSessionState, FixtureSession>();
builder.Services.AddScoped(sp => new RazorAntiforgeryBridge(sp.GetRequiredService<IAntiforgery>(), null!));
await using var app = builder.Build();
app.UseSgolHttpPrimitives();
app.UseStaticFiles();
app.Use(async (context, next) =>
{
    if (context.Request.Headers.ContainsKey("X-Fixture-Session") || context.Request.Path.StartsWithSegments("/context"))
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "fixture")], "fixture"));
    await next();
});
app.MapGet("/status/{status:int}", (int status) => Results.StatusCode(status));
app.MapGet("/context/{status:int}", (int status) => Results.StatusCode(status));
app.MapGet("/appearance", () => Results.Content("""
    <!doctype html><html lang="es"><head><meta name="viewport" content="width=device-width, initial-scale=1"><script src="/js/theme.js"></script><link rel="stylesheet" href="/css/tokens.css"><link rel="stylesheet" href="/css/components.css"></head><body><main>
    <h1>Apariencia de pruebas</h1><label><input type="radio" name="theme" value="light" data-theme-choice>Claro</label><label><input type="radio" name="theme" value="dark" data-theme-choice>Oscuro</label>
    <label><input type="radio" name="layout" value="auto" data-layout-choice>Automática</label><label><input type="radio" name="layout" value="mobile" data-layout-choice>Móvil</label>
    <p><a href="/status/404">404 esencial</a></p><p><a href="/context/403">403 con contexto</a></p><p><a href="/csrf">Formulario rechazado</a></p>
    </main></body></html>
    """, "text/html"));
app.MapGet("/api/check", () => Results.StatusCode(403));
app.MapGet("/health/check", () => Results.StatusCode(503));
app.MapGet("/failure", Fail);
app.MapGet("/existing", () => Results.Text("existing response", statusCode: 422));
app.MapGet("/csrf", (HttpContext context) => { context.Items[SystemErrorPageRenderer.CsrfFailure] = true; return Results.StatusCode(400); });
await app.StartAsync();
if (serve) { Console.WriteLine("Visual checks: http://127.0.0.1:7255/appearance (isolated fixtures, no database)"); await app.WaitForShutdownAsync(); return; }
using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
foreach (var status in new[] {400,401,403,404,409,412,429,500,503})
{
    using var response = await Send($"/status/{status}", "text/html");
    var html = await response.Content.ReadAsStringAsync();
    Check((int)response.StatusCode == status && response.Content.Headers.ContentType?.MediaType == "text/html", $"HTML status {status}");
    Check(html.Contains("error-titulo") && html.Contains("Detalles para soporte") && html.Contains(response.Headers.GetValues("X-Correlation-ID").Single()), $"Screen and real correlation {status}");
    Check(response.Headers.CacheControl?.NoStore == true, $"No-store {status}");
}
using (var response = await Send("/status/403", "text/html", "valid"))
{
    var html = await response.Content.ReadAsStringAsync();
    Check(html.Contains("barra-lateral") && !html.Contains("Personas y accesos"), "Context uses actual role navigation");
}
using (var response = await Send("/status/404", "text/html", "broken"))
    Check((await response.Content.ReadAsStringAsync()).Contains("error-sistema__marca"), "Session lookup failure still renders essential view");
foreach (var path in new[] {"/api/check", "/health/check", "/status/404"})
{
    using var response = await Send(path, path == "/status/404" ? "application/json" : "text/html, application/json");
    Check(response.Content.Headers.ContentType?.MediaType == "application/problem+json", $"JSON contract: {path}");
}
using (var response = await Send("/existing", "text/html"))
    Check(await response.Content.ReadAsStringAsync() == "existing response", "Existing validation response preserved");
using (var response = await Send("/failure", "text/html"))
{
    var html = await response.Content.ReadAsStringAsync();
    Check(response.StatusCode == HttpStatusCode.InternalServerError && html.Contains("error-titulo") && !html.Contains("PRIVATE-EXCEPTION-DETAIL"), "Safe HTML exception response");
}
using (var response = await Send("/failure", "application/json"))
    Check(response.Content.Headers.ContentType?.MediaType == "application/problem+json" && !(await response.Content.ReadAsStringAsync()).Contains("PRIVATE-EXCEPTION-DETAIL"), "Safe JSON exception response");
using (var response = await Send("/csrf", "text/html"))
    Check((await response.Content.ReadAsStringAsync()).Contains("Este formulario necesita actualizarse"), "CSRF guidance");
await app.StopAsync();
Console.WriteLine("All error page checks passed.");

async Task<HttpResponseMessage> Send(string path, string accept, string? session = null)
{
    using var request = new HttpRequestMessage(HttpMethod.Get, path);
    request.Headers.TryAddWithoutValidation("Accept", accept);
    if (session != null) request.Headers.Add("X-Fixture-Session", session);
    return await client.SendAsync(request);
}
static void Check(bool valid, string label) { if (!valid) throw new Exception(label); Console.WriteLine("PASS " + label); }
static IResult Fail() => throw new InvalidOperationException("PRIVATE-EXCEPTION-DETAIL");
sealed class FixtureSession(IHttpContextAccessor accessor) : IRazorSessionState
{
    public bool IsInvalid => false;
    public void Invalidate() { }
    public Task<SessionSnapshot?> GetAsync(CancellationToken cancellationToken = default)
    {
        if (accessor.HttpContext!.Request.Headers["X-Fixture-Session"] == "broken") throw new InvalidOperationException("Dependency unavailable");
        return Task.FromResult<SessionSnapshot?>(new(Guid.NewGuid(), Guid.NewGuid(), "fixture", "Cuenta de prueba", "LOR-001", CanonicalRole.SalesFloor, [], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow.AddHours(2)));
    }
}
