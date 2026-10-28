using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.EntityFrameworkCore;
using Sgol.Identity.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Planning;
using Sgol.Web.Infrastructure.Persistence.Organization;
using Sgol.Web.Presentation.Navigation;

internal sealed class ImportCheckSession(Guid actor) : IRazorSessionState
{
    public bool IsInvalid => false;
    public void Invalidate() { }
    public Task<SessionSnapshot?> GetAsync(CancellationToken token=default) => Task.FromResult<SessionSnapshot?>(
        new(actor,Guid.Empty,"check","Check","LOR-001",CanonicalRole.Direction,[],DateTimeOffset.UtcNow,DateTimeOffset.UtcNow.AddHours(1),DateTimeOffset.UtcNow.AddHours(1)));
}

internal static class ImportFormChecks
{
    public static async Task RunAsync(IServiceProvider services,SgolDbContext db,Guid actor)
    {
        var connection=db.Database.GetConnectionString()!;
        if(!connection.Contains("sgol_task_checks_"))throw new Exception("Form checks require isolated database");
        async Task Check(string code,bool activate,bool accepted)
        {
            db.ChangeTracker.Clear();
            var input=await db.OperationalTasks.AsNoTracking().SingleAsync(t=>t.TaskCode==code);
            if(activate)input.Active=true;
            var page=new Sgol.Web.Pages.Tasks.IndexModel(new ImportCheckSession(actor),services.GetRequiredService<OperationalTaskService>(),
                services.GetRequiredService<SpecializationService>(),services.GetRequiredService<OperationalProcessCatalogService>(),services.GetRequiredService<OperationalSchedulingPolicyService>(),services.GetRequiredService<AssignmentRefreshService>()) {
                Input=input,RequiresSpecialization=input.SpecializationId!=null,
                InputDays=Enumerable.Range(0,7).Where(d=>(input.WeekDays&(1<<d))!=0).ToArray(),
                PageContext=new PageContext {HttpContext=new DefaultHttpContext {RequestServices=services}} };
            services.GetRequiredService<IObjectModelValidator>().Validate(page.PageContext,null,"Input",input);
            var result=await page.OnPostSaveAsync(default);
            if(accepted ? result is not RedirectToPageResult : page.Response.StatusCode!=422)
                throw new Exception($"Form {code} activate={activate}: {page.Response.StatusCode}, {page.Error}, {string.Join(';',page.ModelState.Where(s=>s.Value!.Errors.Count>0).Select(s=>s.Key))}");
            Console.WriteLine($"PASS Formulario {code}: {(accepted?"guarda parámetros":"rechaza activación incompleta")}");
        }
        await Check("TAR-0017",false,true);
        await Check("TAR-0017",true,false);
        await Check("TAR-0044",false,true);
        await Check("TAR-0001",false,true);
    }
}
