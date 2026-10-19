using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sgol.Identity.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Planning;
using Sgol.Web.Infrastructure.Persistence.Organization;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
var connection = builder.Configuration.GetConnectionString("Sgol") ?? throw new Exception("Falta conexión");
var settings = new NpgsqlConnectionStringBuilder(connection);
if (!builder.Environment.IsDevelopment() || settings.Host != "localhost" ||
    settings.Database != "sgol_local" && !settings.Database!.StartsWith("sgol_task_checks_")) throw new Exception("Solo base local Development");
builder.Services.AddSgolPersistence(builder.Configuration);
builder.Services.AddRazorPages();
await using var app = builder.Build();
await using var scope = app.Services.CreateAsyncScope();
var db = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
if (args.Contains("--migrate")) { await db.Database.MigrateAsync(); Console.WriteLine("Migración aplicada"); return; }
var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, WriteIndented = true };
string Option(string key) => args[Array.IndexOf(args, key) + 1];
var source = Path.GetFullPath(Option("--batch"));
var rows = JsonDocument.Parse(await File.ReadAllTextAsync(source)).RootElement.EnumerateArray().ToArray();
var batch = rows.Select(r => r.Deserialize<OperationalTask>(jsonOptions)!).ToArray();
var specialties = await db.Set<Specialization>().AsNoTracking().ToArrayAsync();
for (var i=0;i<batch.Length;i++)
{
    var name = rows[i].GetProperty("SpecializationName").GetString()!;
    batch[i].SpecializationId = OperationalTaskService.NormalizeImportName(name) == "NO REQUIERE" ? null :
        specialties.Single(s => OperationalTaskService.NormalizeImportName(s.Name) == OperationalTaskService.NormalizeImportName(name) && s.Active).Id;
}
var now = DateTimeOffset.UtcNow;
var actor = await db.RoleAssignmentVersions.Where(r => r.RoleCode == CanonicalRole.Direction && r.Status == RoleAssignmentStatus.Active &&
    r.ValidFrom <= now && (r.ValidTo == null || r.ValidTo > now)).Select(r => r.UserId).SingleAsync();
var service = scope.ServiceProvider.GetRequiredService<OperationalTaskService>();
if (args.Contains("--verify-form")) { await ImportFormChecks.RunAsync(scope.ServiceProvider,db,actor); return; }
if (args.Contains("--verify-boundaries"))
{
    async Task Reject(Func<Task> action,string code)
    {
        try { await action(); } catch (Sgol.Planning.Contracts.DirectionPlanException e) when (e.Code==code) { db.ChangeTracker.Clear(); Console.WriteLine("PASS "+code);return; }
        throw new Exception("Expected "+code);
    }
    var deleted=await db.OperationalTasks.AsNoTracking().SingleAsync(t=>t.TaskCode=="TAR-0225");
    var collision=new OperationalTask {TaskCode=deleted.TaskCode,Name="Colisión con eliminado"};
    var p=await service.PreviewImportAsync(actor,[collision],default);
    if(p.Additions.Length!=0||p.Exclusions.Length!=1)throw new Exception("Deleted collision admitted");
    Console.WriteLine("PASS Colisión con eliminado excluida");
    var original=await db.OperationalTasks.AsNoTracking().SingleAsync(t=>t.TaskCode=="TAR-0021");
    var alias=new OperationalTask {TaskCode="TAR-9009",Name="  "+original.Name.ToUpperInvariant().Replace(" ","  ")+"  "};
    p=await service.PreviewImportAsync(actor,[alias],default);
    if(p.Additions.Length!=0)throw new Exception("Normalized collision admitted");
    if(OperationalTaskService.NormalizeImportName("  Árbol   ÚNICO  ")!="ARBOL UNICO")throw new Exception("Accent normalization failed");
    Console.WriteLine("PASS Nombre con acentos, mayúsculas y espacios normalizado");
    await Reject(()=>service.PreviewImportAsync(actor,[new(){TaskCode="TAR-9008",Name="Duplicada"},new(){TaskCode="TAR-9008",Name="Otra"}],default),"TASK_IMPORT_DUPLICATE");
    await Reject(()=>service.ImportAsync(actor,[new(){TaskCode="TAR-9008",Name="No escribir"}],"stale",new string('a',64),default),"TASK_IMPORT_STALE");
    foreach(var code in new[]{"TAR-0017","TAR-0020","TAR-0035"})
    {
        var t=await db.OperationalTasks.AsNoTracking().SingleAsync(t=>t.TaskCode==code);
        if(t.GoalUnits!=null||t.ProgressUnit!=""||t.Active||!t.DifferentPeople)throw new Exception("Incomplete progress altered");
        await Reject(()=>service.ChangeAsync(actor,t.Id,t.Revision,false,default),"TASK_PARAMETERS_INVALID");
        t.Active=true;
        await Reject(()=>service.SaveAsync(actor,t,default),"TASK_PARAMETERS_INVALID");
    }
    // Exercise the actual database constraint, independently of service validation.
    foreach(var code in new[]{"TAR-0017","TAR-0020","TAR-0035"})
    {
        await using var tx=await db.Database.BeginTransactionAsync();
        try { await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE operational_task SET \"Active\" = true WHERE \"TaskCode\" = {code}");throw new Exception("Database accepted incomplete progress"); }
        catch(PostgresException e) when(e.SqlState=="23514") { Console.WriteLine("PASS Restricción BD progreso incompleto "+code); }
        await tx.RollbackAsync();
    }
    Console.WriteLine("Import boundary checks passed");return;
}
var preview = await service.PreviewImportAsync(actor,batch,default);
if (args.Contains("--apply"))
{
    var expected = JsonSerializer.Deserialize<TaskImportPreview>(await File.ReadAllTextAsync(Option("--preview")),jsonOptions)!;
    var sourceHash = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Path.GetDirectoryName(source)!,"fuente.json"))).RootElement.GetProperty("sha256").GetString()!;
    var actualHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(Path.GetDirectoryName(source)!,"origen.xlsx"))));
    if (!string.Equals(actualHash,sourceHash,StringComparison.OrdinalIgnoreCase)) throw new Exception("El origen cambió");
    var imported = await service.ImportAsync(actor,batch,expected.Fingerprint,sourceHash,default);
    await File.WriteAllTextAsync(Option("--report"),JsonSerializer.Serialize(new { Imported=imported, Count=imported.Length, preview.Exclusions, sourceHash, At=now },jsonOptions));
    Console.WriteLine($"Importadas {imported.Length}; excluidas {preview.Exclusions.Length}");
}
else
{
    await File.WriteAllTextAsync(Option("--report"),JsonSerializer.Serialize(preview,jsonOptions));
    Console.WriteLine($"Vista previa: {preview.Additions.Length} altas ({preview.Additions.Count(t=>t.Active)} activas, {preview.Additions.Count(t=>!t.Active)} inactivas); {preview.Exclusions.Length} exclusiones");
}
