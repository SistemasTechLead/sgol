using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sgol.Identity.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Planning;

public static class ProcessSearchChecks
{
    public static async Task RunAsync(SgolDbContext db, HttpClient client)
    {
        var root=await db.Set<OperationalSequence>().SingleAsync(p=>p.Name=="Ítems: prueba aislada");
        db.AddRange(new OperationalTask{Id=Guid.CreateVersion7(),TaskCode="TAR-9881",Name="Revisár producto de prueba",ProcessId=OperationalSequence.GeneralId,AssignedRole=CanonicalRole.Direction,Requirement="COMENTARIO",LoadMinutes=10,OperationalCriticality="FLEXIBLE"},
            new OperationalTask{Id=Guid.CreateVersion7(),TaskCode="TAR-9882",Name="Archivar producto de prueba",ProcessId=OperationalSequence.GeneralId,AssignedRole=CanonicalRole.Direction,Requirement="COMENTARIO",LoadMinutes=10,OperationalCriticality="FLEXIBLE"});
        await db.SaveChangesAsync();db.ChangeTracker.Clear();
        async Task<string> Snapshot()=>JsonSerializer.Serialize(new{Process=await db.Set<OperationalSequence>().AsNoTracking().SingleAsync(p=>p.Id==root.Id),
            Steps=await db.Set<OperationalSequenceStep>().AsNoTracking().OrderBy(s=>s.VersionId).ThenBy(s=>s.Position).ToArrayAsync(),Assignments=await db.OperationalAssignments.AsNoTracking().OrderBy(a=>a.Id).ToArrayAsync()});
        var before=await Snapshot();
        foreach(var pair in new[]{("revisar","TAR-9881","TAR-9882"),("tar-9882","TAR-9882","TAR-9881"),("ninguna coincidencia","No se encontraron tareas con esa búsqueda.","TAR-9881")}) {
            var response=await client.GetAsync($"/tareas/procesos?id={root.Id}&Search={Uri.EscapeDataString(pair.Item1)}");
            var html=await response.Content.ReadAsStringAsync();
            Check(response.StatusCode==HttpStatusCode.OK&&html.Contains("data-process-search-results")&&html.Contains(pair.Item2)&&!html.Contains(pair.Item3),"Real HTTP search filters name/code/empty with reusable Razor results: "+pair.Item1);
        }
        Check(before==await Snapshot(),"Searching changes no process rules, revision, assignments or reservations");
        Console.WriteLine($"PROCESS_SEARCH_BROWSER_URL /tareas/procesos?id={root.Id}");
        Console.WriteLine("PROCESS_SEARCH_HTTP_PASS");
    }
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS: "+message);}
}
