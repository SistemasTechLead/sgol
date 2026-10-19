using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Sgol.Web.Infrastructure.Http;
using Sgol.Web.Infrastructure.Evidence;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Authentication;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.Endpoints;
using Sgol.Web.Presentation.Navigation;
using Sgol.Web.Presentation.Authentication;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "O";
    options.UseUtcTimestamp = true;
});

builder.Services.AddRazorPages(options =>
{
    options.Conventions.ConfigureFilter(new Microsoft.AspNetCore.Mvc.ServiceFilterAttribute(typeof(RoleScreenAccessFilter)));
    options.Conventions.AddPageRoute("/Access/Index", "acceso/cambiar-contrasena");
    options.Conventions.AddPageRoute("/Access/Index", "acceso/mfa/enrolar");
    options.Conventions.AddPageRoute("/Access/Index", "acceso/mfa/verificar");
    options.Conventions.AddPageRoute("/Access/Index", "acceso/codigos-recuperacion");
    options.Conventions.AddPageRoute("/Tasks/Assign", "asignar-tarea/asignar");
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IRazorSessionState, RazorSessionState>();
builder.Services.AddScoped<RoleScreenAccessFilter>();
builder.Services.AddScoped<RazorAntiforgeryBridge>();
builder.Services.AddSingleton<SafeReturnDestination>();
builder.Services.AddSingleton<AccessNotice>();
builder.Services.AddSgolApiClient(builder.Configuration, builder.Environment);
builder.Services.AddSgolHttpPrimitives();
builder.Services.AddLocalSharedTransport(builder.Configuration);
builder.Services.AddSgolHostedAuthentication();
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
builder.Services.AddHostedService<ExpiredCameraCleanupService>();

var app = builder.Build();

app.UseLocalSharedTransport(builder.Configuration);
app.UseSgolHttpPrimitives();
app.Use(async (context, next) =>
{
    context.Response.Headers["Permissions-Policy"] = "camera=(self), microphone=()";
    await next(context);
});
app.UseStaticFiles();
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<ScreenAccessMiddleware>();
app.UseMiddleware<AntiforgeryValidationMiddleware>();
app.UseAuthorization();

app.MapGet("/health/live", (HttpContext context) => Results.Ok(new
{
    status = "alive",
    meta = new { correlationId = context.GetCorrelationId() }
}));
app.MapGet("/health/ready", async (HttpContext context, HealthCheckService healthChecks) =>
{
    var ready = false;
    try
    {
        var report = await healthChecks.CheckHealthAsync(
            registration => registration.Tags.Contains("ready"), context.RequestAborted);
        ready = report.Status == HealthStatus.Healthy;
    }
    catch (Exception) when (!context.RequestAborted.IsCancellationRequested)
    {
        ready = false;
    }

    return Results.Json(new
    {
        status = ready ? "ready" : "unavailable",
        meta = new { correlationId = context.GetCorrelationId() }
    }, statusCode: ready ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
});
app.MapRazorPages();
app.MapAuthenticationApi();
app.MapScreenDeviceApi();
app.MapBranchApi();
app.MapPersonApi();
app.MapAccountApi();
app.MapConfigurationApi();
app.MapCalendarApi();
app.MapDirectionPreparedPlanApi();
// Historical tables remain available; the retired workflow is opt-in.
if (builder.Configuration.GetValue<bool>("LegacyWorkflow:Enabled"))
{
    app.MapTaskDefinitionApi();
    app.MapEligibilityPolicyApi();
    app.MapActivationPolicyApi();
    app.MapEvidencePolicyApi();
    app.MapValidationPolicyApi();
    app.MapWeekApi();
    app.MapWorkPlanApi();
    app.MapPlanPublicationApi();
    app.MapPlanQueryApi();
    app.MapGenerationRequestApi();
    app.MapEligibilityEvaluationApi();
    app.MapAssignmentCorrectionApi();
    app.MapActiveLoadApi();
    app.MapObligationQueryApi();
    app.MapHierarchySupervisionApi();
    app.MapObligationConclusionApi();
    app.MapValidationDecisionApi();
    app.MapInboxApi();
    app.MapEvidenceApi();
}

app.Run();

public partial class Program;
