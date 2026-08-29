using Microsoft.Extensions.Logging.Console;
using Sgol.Web.Infrastructure.Http;
using Sgol.Web.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "O";
    options.UseUtcTimestamp = true;
});

builder.Services.AddRazorPages();
builder.Services.AddSgolHttpPrimitives();
builder.Services.AddSgolPersistence(builder.Configuration);

var app = builder.Build();

app.UseSgolHttpPrimitives();

app.MapGet("/health/live", (HttpContext context) => Results.Ok(new
{
    status = "alive",
    meta = new { correlationId = context.GetCorrelationId() }
}));
app.MapRazorPages();

app.Run();

public partial class Program;
