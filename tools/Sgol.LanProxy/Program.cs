using System.Net;
using System.Security.Cryptography.X509Certificates;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);
var lan = builder.Configuration.GetRequiredSection("Lan");
var address = IPAddress.Parse(lan["Address"]!);
var certificate = X509CertificateLoader.LoadPkcs12FromFile(lan["CertificatePath"]!, lan["CertificatePassword"]);
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(7240, endpoint => endpoint.UseHttps());
    options.Listen(address, 7440, endpoint => endpoint.UseHttps(certificate));
    options.Listen(address, 7443, endpoint => endpoint.UseHttps(certificate));
});
// Avoid logging query strings containing signed evidence authorizations.
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
builder.Logging.AddFilter("Yarp", LogLevel.Warning);
RouteConfig Route(string id, int port, string cluster) => new()
{
    RouteId = id, ClusterId = cluster,
    MaxRequestBodySize = cluster == "web" ? 33554432 : null,
    Match = new RouteMatch { Path = "/{**path}", Hosts = [$"{address}:{port}"] },
    Transforms = [new Dictionary<string, string> { ["RequestHeaderOriginalHost"] = "true" },
        new Dictionary<string, string> { ["X-Forwarded"] = "Set" }]
};
builder.Services.AddReverseProxy().LoadFromMemory(
    [Route("web", 7440, "web"), Route("evidence", 7443, "evidence")],
    [new ClusterConfig { ClusterId = "web", Destinations = new Dictionary<string, DestinationConfig>
        { ["local"] = new() { Address = "http://127.0.0.1:7241" } } },
     new ClusterConfig { ClusterId = "evidence", Destinations = new Dictionary<string, DestinationConfig>
        { ["local"] = new() { Address = "http://127.0.0.1:8333" } } }]);
var app = builder.Build();
app.Use(async (context, next) =>
{
    if (context.Connection.LocalPort == 7240)
    {
        if (!string.Equals(context.Request.Host.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        { context.Response.StatusCode = 400; return; }
        context.Response.Redirect($"https://{address}:7440{context.Request.PathBase}{context.Request.Path}{context.Request.QueryString}");
        return;
    }
    if (context.Request.Host.Value != $"{address}:{context.Connection.LocalPort}")
    { context.Response.StatusCode = 400; return; }
    await next();
});
app.MapReverseProxy();
app.Run();
