using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SkiaSharp;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Authentication;
using Sgol.Web.Infrastructure.Evidence;
using Sgol.Web.Infrastructure.Http;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Sgol.Web.Infrastructure.Persistence.Configuration;
using Sgol.Web.Infrastructure.Persistence.Planning;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.Authentication;
using Sgol.Web.Presentation.Endpoints;
using Sgol.Web.Presentation.Navigation;

/// <summary>Diagnostic workloads only. All persistence is guarded to a dedicated local container.</summary>
public static class PerformanceLoadChecks
{
    const string Origin="https://localhost:7267";
    static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web){WriteIndented=true};
    static readonly List<object> Evidence=[];
    static string output="";
    sealed record Reply(int Status,long Bytes,string? Error=null);
    sealed record Attempt(int Index,double Ms,int Status,long Bytes,string? Error);
    sealed record Account(Guid User,Guid Person,string Login);
    static DbContextOptions<SgolDbContext> options=null!;
    static string connection="";
    internal static string ArtifactDirectory(string name)
    {
        var root=new DirectoryInfo(AppContext.BaseDirectory);
        while(!File.Exists(Path.Combine(root.FullName,"SistemaSGOL.slnx")))root=root.Parent??throw new Exception("Source root not found");
        return Path.Combine(root.FullName,"artifacts",name);
    }

    public static async Task RunAsync(bool previewOnly=false)
    {
        connection=Environment.GetEnvironmentVariable("SGOL_TASK_CHECKS_CONNECTION")??throw new Exception("Missing dedicated connection");
        var settings=new NpgsqlConnectionStringBuilder(connection);
        if(settings.Host!="localhost"||settings.Port!=5547||settings.Database!="sgol_task_checks_performance")
            throw new Exception("Requires localhost:5547/sgol_task_checks_performance, never the shared simulation");
        var secret=Environment.GetEnvironmentVariable("SGOL_PERF_SECRET")??throw new Exception("Set ephemeral test secret");
        var root=new DirectoryInfo(AppContext.BaseDirectory);
        while(!File.Exists(Path.Combine(root.FullName,"SistemaSGOL.slnx")))root=root.Parent??throw new Exception("Root not found");
        output=Path.Combine(root.FullName,"artifacts","performance-20261028",DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss"));Directory.CreateDirectory(output);
        // Published dedicated PostgreSQL port is IPv4-only; cancellation opens a new socket.
        settings.Host = "127.0.0.1"; connection = settings.ConnectionString;
        options=new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(connection,n=>n.CommandTimeout(15)).Options;
        await using(var db=new SgolDbContext(options)) {
            await db.Database.MigrateAsync();
            if(!previewOnly&&await db.People.AnyAsync())throw new Exception("Fixture requires an empty dedicated DB; preserve results and use a new DB/container volume for reruns");
        }
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions{ApplicationName=typeof(SgolDbContext).Assembly.FullName,ContentRootPath=Path.Combine(root.FullName,"src","Sgol.Web"),WebRootPath=Path.Combine(root.FullName,"src","Sgol.Web","wwwroot"),EnvironmentName="Development"});
        builder.WebHost.UseUrls(Origin);
        // Remove inherited transport overrides: this host exposes only its dedicated loopback URL.
        builder.Configuration["ConnectionStrings:Sgol"]=connection;
        builder.Configuration["SgolApi:TrustedOrigin"]=Origin;
        builder.Configuration["ReverseProxy:KnownProxy"]="";builder.Configuration["LocalShared:SecurityHeaders"]="false";
        builder.Configuration["Authentication:RequireMfa"]="true";
        builder.Configuration["Processes:OperationsEnabled"]="true";
        builder.Configuration["Evidence:Storage:Endpoint"]="http://localhost:8347";
        builder.Configuration["Evidence:Storage:Region"]="us-east-1";
        builder.Configuration["Evidence:Storage:QuarantineBucket"]="sgol-perf-quarantine";
        builder.Configuration["Evidence:Storage:CleanBucket"]="sgol-perf-clean";
        builder.Configuration["Evidence:Storage:AccessKey"]="sgol-perf";
        builder.Configuration["Evidence:Storage:SecretKey"]=secret;
        builder.Configuration["Evidence:Storage:AllowedUploadOrigins:0"]=Origin;
        builder.Configuration["Evidence:Storage:AllowInsecureTransport"]="true";
        builder.Configuration["Evidence:Scanner:Host"]="127.0.0.1";builder.Configuration["Evidence:Scanner:Port"]="3347";
        builder.Logging.ClearProviders();builder.Logging.AddConsole();builder.Logging.SetMinimumLevel(LogLevel.Critical);
        builder.Services.AddRazorPages(o=>o.Conventions.ConfigureFilter(new Microsoft.AspNetCore.Mvc.ServiceFilterAttribute(typeof(RoleScreenAccessFilter)))).AddApplicationPart(typeof(SgolDbContext).Assembly);
        builder.Services.AddHttpContextAccessor();builder.Services.AddScoped<IRazorSessionState,RazorSessionState>();builder.Services.AddScoped<RoleScreenAccessFilter>();
        builder.Services.AddScoped<RazorAntiforgeryBridge>();builder.Services.AddSingleton<SafeReturnDestination>();builder.Services.AddSingleton<AccessNotice>();
        builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(output,"private-keys")));
        builder.Services.AddSgolApiClient(builder.Configuration,builder.Environment);builder.Services.AddSgolHttpPrimitives();builder.Services.AddSgolHostedAuthentication();
        builder.Services.AddSgolEvidenceInfrastructure(builder.Configuration,builder.Environment);
        builder.Services.AddAntiforgery(o=>{o.HeaderName="X-CSRF-TOKEN";o.Cookie.Name="__Host-SGOL-CSRF";o.Cookie.SecurePolicy=CookieSecurePolicy.Always;o.Cookie.HttpOnly=true;o.Cookie.SameSite=SameSiteMode.Strict;o.Cookie.Path="/";});
        builder.Services.AddSgolPersistence(builder.Configuration);
        await using var app=builder.Build();app.UseSgolHttpPrimitives();app.UseStaticFiles();app.UseRateLimiter();app.UseMiddleware<Sgol.Web.Infrastructure.Http.PerformanceAdmissionMiddleware>();app.UseAuthentication();
        app.UseMiddleware<ScreenAccessMiddleware>();app.UseMiddleware<AntiforgeryValidationMiddleware>();app.UseAuthorization();
        app.MapRazorPages();app.MapAuthenticationApi();app.MapDirectionPreparedPlanApi();app.MapGet("/health/live",()=>Results.Ok(new{status="alive"}));
        await app.StartAsync();
        if(previewOnly){Console.WriteLine("Isolated browser preview listening on "+Origin+" for 300 seconds; existing synthetic fixture retained");try{await Task.Delay(TimeSpan.FromSeconds(int.TryParse(Environment.GetEnvironmentVariable("SGOL_PERF_PREVIEW_SECONDS"),out var previewSeconds)?Math.Clamp(previewSeconds,60,900):300));}finally{await app.StopAsync();}return;}
        var stages = new ConcurrentDictionary<string, ConcurrentBag<double>>();
        var activeWork = new ConcurrentDictionary<string, long>();
        using var metrics = new System.Diagnostics.Metrics.MeterListener();
        metrics.InstrumentPublished = (instrument, listener) => { if (instrument.Meter.Name == "Sgol.Performance") listener.EnableMeasurementEvents(instrument); };
        metrics.SetMeasurementEventCallback<double>((instrument, value, tags, state) => {
            var stage = "unknown"; foreach (var tag in tags) if (tag.Key == "stage") stage = tag.Value?.ToString() ?? stage;
            var samples = stages.GetOrAdd(stage, _ => new()); if (samples.Count < 10000) samples.Add(value);
        });
        metrics.SetMeasurementEventCallback<long>((instrument, value, tags, state) => {
            if (instrument.Name != "sgol.work.active") return;
            var operation = "unknown"; foreach (var tag in tags) if (tag.Key == "operation") operation = tag.Value?.ToString() ?? operation;
            activeWork.AddOrUpdate(operation, value, (_, previous) => previous + value);
        });
        metrics.Start();
        var process=Process.GetCurrentProcess();long peakWorking=0,peakManaged=0;using var sampleStop=new CancellationTokenSource();
        var sampler=Task.Run(async()=>{while(!sampleStop.IsCancellationRequested){process.Refresh();peakWorking=Math.Max(peakWorking,process.WorkingSet64);peakManaged=Math.Max(peakManaged,GC.GetTotalMemory(false));try{await Task.Delay(100,sampleStop.Token);}catch(OperationCanceledException){break;}}});
        try {
            Account director,worker;
            await using(var db=new SgolDbContext(options)){director=await AddAccount(db,"perf-director",CanonicalRole.Direction,secret);worker=await AddAccount(db,"perf-worker",CanonicalRole.SalesFloor,secret);}
            using var directorClient=Client();using var workerClient=Client();
            await Authenticate(directorClient,director.Login,secret);await Authenticate(workerClient,worker.Login,secret);
            var directorCsrf=await Csrf(directorClient);var workerCsrf=await Csrf(workerClient);
            Check((await directorClient.GetAsync("/tareas")).StatusCode==HttpStatusCode.OK,"Real direction session and Razor permission");
            await TaskImports(app,director);
            foreach(var c in new[]{1,8,24})await Phase($"task-form-create-c{c}",40,c,i=>Form(directorClient,"/tareas?handler=Save",new(){["__RequestVerificationToken"]=directorCsrf,["Input.Name"]=$"HTTP workload {c}-{i}",["Input.Revision"]="0",["Input.OperationalCriticality"]="FLEXIBLE"}));
            await using(var db=new SgolDbContext(options))Check(await db.OperationalTasks.CountAsync(t=>t.Name.StartsWith("HTTP workload"))==120,"All 120 form creations persisted exactly once");
            var assignmentIds=await SeedAssignments(director,worker,160);
            foreach(var c in new[]{1,8,24})await Phase($"task-delivery-c{c}",40,c,i=>Form(workerClient,"/mi-trabajo?handler=CompleteOperational",new(){["__RequestVerificationToken"]=workerCsrf,["id"]=assignmentIds[(c==1?0:c==8?40:80)+i].ToString(),["conclusion"]="Synthetic delivery"}));
            await using(var db=new SgolDbContext(options))Check(await db.OperationalAssignments.CountAsync(a=>a.CompletedAt!=null)==120,"Concurrent deliveries persisted; no duplicate or lost task completion");
            await Photographs(app,workerClient,workerCsrf,worker,director);
            await BlockedDelivery(workerClient,workerCsrf,assignmentIds[121],2000);
            await BlockedDelivery(workerClient,workerCsrf,assignmentIds[122],17000);
            foreach(var size in new[]{100,1000,3000}){
                await SeedHistory(director,worker,size);
                await Phase($"reports-history-{size}",3,1,_=>Get(directorClient,"/reportes"));
                await Phase($"validation-history-{size}",3,1,_=>Get(directorClient,"/validaciones"));
            }
            await Phase("mixed-reads-history3000",24,8,i=>Get(i%2==0?directorClient:workerClient,i%3==0?"/reportes":i%3==1?"/mi-trabajo":"/tablero"));
            await CancelReports(directorClient);
            Check(activeWork.GetValueOrDefault("report") == 0, "Cancelled report HTTP pipelines completed on server after 1200ms");
            await LoginStorm();
            // Full-page and browser probes can run against this still-isolated host if requested.
            if(Environment.GetEnvironmentVariable("SGOL_PERF_HOLD_SECONDS") is {} hold&&int.TryParse(hold,out var seconds))await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(seconds,0,600)));
            await PerformanceMediaChecks.RunAsync(output);
            await PerformanceScannerChecks.RunAsync(output);
            await PerformanceScannerChecks.RunHostComparisonAsync(output);
            await using(var db=new SgolDbContext(options)){
                Evidence.Add(new { serverStages = stages.ToDictionary(p => p.Key, p => { var values = p.Value.Order().ToArray(); return new { count = values.Length, p50Ms = values[values.Length / 2], p95Ms = values[Math.Min(values.Length - 1, (int)Math.Ceiling(values.Length * .95) - 1)], maxMs = values[^1] }; }), activeWork });
                Evidence.Add(new{integrity=new{catalogue=await db.OperationalTasks.CountAsync(),assignments=await db.OperationalAssignments.CountAsync(),photos=await db.Set<OperationalPhotograph>().CountAsync(),duplicateTaskCodes=await db.OperationalTasks.GroupBy(t=>t.TaskCode).CountAsync(g=>g.Count()>1),duplicatePhotoIds=await db.Set<OperationalPhotograph>().CountAsync()-await db.Set<OperationalPhotograph>().Select(p=>p.Id).Distinct().CountAsync()},scope="synthetic dedicated database only"});
            }
            await Phase("recovered-health",5,1,_=>Get(workerClient,"/health/live"));
        } finally {
            sampleStop.Cancel();await sampler;Evidence.Add(new{resources=new{peakWorkingSetSampledBytes=peakWorking,peakManagedSampledBytes=peakManaged,samplingMs=100,scope="integration host plus in-process load client; excludes dependency containers and native/GPU memory of browsers"}});Save();
            await app.StopAsync();Console.WriteLine("Performance evidence: "+output);
        }
    }

    static HttpClient Client()=>new(new HttpClientHandler{AllowAutoRedirect=false,CookieContainer=new(),ServerCertificateCustomValidationCallback=(_,_,_,_)=>true}){BaseAddress=new(Origin),Timeout=TimeSpan.FromSeconds(50)};
    static async Task<string> Csrf(HttpClient client){using var r=await client.GetAsync("/api/v1/auth/csrf");r.EnsureSuccessStatusCode();var j=JsonDocument.Parse(await r.Content.ReadAsStringAsync());return j.RootElement.GetProperty("data").GetProperty("requestToken").GetString()!;}
    static async Task<JsonElement> Api(HttpClient client,string path,object data){using var req=new HttpRequestMessage(HttpMethod.Post,path){Content=JsonContent.Create(data)};req.Headers.Add("X-CSRF-TOKEN",await Csrf(client));using var r=await client.SendAsync(req);if(!r.IsSuccessStatusCode)throw new Exception($"API setup failed {path}: {(int)r.StatusCode}");return JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.GetProperty("data").Clone();}
    static async Task Authenticate(HttpClient client,string user,string password){await Api(client,"/api/v1/auth/login",new{userName=user,password});var enrolled=await Api(client,"/api/v1/auth/mfa/enroll",new{});var secret=enrolled.GetProperty("manualKey").GetString()!;await Api(client,"/api/v1/auth/mfa/confirm",new{totpCode=Totp(secret)});Check((await client.GetAsync("/api/v1/auth/session")).StatusCode==HttpStatusCode.OK,"Password + real MFA enrollment + session completed");}
    static string Totp(string text){const string alphabet="ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";var bytes=new List<byte>();int buffer=0,bits=0;foreach(var c in text){buffer=(buffer<<5)|alphabet.IndexOf(c);bits+=5;if(bits>=8){bits-=8;bytes.Add((byte)(buffer>>bits));}}var counter=BitConverter.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeSeconds()/30);if(BitConverter.IsLittleEndian)Array.Reverse(counter);var hash=HMACSHA1.HashData(bytes.ToArray(),counter);var o=hash[^1]&15;var value=((hash[o]&127)<<24)|(hash[o+1]<<16)|(hash[o+2]<<8)|hash[o+3];return(value%1000000).ToString("D6");}
    static async Task<Account> AddAccount(SgolDbContext db,string login,string role,string password){var now=DateTimeOffset.UtcNow;var a=new Account(Guid.CreateVersion7(),Guid.CreateVersion7(),login);var user=new AppUser{Id=a.User,PersonId=a.Person,Status=AccountStatus.Active,SecurityStamp=Guid.NewGuid().ToString()};db.People.Add(new(){Id=a.Person,StableCode=login,DisplayName=login,CreatedAt=now.AddYears(-1)});db.AppUsers.Add(user);db.IdentityCredentials.Add(new(){UserId=a.User,UserName=login,NormalizedUserName=login.ToUpperInvariant(),PasswordHash=new PasswordHasher<AppUser>().HashPassword(user,password)});db.EmploymentVersions.Add(new(Guid.CreateVersion7(),a.Person,BranchScope.LorettaId,EmploymentStatus.Active,now.AddYears(-1)));db.RoleAssignmentVersions.Add(new(){Id=Guid.CreateVersion7(),UserId=a.User,BranchId=BranchScope.LorettaId,RoleCode=role,Status=RoleAssignmentStatus.Active,ValidFrom=now.AddYears(-1)});await db.SaveChangesAsync();return a;}
    static async Task<Reply> Form(HttpClient c,string path,Dictionary<string,string> fields){using var body=new FormUrlEncodedContent(fields);using var r=await c.PostAsync(path,body);return new((int)r.StatusCode,(await r.Content.ReadAsByteArrayAsync()).Length);}
    static async Task<Reply> Get(HttpClient c,string path){using var r=await c.GetAsync(path);return new((int)r.StatusCode,(await r.Content.ReadAsByteArrayAsync()).Length);}
    static async Task Phase(string name,int count,int concurrency,Func<int,Task<Reply>> action){var samples=new ConcurrentBag<Attempt>();var cpu=Process.GetCurrentProcess().TotalProcessorTime;var allocated=GC.GetTotalAllocatedBytes();var clock=Stopwatch.StartNew();await Parallel.ForEachAsync(Enumerable.Range(0,count),new ParallelOptions{MaxDegreeOfParallelism=concurrency},async(i,ct)=>{var sw=Stopwatch.StartNew();try{var r=await action(i);samples.Add(new(i,sw.Elapsed.TotalMilliseconds,r.Status,r.Bytes,r.Error));}catch(Exception e){samples.Add(new(i,sw.Elapsed.TotalMilliseconds,0,0,e.GetType().Name));}});var sorted=samples.OrderBy(s=>s.Ms).ToArray();double P(double p)=>sorted[Math.Min(sorted.Length-1,(int)Math.Ceiling(sorted.Length*p)-1)].Ms;var phase=new{name,count,concurrency,elapsedMs=clock.Elapsed.TotalMilliseconds,requestsPerSecond=count/clock.Elapsed.TotalSeconds,p50Ms=P(.5),p95Ms=P(.95),p99Ms=P(.99),cpuMs=(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalMilliseconds,allocatedBytes=GC.GetTotalAllocatedBytes()-allocated,statuses=samples.GroupBy(s=>s.Status).ToDictionary(g=>g.Key.ToString(),g=>g.Count()),attempts=samples.OrderBy(s=>s.Index).ToArray()};Evidence.Add(phase);Save();Console.WriteLine($"PERF {name}: n={count} c={concurrency} p95={P(.95):F1}ms statuses={JsonSerializer.Serialize(phase.statuses)}");}
    static void Check(bool ok,string text){Evidence.Add(new{check=text,passed=ok});Save();if(!ok)throw new Exception("Performance fixture assertion: "+text);Console.WriteLine("PASS "+text);}
    static void Save()=>File.WriteAllText(Path.Combine(output,"load-results.json"),JsonSerializer.Serialize(Evidence,Json));

    static async Task TaskImports(WebApplication app,Account director){
        var batch=Enumerable.Range(5000,1000).Select(i=>new OperationalTask{TaskCode=$"TAR-{i}",Name=$"Imported performance {i}",OperationalCriticality="FLEXIBLE"}).ToArray();
        using var scope=app.Services.CreateScope();var service=scope.ServiceProvider.GetRequiredService<OperationalTaskService>();
        var preview=await service.PreviewImportAsync(director.User,batch,default);
        await Phase("task-import-1000-service",1,1,async _=>{var added=await service.ImportAsync(director.User,batch,preview.Fingerprint,new string('a',64),default);return new(added.Length==1000?200:500,added.Length);});
        // Two identical signed previews race: one commits, one must report stale; neither may duplicate definitions.
        var next=Enumerable.Range(7000,50).Select(i=>new OperationalTask{TaskCode=$"TAR-{i}",Name=$"Racing performance {i}",OperationalCriticality="FLEXIBLE"}).ToArray();
        preview=await service.PreviewImportAsync(director.User,next,default);
        await Phase("task-import-identical-race-service",2,2,async _=>{using var s=app.Services.CreateScope();try{var added=await s.ServiceProvider.GetRequiredService<OperationalTaskService>().ImportAsync(director.User,next,preview.Fingerprint,new string('b',64),default);return new(200,added.Length);}catch(Sgol.Planning.Contracts.DirectionPlanException e){return new(e.Status,0,e.Code);}});
        await using var db=new SgolDbContext(options);Check(await db.OperationalTasks.CountAsync(t=>t.Name.StartsWith("Imported performance"))==1000&&await db.OperationalTasks.CountAsync(t=>t.Name.StartsWith("Racing performance"))==50,"Batch imports preserved exactly 1050 unique definitions; stale retry did not duplicate");
    }
    static async Task<Guid[]> SeedAssignments(Account director,Account worker,int count,string requirement="NINGUNO"){
        await using var db=new SgolDbContext(options);var now=DateTimeOffset.UtcNow;var day=DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now,OperationalSchedule.Zone).DateTime);var plan=DirectionPreparedPlan.ForOccasion(Guid.CreateVersion7(),BranchScope.LorettaId,director.User,now,"Synthetic performance",day.AddDays(-7),day.AddDays(7));db.DirectionPreparedPlans.Add(plan);var task=await db.OperationalTasks.FirstAsync();var list=Enumerable.Range(0,count).Select(i=>new OperationalAssignment{Id=Guid.CreateVersion7(),PlanId=plan.Id,TaskId=task.Id,TaskCode=task.TaskCode,Name="Synthetic performance assignment "+i,PersonId=worker.Person,PersonName=worker.Login,AssignedRole=CanonicalRole.SalesFloor,Requirement=requirement,LoadMinutes=1,StartsAt=now.AddMinutes(-30),DueAt=now.AddHours(2),OperationalCriticality="FLEXIBLE",GoalUnits=0,MaximumPhotos=20,MinimumPhotos=1}).ToArray();db.OperationalAssignments.AddRange(list);await db.SaveChangesAsync();return list.Select(a=>a.Id).ToArray();
    }
    static byte[] Photograph(){using var bitmap=new SKBitmap(1600,1200);var pixels=new SKColor[1600*1200];var random=new Random(42);for(int i=0;i<pixels.Length;i++)pixels[i]=new((byte)random.Next(256),(byte)random.Next(256),(byte)random.Next(256));bitmap.Pixels=pixels;using var data=bitmap.Encode(SKEncodedImageFormat.Jpeg,85);return data.ToArray();}
    static async Task Photographs(WebApplication app,HttpClient client,string csrf,Account worker,Account director){
        using(var scope=app.Services.CreateScope()){var scan=scope.ServiceProvider.GetRequiredService<Sgol.Evidence.Contracts.IFileMalwareScanner>();await scan.CheckAvailabilityAsync(default);}
        var ids=await SeedAssignments(director,worker,4,"FOTOGRAFIA");var sessions=new List<Guid>();
        foreach(var id in ids){using var r=await client.PostAsync("/mi-trabajo?handler=BeginCamera",new FormUrlEncodedContent(new Dictionary<string,string>{["__RequestVerificationToken"]=csrf,["id"]=id.ToString()}));r.EnsureSuccessStatusCode();sessions.Add(JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.GetProperty("captureSessionId").GetGuid());}
        var photo=Photograph();var stored=new ConcurrentBag<(Guid Assignment,Guid Session,Guid Photo)>();var deferred=new ConcurrentBag<(Guid Assignment,Guid Session,Guid Photo)>();
        for(int phase=0;phase<3;phase++){var index=phase;var c=new[]{1,4,12}[phase];await Phase($"photo-upload-double-clamav-c{c}",16,c,async i=>{var photoId=Guid.NewGuid();var result=await Upload(ids[index],sessions[index],photoId);if(result.Status==200)stored.Add((ids[index],sessions[index],photoId));else if(result.Status==503)deferred.Add((ids[index],sessions[index],photoId));return result;});}
        async Task<Reply> Upload(Guid assignment,Guid session,Guid id){using var content=new MultipartFormDataContent();content.Add(new StringContent(csrf),"__RequestVerificationToken");content.Add(new StringContent(assignment.ToString()),"id");content.Add(new StringContent(session.ToString()),"captureSessionId");content.Add(new StringContent(id.ToString()),"photographId");var file=new ByteArrayContent(photo);file.Headers.ContentType=new("image/jpeg");content.Add(file,"photo","capture.jpg");using var r=await client.PostAsync("/mi-trabajo?handler=UploadCamera",content);return new((int)r.StatusCode,(await r.Content.ReadAsByteArrayAsync()).Length);}
        var retry = deferred.ToArray();
        if(retry.Length>0)await Phase("photo-explicit-retry-after-admission",retry.Length,1,async i=>{var item=retry[i];var result=await Upload(item.Assignment,item.Session,item.Photo);if(result.Status==200)stored.Add(item);return result;});
        var first=stored.First();await Phase("photo-identical-id-retry",6,6,_=>Upload(first.Assignment,first.Session,first.Photo));
        await using(var db=new SgolDbContext(options))Check(await db.Set<OperationalPhotograph>().CountAsync()==48,"48 verified photos persisted; six identical retries did not duplicate");
        var limitIds=Enumerable.Range(0,21).Select(_=>Guid.NewGuid()).ToArray();var limitDeferred=new ConcurrentBag<Guid>();
        await Phase("photo-session-limit-21",21,8,async i=>{var result=await Upload(ids[3],sessions[3],limitIds[i]);if(result.Status==503)limitDeferred.Add(limitIds[i]);return result;});
        var limitRetry=limitDeferred.ToArray();if(limitRetry.Length>0)await Phase("photo-limit-explicit-retry-after-admission",limitRetry.Length,1,i=>Upload(ids[3],sessions[3],limitRetry[i]));
        await using(var db=new SgolDbContext(options))Check(await db.OperationalPhotographs.CountAsync(p=>p.CaptureSessionId==sessions[3])==20,"Saturation retries retain 20-photo session limit");
        var selected=stored.Where(x=>x.Assignment==ids[0]).ToArray();await Phase("photo-complete-16-then-retry",2,1,_=>Form(client,"/mi-trabajo?handler=CompleteOperational",new(){["__RequestVerificationToken"]=csrf,["id"]=ids[0].ToString(),["captureSessionId"]=sessions[0].ToString(),["photographIds"]=JsonSerializer.Serialize(selected.Select(x=>x.Photo)),["conclusion"]="Synthetic photographed delivery"}));
        await using(var db=new SgolDbContext(options))Check(await db.Set<OperationalPhotograph>().CountAsync(p=>p.AttachedAt!=null)==16,"Verified photos attach exactly once to delivered assignment");
    }
    static async Task BlockedDelivery(HttpClient client,string csrf,Guid id,int holdMs)
    {
        await using var blocker=new NpgsqlConnection(connection);await blocker.OpenAsync();await using var tx=await blocker.BeginTransactionAsync();
        await using(var cmd=new NpgsqlCommand("SELECT pg_advisory_xact_lock(734651001)",blocker,tx))await cmd.ExecuteNonQueryAsync();
        Reply? reply=null;var watch=Stopwatch.StartNew();
        var pending=Phase($"delivery-shared-lock-held-{holdMs}ms",1,1,async _=>reply=await Form(client,"/mi-trabajo?handler=CompleteOperational",new(){["__RequestVerificationToken"]=csrf,["id"]=id.ToString(),["conclusion"]="Synthetic lock probe"}));
        await Task.Delay(holdMs);await tx.RollbackAsync();await pending;
        if(holdMs>5000){
            Check(reply?.Status==503,"Long lock returns bounded busy rejection");
            await using(var db=new SgolDbContext(options))Check(!await db.OperationalAssignments.Where(a=>a.Id==id).Select(a=>a.CompletedAt!=null).SingleAsync(),"Busy delivery leaves no partial completion");
            await Phase("delivery-explicit-retry-after-lock",1,1,_=>Form(client,"/mi-trabajo?handler=CompleteOperational",new(){["__RequestVerificationToken"]=csrf,["id"]=id.ToString(),["conclusion"]="Synthetic lock probe"}));
            await using(var db=new SgolDbContext(options))Check(await db.OperationalAssignments.Where(a=>a.Id==id).Select(a=>a.CompletedAt!=null).SingleAsync(),"Delivery succeeds after lock release");
        }else Check(reply?.Status==302,"Short lock preserves successful delivery");
    }
    static async Task SeedHistory(Account director,Account worker,int target){await using var db=new SgolDbContext(options);var existing=await db.OperationalAssignments.CountAsync(a=>a.Name.StartsWith("Synthetic history"));if(existing>=target)return;var ids=await SeedAssignments(director,worker,target-existing);var rows=await db.OperationalAssignments.Where(a=>ids.Contains(a.Id)).ToArrayAsync();foreach(var a in rows)a.Name="Synthetic history "+a.Id;for(int i=0;i<target/10-existing/10;i++){var a=rows[i%rows.Length];var item=new CarryoverItem(a.Id,worker.Person,a.PlanId,1,a.Name,a.AssignedRole,a.StartsAt,a.DueAt,a.DueAt.AddMinutes(30),true,Guid.NewGuid(),null,"","{}",worker.Login);db.Add(new OperationalCarryoverRequest{Id=Guid.CreateVersion7(),BranchId=BranchScope.LorettaId,PersonId=worker.Person,RequestedBy=director.User,IntentKey=Guid.NewGuid(),Cause="CAUSA_NO_IDENTIFICADA",CauseDescription="Synthetic performance",Explanation="Synthetic performance",IncidentStart=a.StartsAt,IncidentEnd=a.StartsAt.AddMinutes(30),Minutes=30,SubmittedAt=DateTimeOffset.UtcNow,SnapshotJson=JsonSerializer.Serialize(new[]{item}),Fingerprint=new string('c',64),ScheduleFingerprint=new string('d',64)});}await db.SaveChangesAsync();}
    static async Task CancelReports(HttpClient client){await Phase("reports-client-cancel-50ms",4,4,async _=>{using var cancel=new CancellationTokenSource(50);try{using var r=await client.GetAsync("/reportes",cancel.Token);return new((int)r.StatusCode,0);}catch(OperationCanceledException){return new(499,0,"client-cancelled; server completion must be checked separately");}});await Task.Delay(1200);await using var c=new NpgsqlConnection(connection);await c.OpenAsync();await using var cmd=new NpgsqlCommand("SELECT count(*) FROM pg_stat_activity WHERE datname=current_database() AND pid<>pg_backend_pid() AND state='active'",c);Evidence.Add(new{check="active SQL after client cancellation + 1200ms",active=Convert.ToInt32(await cmd.ExecuteScalarAsync()),note="Zero active SQL does not prove CPU work stopped; server-side phase instrumentation is still required"});Save();}
    static async Task LoginStorm(){var clients=Enumerable.Range(0,40).Select(_=>Client()).ToArray();try{var csrf=await Task.WhenAll(clients.Select(Csrf));await Phase("login-api-single-ip-burst",40,24,async i=>{using var req=new HttpRequestMessage(HttpMethod.Post,"/api/v1/auth/login"){Content=JsonContent.Create(new{userName="synthetic-unknown-"+i,password="Synthetic invalid password 2026!"})};req.Headers.Add("X-CSRF-TOKEN",csrf[i]);using var r=await clients[i].SendAsync(req);return new((int)r.StatusCode,(await r.Content.ReadAsByteArrayAsync()).Length);});using var razor=Client();var html=await razor.GetStringAsync("/acceso");var token=WebUtility.HtmlDecode(Regex.Match(html,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);await Phase("login-razor-after-api-quota",20,12,i=>Form(razor,"/acceso",new(){["__RequestVerificationToken"]=token,["stepAction"]="login",["userName"]="synthetic-other-"+i,["password"]="Synthetic invalid password 2026!"}));}finally{foreach(var c in clients)c.Dispose();}}
}
