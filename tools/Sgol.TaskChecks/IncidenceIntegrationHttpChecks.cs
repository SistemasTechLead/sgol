using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SkiaSharp;
using Sgol.Evidence.Contracts;
using Sgol.BuildingBlocks.Time;
using Sgol.Identity.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Authentication;
using Sgol.Web.Infrastructure.Evidence;
using Sgol.Web.Infrastructure.Http;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Sgol.Web.Infrastructure.Persistence.Planning;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.Authentication;
using Sgol.Web.Presentation.Endpoints;
using Sgol.Web.Presentation.Navigation;

internal static partial class IncidenceChecks
{
    const string IntegrationOrigin="https://localhost:7296";
    static string HtmlField(string html,string name)=>WebUtility.HtmlDecode(Regex.Match(html,"name=\""+Regex.Escape(name)+"\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);
    static string IntegrationTotp(string text)
    {
        const string alphabet="ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";var bytes=new List<byte>();int buffer=0,bits=0;
        foreach(var c in text){buffer=(buffer<<5)|alphabet.IndexOf(c);bits+=5;if(bits>=8){bits-=8;bytes.Add((byte)(buffer>>bits));}}
        var counter=BitConverter.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeSeconds()/30);if(BitConverter.IsLittleEndian)Array.Reverse(counter);
        var hash=HMACSHA1.HashData(bytes.ToArray(),counter);var o=hash[^1]&15;var value=((hash[o]&127)<<24)|(hash[o+1]<<16)|(hash[o+2]<<8)|hash[o+3];return(value%1000000).ToString("D6");
    }
    public static async Task IntegrationHttpChecks(SgolDbContext db, Clock clock)
    {
        var root=new DirectoryInfo(AppContext.BaseDirectory);while(!File.Exists(Path.Combine(root.FullName,"SistemaSGOL.slnx")))root=root.Parent!;
        Directory.SetCurrentDirectory(root.FullName);var output=Path.Combine(root.FullName,".local/incidence-integration-20261104");Directory.CreateDirectory(output);
        clock.UtcNow=DateTimeOffset.UtcNow;
        var f=await QaFixtures.CreateAsync(db);var logins=new Dictionary<Guid,string>();var password="Isolated-"+Guid.NewGuid()+"!";
        foreach(var id in new[]{f.Director,f.Worker,f.Other})
        {
            var user=await db.AppUsers.SingleAsync(u=>u.Id==id);var login="inc-"+id.ToString("N");logins[id]=login;
            db.IdentityCredentials.Add(new(){UserId=id,UserName=login,NormalizedUserName=login.ToUpperInvariant(),PasswordHash=new PasswordHasher<AppUser>().HashPassword(user,password)});
        }
        foreach(var schedule in await db.Set<PersonalScheduleVersion>().Where(s=>s.PersonId==f.Person || s.PersonId==f.OtherPerson).ToArrayAsync())
        {
            schedule.Superseded=true;
            db.Add(new PersonalScheduleVersion{Id=Guid.NewGuid(),PersonId=schedule.PersonId,Revision=2,From=new(2026,11,4),ChangedBy=f.Director,ChangedAt=clock.UtcNow,
                IntentKey=Guid.NewGuid(),RequestHash="http-fixture",DaysJson=JsonSerializer.Serialize(Enumerable.Range(0,7).ToDictionary(d=>d,d=>new[]{new WorkInterval(0,1440)}))});
        }
        await db.SaveChangesAsync();db.ChangeTracker.Clear();
        var photo=await Row(db,f,clock,clock.UtcNow.AddMinutes(-5),10);
        var retiredPhoto=await Row(db,f,clock,clock.UtcNow.AddMinutes(-5),10);
        var document=await Row(db,f,clock,clock.UtcNow.AddMinutes(-5),10);
        foreach(var row in await db.OperationalAssignments.Where(a=>new[]{photo.Id,retiredPhoto.Id,document.Id}.Contains(a.Id)).ToArrayAsync())
        {row.Requirement=row.Id==document.Id?"EXCEL":"FOTOGRAFIA";row.Name="Integración "+row.Requirement+" "+row.Id;row.DueAt=clock.UtcNow.AddHours(2);row.Revision++;}
        await db.SaveChangesAsync();db.ChangeTracker.Clear();
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions{ApplicationName=typeof(SgolDbContext).Assembly.FullName,ContentRootPath=Path.Combine(root.FullName,"src/Sgol.Web"),WebRootPath=Path.Combine(root.FullName,"src/Sgol.Web/wwwroot"),EnvironmentName="Development"});
        builder.WebHost.UseUrls(IntegrationOrigin);
        builder.Configuration["ConnectionStrings:Sgol"]=db.Database.GetConnectionString();builder.Configuration["SgolApi:TrustedOrigin"]=IntegrationOrigin;
        builder.Configuration["ReverseProxy:KnownProxy"]="";builder.Configuration["LocalShared:SecurityHeaders"]="false";
        builder.Configuration["Authentication:RequireMfa"]="true";builder.Configuration["Processes:OperationsEnabled"]="true";
        // Operational task files use PostgreSQL, never the legacy object storage adapter.
        builder.Configuration["Evidence:Scanner:Host"]="127.0.0.1";builder.Configuration["Evidence:Scanner:Port"]="3310";
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Services.AddRazorPages(o=>o.Conventions.ConfigureFilter(new Microsoft.AspNetCore.Mvc.ServiceFilterAttribute(typeof(RoleScreenAccessFilter)))).AddApplicationPart(typeof(SgolDbContext).Assembly);
        builder.Services.AddHttpContextAccessor();builder.Services.AddScoped<IRazorSessionState,RazorSessionState>();builder.Services.AddScoped<RoleScreenAccessFilter>();
        builder.Services.AddScoped<RazorAntiforgeryBridge>();builder.Services.AddSingleton<SafeReturnDestination>();builder.Services.AddSingleton<AccessNotice>();
        builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(output,"private-keys")));
        builder.Services.AddSgolApiClient(builder.Configuration,builder.Environment);builder.Services.AddSgolHttpPrimitives();builder.Services.AddSgolHostedAuthentication();
        builder.Services.AddSgolEvidenceInfrastructure(builder.Configuration,builder.Environment);
        // Exercise the real scanner for PostgreSQL operational media without configuring an unrelated S3 bucket.
        builder.Services.RemoveAll<IFileMalwareScanner>();builder.Services.AddSingleton<IFileMalwareScanner,ClamAvScanner>();
        builder.Services.AddAntiforgery(o=>{o.HeaderName="X-CSRF-TOKEN";o.Cookie.Name="__Host-SGOL-CSRF";o.Cookie.SecurePolicy=CookieSecurePolicy.Always;o.Cookie.HttpOnly=true;o.Cookie.SameSite=SameSiteMode.Strict;o.Cookie.Path="/";});
        builder.Services.AddSgolPersistence(builder.Configuration);builder.Services.RemoveAll<IClock>();builder.Services.AddSingleton<IClock>(clock);
        await using var app=builder.Build();app.UseSgolHttpPrimitives();app.UseStaticFiles();app.UseRateLimiter();app.UseAuthentication();
        app.UseMiddleware<ScreenAccessMiddleware>();app.UseMiddleware<AntiforgeryValidationMiddleware>();app.UseAuthorization();
        app.MapRazorPages();app.MapAuthenticationApi();app.MapDirectionPreparedPlanApi();await app.StartAsync();
        var jars=new Dictionary<HttpClient,CookieContainer>();
        HttpClient Client(){var jar=new CookieContainer();var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false,CookieContainer=jar}){BaseAddress=new(IntegrationOrigin),Timeout=TimeSpan.FromSeconds(90)};jars[client]=jar;return client;}
        async Task<JsonElement> Api(HttpClient c,string path,object data)
        {
            var csrf=await c.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf");using var req=new HttpRequestMessage(HttpMethod.Post,path){Content=JsonContent.Create(data)};
            req.Headers.Add("X-CSRF-TOKEN",csrf.GetProperty("data").GetProperty("requestToken").GetString());using var response=await c.SendAsync(req);
            Check(response.IsSuccessStatusCode,"integration real authentication "+path+" status="+(int)response.StatusCode);
            return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("data").Clone();
        }
        async Task Login(HttpClient c,Guid id)
        {
            await Api(c,"/api/v1/auth/login",new{userName=logins[id],password});var enroll=await Api(c,"/api/v1/auth/mfa/enroll",new{});
            await Api(c,"/api/v1/auth/mfa/confirm",new{totpCode=IntegrationTotp(enroll.GetProperty("manualKey").GetString()!)});
            Check((await c.GetAsync("/api/v1/auth/session")).StatusCode==HttpStatusCode.OK,"integration persisted authenticated session with real MFA");
        }
        using var director=Client();using var worker=Client();using var receiver=Client();await Login(director,f.Director);await Login(worker,f.Worker);await Login(receiver,f.Other);
        async Task<string> Token(HttpClient c,string path="/mi-trabajo")=>HtmlField(await c.GetStringAsync(path),"__RequestVerificationToken");
        async Task<HttpResponseMessage> Form(HttpClient c,string path,Dictionary<string,string> form,string tokenPath="/mi-trabajo")
        {form["__RequestVerificationToken"]=await Token(c,tokenPath);return await c.PostAsync(path,new FormUrlEncodedContent(form));}
        using var bitmap=new SKBitmap(32,32);bitmap.Erase(SKColors.Blue);using var jpeg=bitmap.Encode(SKEncodedImageFormat.Jpeg,90);var photoBytes=jpeg.ToArray();
        async Task<(Guid Session,Guid Photo)> Upload(HttpClient c,Guid assignment)
        {
            using var begin=await Form(c,"/mi-trabajo?handler=BeginCamera",new(){["id"]=assignment.ToString()});begin.EnsureSuccessStatusCode();
            var session=JsonDocument.Parse(await begin.Content.ReadAsStringAsync()).RootElement.GetProperty("captureSessionId").GetGuid();var id=Guid.NewGuid();
            using var body=new MultipartFormDataContent();body.Add(new StringContent(await Token(c)),"__RequestVerificationToken");body.Add(new StringContent(assignment.ToString()),"id");body.Add(new StringContent(session.ToString()),"captureSessionId");body.Add(new StringContent(id.ToString()),"photographId");
            var file=new ByteArrayContent(photoBytes);file.Headers.ContentType=new("image/jpeg");body.Add(file,"photo","capture.jpg");using var response=await c.PostAsync("/mi-trabajo?handler=UploadCamera",body);
            if(!response.IsSuccessStatusCode) await File.WriteAllTextAsync(Path.Combine(output,"upload-error.txt"),await response.Content.ReadAsStringAsync());
            Check(response.IsSuccessStatusCode,"integration real camera upload, normalization and ClamAV status="+(int)response.StatusCode);return(session,id);
        }
        var originalPhoto=await Upload(worker,photo.Id);var originalRetired=await Upload(worker,retiredPhoto.Id);
        var day=WeekContract.LocalToday(clock.UtcNow);
        var form=new Dictionary<string,string>{["Day"]=day.ToString("yyyy-MM-dd"),["PersonId"]=f.Person.ToString(),["Key"]=Guid.NewGuid().ToString(),["Reason"]="Ausencia integrada con evidencia real"};
        var ids=new[]{photo.Id,retiredPhoto.Id,document.Id};
        // MVC indexed form binding, including an explicitly discarded overload on RETIRE.
        for(var i=0;i<ids.Length;i++){form[$"Decisions[{i}].AssignmentId"]=ids[i].ToString();form[$"Decisions[{i}].Action"]=i==1?"RETIRE":"TRANSFER";form[$"Decisions[{i}].RecipientId"]=f.OtherPerson.ToString();form[$"Decisions[{i}].AuthorizeOverload"]="true";}
        using var review=await Form(director,"/supervision/incidencias?handler=Preview",form,"/supervision/incidencias");var html=await review.Content.ReadAsStringAsync();
        await File.WriteAllTextAsync(Path.Combine(output,"integration-preview.html"),html);
        Check(review.IsSuccessStatusCode && html.Contains("data-incidence-confirm"),"integration real form reviews photos, retirement and document transfer");
        form["Fingerprint"]=HtmlField(html,"Fingerprint");form["NotBefore"]=HtmlField(html,"NotBefore");
        using var confirmed=await Form(director,"/supervision/incidencias?handler=Apply",form,"/supervision/incidencias");
        Check(confirmed.StatusCode==HttpStatusCode.Redirect,"integration real form commits absence and mixed evidence dispositions");
        var receiptPath=confirmed.Headers.Location!.ToString();html=await director.GetStringAsync(receiptPath);await File.WriteAllTextAsync(Path.Combine(output,"integration-receipt.html"),html);
        Check(html.Contains("Ausencia confirmada"),"integration real receipt renders");
        // Keep the authenticated browser fixture private; account credentials never leave this process.
        db.ChangeTracker.Clear();var receipt=await db.Set<OperationalIncidence>().SingleAsync(i=>i.PersonId==f.Person && i.Day==day);
        var evidencePath=$"/supervision/incidencias?handler=Evidence&Day={day:yyyy-MM-dd}&receipt={receipt.Id}&assignmentId={retiredPhoto.Id}&photoId={originalRetired.Photo}";
        using var preserved=await director.GetAsync(evidencePath);
        Check(preserved.IsSuccessStatusCode && preserved.Content.Headers.ContentType?.MediaType=="image/jpeg","integration retired staged photo downloadable from authorized receipt");
        var downloaded=await preserved.Content.ReadAsByteArrayAsync();var storedPhoto=await db.OperationalPhotographs.SingleAsync(p=>p.Id==originalRetired.Photo);
        Check(downloaded.SequenceEqual(storedPhoto.Content),"integration retired photo download matches verified stored bytes");
        Check((await worker.GetAsync(evidencePath)).StatusCode==HttpStatusCode.Forbidden,"integration employee cannot use supervisory receipt evidence endpoint");
        Check((await director.GetAsync(evidencePath.Replace(receipt.Id.ToString(),Guid.NewGuid().ToString()))).StatusCode==HttpStatusCode.Forbidden,"integration forged receipt denied");
        Check((await director.GetAsync(evidencePath.Replace(retiredPhoto.Id.ToString(),document.Id.ToString()))).StatusCode==HttpStatusCode.NotFound,"integration photo cannot be borrowed from another receipt assignment");
        Check((await director.GetAsync(evidencePath.Replace(retiredPhoto.Id.ToString(),Guid.NewGuid().ToString()))).StatusCode==HttpStatusCode.Forbidden,"integration unrelated assignment denied");
        using(var replay=await Form(director,"/supervision/incidencias?handler=Apply",form,"/supervision/incidencias"))
            Check(replay.StatusCode==HttpStatusCode.Redirect && replay.Headers.Location!.ToString()==receiptPath,"integration real form replay after discarded response returns the same receipt");
        await IntegrationCarryoverHttp(db,clock,f,director,receiver,Form,Token);
        clock.UtcNow=DateTimeOffset.Parse(form["NotBefore"]).AddSeconds(1);
        var ownPhoto=await Upload(receiver,photo.Id);
        using var delivered=await Form(receiver,"/mi-trabajo?handler=CompleteOperational",new(){["id"]=photo.Id.ToString(),["captureSessionId"]=ownPhoto.Session.ToString(),["photographIds"]=JsonSerializer.Serialize(new[]{ownPhoto.Photo}),["conclusion"]="Entrega de receptora con autoría propia"});
        Check(delivered.StatusCode==HttpStatusCode.Redirect,"integration receiver completes transferred camera task through real form");
        var filePath=$"/mi-trabajo?handler=OperationalFile&id={photo.Id}&photoId={ownPhoto.Photo}";
        using var read=await receiver.GetAsync(filePath);Check(read.IsSuccessStatusCode && read.Content.Headers.ContentType?.MediaType=="image/jpeg","integration receiver downloads delivered photo");
        Check((await worker.GetAsync(filePath)).StatusCode==HttpStatusCode.Forbidden,"integration old owner cannot read recipient's later delivery");
        var laterPath=evidencePath.Replace(retiredPhoto.Id.ToString(),photo.Id.ToString()).Replace(originalRetired.Photo.ToString(),ownPhoto.Photo.ToString());
        Check((await director.GetAsync(laterPath)).StatusCode==HttpStatusCode.NotFound,"integration receipt excludes recipient media created after confirmation");
        var workbook=PerformanceMediaChecks.Workbook(1,rows:2);
        using(var body=new MultipartFormDataContent()) {
            body.Add(new StringContent(await Token(receiver)),"__RequestVerificationToken");body.Add(new StringContent(document.Id.ToString()),"id");body.Add(new StringContent("Documento real de destinataria"),"conclusion");
            var bytes=new ByteArrayContent(workbook);bytes.Headers.ContentType=new("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");body.Add(bytes,"file","integracion.xlsx");
            using var sent=await receiver.PostAsync("/mi-trabajo?handler=CompleteOperational",body);
            if(!sent.IsSuccessStatusCode && sent.StatusCode!=HttpStatusCode.Redirect) await File.WriteAllTextAsync(Path.Combine(output,"document-error.html"),await sent.Content.ReadAsStringAsync());
            Check(sent.StatusCode==HttpStatusCode.Redirect,"integration actual XLSX multipart delivery after transfer and ClamAV");
        }
        using(var downloadedFile=await receiver.GetAsync($"/mi-trabajo?handler=OperationalFile&id={document.Id}")) {
            var content=await downloadedFile.Content.ReadAsByteArrayAsync();
            Check(downloadedFile.IsSuccessStatusCode && workbook.SequenceEqual(content),"integration transferred document download preserves exact bytes");
        }
        Check((await worker.GetAsync($"/mi-trabajo?handler=OperationalFile&id={document.Id}")).StatusCode==HttpStatusCode.Forbidden,"integration previous owner cannot download later document");
        foreach(var path in new[]{"/mi-trabajo","/validaciones","/planificacion","/supervision/bono-tareas","/reportes"})
        {using var response=await (path=="/mi-trabajo"?receiver:director).GetAsync(path);Check(response.IsSuccessStatusCode,"integration authenticated consumer "+path+" status="+(int)response.StatusCode);}
        if(Environment.GetEnvironmentVariable("SGOL_INCIDENCE_BROWSER")=="1") {
            var done=Path.Combine(output,"browser.done");
            // A successful prior run must not satisfy this host's browser barrier.
            if(File.Exists(done)) File.Delete(done);
            var cookies=jars[director].GetAllCookies().Cast<Cookie>().Select(c=>new{name=c.Name,value=c.Value,domain=c.Domain,path=c.Path,httpOnly=c.HttpOnly,secure=c.Secure,sameSite="Strict"});
            await File.WriteAllTextAsync(Path.Combine(output,"browser-private.json"),JsonSerializer.Serialize(new{cookies,origin=IntegrationOrigin,receiptPath,person=f.Person,day=day.AddDays(1).ToString("yyyy-MM-dd")}));
            Console.WriteLine("INTEGRATION_BROWSER_READY");
            var deadline=DateTimeOffset.UtcNow.AddMinutes(8);
            while(!File.Exists(done) && DateTimeOffset.UtcNow<deadline)await Task.Delay(500);
            Check(File.Exists(done),"integration browser completed while real authenticated host remained live");
        }
        await app.StopAsync();
    }
    static async Task IntegrationCarryoverHttp(SgolDbContext db,Clock clock,QaFixtures.Actors f,HttpClient director,HttpClient receiver,
        Func<HttpClient,string,Dictionary<string,string>,string,Task<HttpResponseMessage>> form,Func<HttpClient,string,Task<string>> token)
    {
        var owner=f with { Person=f.OtherPerson,Worker=f.Other };
        var overdue=await Row(db,owner,clock,clock.UtcNow.AddMinutes(-45),10);
        const string path="/mi-trabajo/arrastre";
        foreach(var kind in new[]{"POSPONER","INCIDENTE"}) {
            if(kind=="INCIDENTE") await Row(db,owner,clock,clock.UtcNow.AddMinutes(-5),10);
            var intent=Guid.NewGuid();
            var fields=kind=="POSPONER"?new Dictionary<string,string>{["Selected"]=overdue.Id.ToString(),["PostponeCause"]="FALTA_TIEMPO_OPERATIVO",["PostponeExplanation"]="Petición real de integración",["PostponeIntent"]=intent.ToString()}:
                new Dictionary<string,string>{["Cause"]="FALTA_TIEMPO_OPERATIVO",["Explanation"]="Incidente real de integración",["Intent"]=intent.ToString(),["Minutes"]="15",["End"]=TimeZoneInfo.ConvertTime(clock.UtcNow,OperationalSchedule.Zone).DateTime.ToString("yyyy-MM-ddTHH:mm:ss")};
            var suffix=kind=="POSPONER"?"Postponement":"";var fingerprint=kind=="POSPONER"?"PostponeFingerprint":"Fingerprint";
            using(var preview=await form(receiver,path+"?handler=Preview"+suffix,fields,path)) {
                var html=await preview.Content.ReadAsStringAsync();fields[fingerprint]=HtmlField(html,fingerprint);
                if(fields[fingerprint].Length==0)await File.WriteAllTextAsync(Path.Combine(".local/incidence-integration-20261104",kind+"-error.html"),html);
                Check(preview.IsSuccessStatusCode && fields[fingerprint].Length>0,"integration actual carryover preview "+kind);
            }
            using(var submitted=await form(receiver,path+"?handler=Submit"+suffix,fields,path))
                Check(submitted.StatusCode==HttpStatusCode.Redirect,"integration actual carryover submit "+kind);
            db.ChangeTracker.Clear();var request=await db.Set<OperationalCarryoverRequest>().SingleAsync(r=>r.PersonId==f.OtherPerson && r.Kind==kind);
            Check(request.State=="PENDIENTE","integration pending carryover persisted "+kind);
            var absence=new Dictionary<string,string>{["PersonId"]=f.OtherPerson.ToString(),["Day"]=WeekContract.LocalToday(clock.UtcNow).ToString("yyyy-MM-dd"),["Key"]=Guid.NewGuid().ToString(),["Reason"]="Ausencia con petición pendiente"};
            using(var blocked=await form(director,"/supervision/incidencias?handler=Preview",absence,"/supervision/incidencias")) {
                var html=await blocked.Content.ReadAsStringAsync();
                Check(!html.Contains("data-incidence-confirm") && html.Contains("pendiente",StringComparison.OrdinalIgnoreCase),"integration incidence real form blocks pending "+kind);
            }
            var original=request.SnapshotJson;
            if(kind=="POSPONER") {
                var current=await db.OperationalAssignments.AsNoTracking().SingleAsync(a=>a.Id==overdue.Id);
                var decision=new Dictionary<string,string>{["ItemDecision.RequestId"]=request.Id.ToString(),["ItemDecision.AssignmentId"]=overdue.Id.ToString(),["ItemDecision.Revision"]=current.Revision.ToString(),["ItemDecision.Action"]="REJECT",["ItemDecision.Comment"]="Resolver petición antes de revisar ausencia",["ItemDecision.Intent"]=Guid.NewGuid().ToString()};
                using(var reviewed=await form(director,"/validaciones?handler=CarryoverItem",decision,"/validaciones")) decision["ItemFingerprint"]=HtmlField(await reviewed.Content.ReadAsStringAsync(),"ItemFingerprint");
                Check(decision["ItemFingerprint"].Length>0,"integration real postponement decision preview");decision["confirm"]="true";
                using var resolved=await form(director,"/validaciones?handler=CarryoverItem",decision,"/validaciones");Check(resolved.IsSuccessStatusCode,"integration real postponement decision confirm");
            } else {
                using var resolved=await form(director,"/validaciones?handler=Carryover",new(){["requestId"]=request.Id.ToString(),["requestRevision"]=request.Revision.ToString(),["approve"]="false",["decisionComment"]="Resolver antes de revisar ausencia"},"/validaciones");Check(resolved.IsSuccessStatusCode,"integration real incident decision confirm");
            }
            db.ChangeTracker.Clear();var saved=await db.Set<OperationalCarryoverRequest>().SingleAsync(r=>r.Id==request.Id);
            Check(saved.State==(kind=="POSPONER"?"RESUELTA":"RECHAZADA") && saved.SnapshotJson==original && saved.PendingItems().Length==0,"integration carryover resolved without rewriting original history "+kind);
        }
    }
}
