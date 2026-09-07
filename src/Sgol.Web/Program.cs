using Microsoft.Extensions.Logging.Console;
using Sgol.Web.Infrastructure.Http;
using Sgol.Web.Infrastructure.Evidence;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Presentation.Endpoints;

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
builder.Services.AddSgolEvidenceInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "__Host-SGOL-CSRF";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.Path = "/";
});
builder.Services.AddSgolPersistence(builder.Configuration);

var app = builder.Build();

app.UseSgolHttpPrimitives();
app.UseStaticFiles();

app.MapGet("/health/live", (HttpContext context) => Results.Ok(new
{
    status = "alive",
    meta = new { correlationId = context.GetCorrelationId() }
}));
app.MapRazorPages();
app.MapBranchApi();
app.MapPersonApi();
app.MapAccountApi();
app.MapConfigurationApi();
app.MapCalendarApi();
app.MapTaskDefinitionApi();
app.MapEligibilityPolicyApi();
app.MapActivationPolicyApi();
app.MapEvidencePolicyApi();
app.MapWeekApi();
app.MapWorkPlanApi();
app.MapPlanPublicationApi();
app.MapGenerationRequestApi();
app.MapEligibilityEvaluationApi();
app.MapAssignmentCorrectionApi();
app.MapActiveLoadApi();
app.MapObligationQueryApi();
app.MapEvidenceApi();

app.Run();

public partial class Program;
