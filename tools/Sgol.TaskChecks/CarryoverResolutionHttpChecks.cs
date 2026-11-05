using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Sgol.BuildingBlocks.Time;
using Sgol.BuildingBlocks.Identifiers;
using Sgol.Configuration.Contracts;
using Sgol.Evidence.Contracts;
using Sgol.Evidence.Technical;
using Sgol.Identity.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Auditing;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence.Planning;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.Authentication;
using Sgol.Web.Presentation.Navigation;

internal static class CarryoverResolutionHttpChecks
{
    public static async Task Run(SgolDbContext db, QaFixtures.Actors fixture, CorrectionSchedulingChecks.Clock clock, Func<Task>? checks = null, bool directorMyWork = false)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(root.FullName, "SistemaSGOL.slnx"))) root = root.Parent!;
        var webRoot=Path.Combine(root.FullName,"src/Sgol.Web/wwwroot");
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions{ApplicationName=typeof(SgolDbContext).Assembly.FullName,WebRootPath=webRoot,EnvironmentName="Development"});
        builder.WebHost.UseUrls("https://localhost:7291");builder.Configuration["SgolApi:TrustedOrigin"]="https://localhost:7291";builder.Logging.SetMinimumLevel(LogLevel.Error);
        builder.Services.AddRazorPages().AddApplicationPart(typeof(SgolDbContext).Assembly);
        builder.Services.AddHttpContextAccessor();builder.Services.AddDataProtection();builder.Services.AddAntiforgery(o=>o.Cookie.SecurePolicy=CookieSecurePolicy.SameAsRequest);
        builder.Services.AddSgolApiClient(builder.Configuration,builder.Environment);builder.Services.AddScoped<RazorAntiforgeryBridge>();builder.Services.AddSingleton<AccessNotice>();
        builder.Services.AddDbContext<SgolDbContext>(o=>o.UseNpgsql(db.Database.GetConnectionString()));builder.Services.AddSingleton<IClock>(clock);builder.Services.AddSingleton<IUuidGenerator,Uuid7Generator>();
        builder.Services.AddScoped<AuditTransaction>();builder.Services.AddScoped<IDirectionPreparedPlanService,EfDirectionPreparedPlanService>();builder.Services.AddScoped<IDirectionPlanningCatalogReader,DirectionPlanningCatalogReader>();
        builder.Services.AddScoped<OperationalTaskService>();builder.Services.AddScoped<OperationalSupervisionService>();builder.Services.AddScoped<OperationalCapacityService>();builder.Services.AddScoped<OperationalCarryoverService>();builder.Services.AddScoped<OperationalCarryoverReader>();builder.Services.AddScoped<OperationalProcessService>();
        builder.Services.AddScoped<Sgol.Web.Infrastructure.Persistence.Identity.PersonAvatarService>();builder.Services.AddScoped<Sgol.Web.Infrastructure.Persistence.Identity.IPersonAvatarReader,Sgol.Web.Infrastructure.Persistence.Identity.PersonAvatarService>();
        builder.Services.AddSingleton<IFileTechnicalValidator,FileTechnicalValidator>();builder.Services.AddSingleton<IFileMalwareScanner,CheckScanner>();
        var directorPerson=await db.AppUsers.Where(u=>u.Id==fixture.Director).Select(u=>u.PersonId).SingleAsync();
        // This isolated test host explicitly substitutes authentication; product forms, CSRF,
        // authorization services, transaction boundaries and SQL persistence remain real.
        builder.Services.AddScoped<IRazorSessionState>(sp=>sp.GetRequiredService<IHttpContextAccessor>().HttpContext!.Request.Path.StartsWithSegments("/mi-trabajo")
            ? new CheckSession(fixture.Worker,fixture.Person,directorMyWork ? CanonicalRole.Direction : CanonicalRole.SalesFloor) : new CheckSession(fixture.Director,directorPerson));
        await using var app=builder.Build();
        app.Use(async(ctx,next)=>{var actor=ctx.Request.Path.StartsWithSegments("/mi-trabajo")?fixture.Worker:fixture.Director;ctx.User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,actor.ToString())],"TEST_ONLY"));await next();});
        var taskCode = await db.OperationalTasks.Where(t => t.Id == fixture.Definition).Select(t => t.TaskCode).SingleAsync();
        app.MapGet("/test-fixture", () => new { taskCode, recipient = fixture.OtherPerson });
        app.UseStaticFiles();app.MapRazorPages();await app.StartAsync();

        Console.WriteLine("RESOLUTION_UI_READY https://localhost:7291/validaciones");
        if (checks == null) await app.WaitForShutdownAsync();
        else { try { await checks(); } finally { await app.StopAsync(); } }
    }
}
