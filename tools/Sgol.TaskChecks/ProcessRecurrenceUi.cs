using Sgol.Web.Presentation.Endpoints;
using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
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

internal static class ProcessRecurrenceUi
{
    internal static async Task Run(SgolDbContext db) {
        var actor = await db.RoleAssignmentVersions.Where(r => r.RoleCode == CanonicalRole.Direction).Select(r => r.UserId).FirstAsync();
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(root.FullName, "SistemaSGOL.slnx"))) root = root.Parent ?? throw new Exception("Source root not found.");
        var webRoot = Path.Combine(root.FullName, "src", "Sgol.Web", "wwwroot");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(SgolDbContext).Assembly.FullName,
            WebRootPath = webRoot, EnvironmentName = "Development" });
        builder.WebHost.UseUrls("https://localhost:7269", "http://127.0.0.1:7268");
        builder.Configuration["SgolApi:TrustedOrigin"] = "https://localhost:7269";
        builder.Configuration["Processes:OperationsEnabled"] = "true";
        builder.Logging.SetMinimumLevel(LogLevel.Error);
        builder.Services.AddRazorPages(o => o.Conventions.ConfigureFilter(new Microsoft.AspNetCore.Mvc.ServiceFilterAttribute(typeof(RoleScreenAccessFilter))))
            .AddApplicationPart(typeof(SgolDbContext).Assembly);
        builder.Services.AddScoped<RoleScreenAccessFilter>();
        builder.Services.AddHttpContextAccessor(); builder.Services.AddDataProtection();
        builder.Services.AddAntiforgery(o => { o.Cookie.Name="__Host-SGOL-CSRF";o.Cookie.SecurePolicy=CookieSecurePolicy.Always;o.Cookie.SameSite=SameSiteMode.Strict;o.HeaderName="X-CSRF-TOKEN"; });
        builder.Services.AddScoped<PersonalScheduleService>();
        builder.Services.AddSgolApiClient(builder.Configuration, builder.Environment);
        builder.Services.AddScoped<RazorAntiforgeryBridge>(); builder.Services.AddSingleton<AccessNotice>();
        builder.Services.AddDbContext<SgolDbContext>(o => o.UseNpgsql(db.Database.GetConnectionString()));
        builder.Services.AddScoped<Sgol.Web.Infrastructure.Persistence.Identity.IPersonAvatarReader, Sgol.Web.Infrastructure.Persistence.Identity.PersonAvatarService>();
        builder.Services.AddScoped<Sgol.Web.Infrastructure.Persistence.Identity.PersonAvatarService>();
        var clock = new Clock();

        builder.Services.AddSingleton<IClock>(clock); builder.Services.AddSingleton<IUuidGenerator, Uuid7Generator>();
        builder.Services.AddScoped<AuditTransaction>(); builder.Services.AddScoped<IDirectionPreparedPlanService, EfDirectionPreparedPlanService>();
        builder.Services.AddScoped<IDirectionPlanningCatalogReader, DirectionPlanningCatalogReader>();
        builder.Services.AddScoped<OperationalTaskService>(); builder.Services.AddScoped<OperationalProcessCatalogService>();
        builder.Services.AddScoped<OperationalSupervisionService>(); builder.Services.AddScoped<TaskBonusService>();
        builder.Services.AddScoped<OperationalCarryoverReader>();
        builder.Services.AddScoped<AssignmentRefreshService>();
        builder.Services.AddScoped<OperationalSchedulingPolicyService>();
        builder.Services.AddScoped<Sgol.Web.Infrastructure.Persistence.Organization.SpecializationService>();
        builder.Services.AddScoped<ProcessPlanningService>(); builder.Services.AddScoped<OperationalProcessService>();
        builder.Services.AddSingleton<IFileTechnicalValidator, FileTechnicalValidator>(); builder.Services.AddSingleton<IFileMalwareScanner, Scanner>();
        builder.Services.AddSingleton<IRazorSessionState>(new Session(actor, await db.AppUsers.Where(u => u.Id == actor).Select(u => u.PersonId).SingleAsync()));
        await using var app = builder.Build();
        app.UseMiddleware<Sgol.Web.Infrastructure.Http.CorrelationIdMiddleware>();
        app.Use(async (ctx, next) => { ctx.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actor.ToString())], "TEST_ONLY")); await next(); });
        app.UseStaticFiles(new StaticFileOptions { FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(webRoot) }); app.MapRazorPages(); app.MapDirectionPreparedPlanApi();
        await app.StartAsync();
        Console.WriteLine("RECURRENCE_UI_READY https://localhost:7269/tareas/procesos");
        await app.WaitForShutdownAsync();
    }
    private sealed class Clock:IClock{public DateTimeOffset UtcNow { get; set; }=DateTimeOffset.TryParse(Environment.GetEnvironmentVariable("SGOL_RECURRENCE_UI_DATE"),out var at) ? at : new(2026,11,23,14,0,0,TimeSpan.Zero);}
    private sealed class Scanner:IFileMalwareScanner{public Task CheckAvailabilityAsync(CancellationToken t)=>Task.CompletedTask;public Task<EvidenceScanOutcome> ScanAsync(Stream s,long n,CancellationToken t)=>Task.FromResult(new EvidenceScanOutcome(EvidenceScanResult.Limpio));}
    private sealed class Session(Guid user,Guid person):IRazorSessionState{
        public bool IsInvalid=>false; public void Invalidate(){}
        public Task<SessionSnapshot?> GetAsync(CancellationToken token=default)=>Task.FromResult<SessionSnapshot?>(new(user,person,"test","Dirección prueba","LOR-001",CanonicalRole.Direction,[WeekAuthorization.View],DateTimeOffset.UtcNow,DateTimeOffset.UtcNow.AddHours(1),DateTimeOffset.UtcNow.AddHours(2)));
    }
}
