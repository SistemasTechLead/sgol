using System.Net;
using System.Text.Json;
using Sgol.Identity.Contracts;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.Navigation;

var origin = new Uri("https://localhost:7240");
var now = DateTimeOffset.UtcNow;
if (args.Contains("--session-lifetime-local")) await SessionLifetimeChecks.RunAsync();
var snapshot = new SessionSnapshot(Guid.CreateVersion7(), Guid.CreateVersion7(), "demo", "Demo",
    "LOR-001", "DIRECCION", [], now, now.AddDays(1), now.AddDays(1));
var handler = new ResponseHandler(snapshot);
using var http = new HttpClient(handler) { BaseAddress = origin };
var context = new DefaultHttpContext();
context.Request.Scheme = origin.Scheme;
context.Request.Host = new HostString(origin.Host, origin.Port);
var accessor = new HttpContextAccessor { HttpContext = context };
var client = new SgolApiClient(http, accessor);
foreach (var extra in new[] { "", "; .AspNetCore.Mvc.CookieTempDataProvider=success", "; other-local-app=value" })
{
    context.Request.Headers.Cookie = "__Host-SGOL-Session=session; __Host-SGOL-CSRF=csrf" + extra;
    var state = new RazorSessionState(client, accessor);
    if ((await state.GetAsync())?.UserId != snapshot.UserId || state.IsInvalid)
        throw new Exception("Session rejected after a Razor success notice.");
    if (handler.LastCookies != "__Host-SGOL-Session=session")
        throw new Exception("Unrelated cookies were forwarded to the session API.");
    await client.SendAsync<JsonElement>(new(HttpMethod.Post, "/api/v1/auth/login", ApiResponseShape.Item,
        new { userName = "demo", password = "test" }, CsrfToken: "request-token"));
    if (handler.LastCookies != "__Host-SGOL-CSRF=csrf")
        throw new Exception("Login forwarded cookies outside its allowlist.");
}
context.Request.Headers.Cookie = "__Host-SGOL-Session=first; __Host-SGOL-Session=duplicate";
try { await client.SendAsync<JsonElement>(new(HttpMethod.Get, "/api/v1/auth/session", ApiResponseShape.Item));
    throw new Exception("Duplicate authentication cookies were accepted."); }
catch (ApiProtocolException) { }
Console.WriteLine("Cookie regression passed: session and login survive TempData, unrelated cookies are not forwarded, duplicate auth cookies rejected.");

sealed class ResponseHandler(SessionSnapshot snapshot) : HttpMessageHandler
{
    public string? LastCookies { get; private set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        LastCookies = request.Headers.TryGetValues("Cookie", out var values) ? string.Join("; ", values) : null;
        var correlation = Guid.CreateVersion7().ToString("D");
        var response = new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent(JsonSerializer.Serialize(new { data = snapshot, meta = new { correlationId = correlation } },
                new JsonSerializerOptions(JsonSerializerDefaults.Web)), System.Text.Encoding.UTF8, "application/json")
        };
        response.Headers.Add("X-Correlation-ID", correlation);
        return Task.FromResult(response);
    }
}
