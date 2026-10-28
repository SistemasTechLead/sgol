using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using SkiaSharp;
using Sgol.Web.Infrastructure.Persistence.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sgol.Identity.Contracts;
using Sgol.Organization.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Identity;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Sgol.Web.Infrastructure.Persistence.Planning;
using Sgol.Planning.Contracts;

internal static class OperationalHttpChecks
{
    public static async Task RunAsync(IServiceProvider provider, Guid directorId, Browser director, Action<bool, string> check)
    {
        var workers = new List<(Guid User, Guid Person, string Login, string Password, Guid Assignment)>();
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
            var profiles = scope.ServiceProvider.GetRequiredService<UserProfileService>();
            var tasks = scope.ServiceProvider.GetRequiredService<OperationalTaskService>();
            const string payload = "SEC-XSS-<img src=x onerror=alert('sgol-security')>";
            await tasks.SaveAsync(directorId, new OperationalTask { Name = payload, Description = "<script>window.sgolSecurityXss=1</script>",
                Requirement = "COMENTARIO", AssignedRole = CanonicalRole.SalesFloor, LoadMinutes = 5, Revision = 0 }, default);
            var task = await db.OperationalTasks.SingleAsync(x => x.Name == payload);
            var now = DateTimeOffset.UtcNow;
            var day = WeekContract.LocalToday(now);
            var plan = DirectionPreparedPlan.ForOccasion(Guid.NewGuid(), BranchScope.LorettaId, directorId, now, "Security HTTP fixtures", day, day.AddDays(1));
            db.Add(plan); await db.SaveChangesAsync();
            for (var i = 0; i < 2; i++)
            {
                var login = "security.worker" + i;
                var created = await profiles.MutateAsync(directorId, new UserProfileInput { UserName = login, DisplayName = "Security worker " + i,
                    RoleCode = CanonicalRole.SalesFloor }, "save", Guid.NewGuid(), default);
                var user = await db.AppUsers.AsNoTracking().SingleAsync(x => x.Id == created.Id);
                var id = Guid.NewGuid();
                db.Add(new OperationalAssignment { Id = id, PlanId = plan.Id, TaskId = task.Id, TaskCode = task.TaskCode,
                    Name = task.Name, Description = task.Description, PersonId = user.PersonId, PersonName = "Security worker " + i,
                    AssignedRole = CanonicalRole.SalesFloor, Requirement = "COMENTARIO", LoadMinutes = 5,
                    StartsAt = now.AddMinutes(-10), DueAt = now.AddHours(1), OperationalCriticality = "FLEXIBLE" });
                await db.SaveChangesAsync();
                workers.Add((user.Id, user.PersonId, login, created.TemporaryPassword!, id));
            }
        }
        var browsers = new List<Browser>();
        try
        {
            foreach (var worker in workers)
            {
                var browser = new Browser("https://localhost:7287"); browsers.Add(browser);
                await browser.Post("/api/v1/auth/login", new { userName = worker.Login, password = worker.Password });
                var changed = await browser.Post("/api/v1/auth/password/change", new { currentPassword = worker.Password, newPassword = worker.Password + "Changed!" });
                check(changed.IsSuccessStatusCode, "D03 worker initial HTTP session");
            }
            for (var i = 0; i < 2; i++)
            {
                var own = workers[i]; var other = workers[1 - i]; var browser = browsers[i];
                var ownResponse = await browser.Client.GetAsync("/mi-trabajo?assignedTask=" + own.Assignment);
                var ownHtml = await ownResponse.Content.ReadAsStringAsync();
                check(ownResponse.IsSuccessStatusCode && ownHtml.Contains("SEC-XSS-") && !ownHtml.Contains("<img src=x onerror=alert('sgol-security')>"), "I03 stored task payload encoded in real Razor");
                var foreign = await browser.Client.GetAsync("/mi-trabajo?assignedTask=" + other.Assignment);
                check((int)foreign.StatusCode == 404, "I02 same-role foreign assignment GET denied");
                var photos = await browser.Client.GetAsync("/mi-trabajo/fotografias/" + other.Assignment);
                check((int)photos.StatusCode is 403 or 404, "I02 foreign evidence gallery denied");
                var denied = await browser.Form("/mi-trabajo?handler=CompleteOperational", new() { ["id"] = other.Assignment.ToString(), ["comment"] = "Foreign write must fail" });
                check((int)denied.StatusCode == 404, "I02 foreign assignment POST denied");
                var completed = await browser.Form("/mi-trabajo?handler=CompleteOperational", new() { ["id"] = own.Assignment.ToString(), ["comment"] = "Isolated HTTP delivery" });
                check((int)completed.StatusCode == 302, "D03 legitimate HTTP delivery commits with runtime role");
            }
            await using var verify = provider.CreateAsyncScope();
            var db = verify.ServiceProvider.GetRequiredService<SgolDbContext>();
            foreach (var worker in workers)
            {
                var submission = await db.OperationalAssignments.AsNoTracking().SingleAsync(x => x.Id == worker.Assignment);
                check(submission.CompletedBy == worker.User, "I02 rejected foreign request did not acquire delivery authorship");
                var revision = (await db.OperationalAssignments.AsNoTracking().SingleAsync(x => x.Id == worker.Assignment)).Revision;
                await verify.ServiceProvider.GetRequiredService<OperationalSupervisionService>().ReviewAsync(directorId, worker.Assignment, revision, "CONCLUIDA", "Isolated security validation", default);
            }
            check(await db.AuditEvents.AnyAsync(x => x.Action.Contains("REVIEW")), "D03 review audit persists with runtime grants");
            using var bitmap = new SKBitmap(16, 16); bitmap.Erase(SKColors.LightBlue);
            using var image = SKImage.FromBitmap(bitmap); using var png = image.Encode(SKEncodedImageFormat.Png, 100);
            var content = png.ToArray(); var digest = Convert.ToHexStringLower(SHA256.HashData(content));
            var photoId = Guid.NewGuid();
            db.OperationalAttachments.Add(new() { AssignmentId = workers[0].Assignment, FileName = "isolated.png", ContentType = "image/png", Content = content, CreatedAt = DateTimeOffset.UtcNow });
            db.OperationalPhotographs.Add(new() { Id = photoId, AssignmentId = workers[1].Assignment, FileName = "isolated.png", Content = content,
                ContentType = "image/png", ReceivedBytes = content.Length, ReceivedSha256 = digest, Sha256 = digest, Position = 0, CreatedAt = DateTimeOffset.UtcNow, AttachedAt = DateTimeOffset.UtcNow });
            var foreignTemplate = new ReportTemplate { Id = Guid.NewGuid(), UserId = workers[0].User, Name = "Private template", Configuration = "{}", UpdatedAt = DateTimeOffset.UtcNow };
            db.ReportTemplates.Add(foreignTemplate); await db.SaveChangesAsync();
            var planId = (await db.OperationalAssignments.AsNoTracking().SingleAsync(x => x.Id == workers[0].Assignment)).PlanId;
            check((await browsers[0].Client.GetAsync("/mi-trabajo?handler=OperationalFile&id=" + workers[0].Assignment)).IsSuccessStatusCode, "I02 owner can download historical attachment");
            check((int)(await browsers[1].Client.GetAsync("/mi-trabajo?handler=OperationalFile&id=" + workers[0].Assignment)).StatusCode is 403 or 404, "I02 foreign attachment bytes denied");
            var photoPath = "/mi-trabajo?handler=OperationalFile&id=" + workers[1].Assignment + "&photoId=" + photoId;
            check((await browsers[1].Client.GetAsync(photoPath)).IsSuccessStatusCode, "I02 owner can download historical photograph");
            check((int)(await browsers[0].Client.GetAsync(photoPath)).StatusCode is 403 or 404, "I02 foreign photograph bytes denied");
            check((int)(await browsers[0].Client.GetAsync("/api/v1/direction-plans/" + planId)).StatusCode == 403, "I02 floor role cannot read a real Direction plan");
            check((int)(await browsers[0].Client.GetAsync("/tareas/procesos?id=" + OperationalSequence.GeneralId)).StatusCode == 403, "I02 floor role cannot open process catalogue by ID");
            check((int)(await browsers[0].Form("/validaciones?handler=Review", new() { ["id"] = workers[1].Assignment.ToString(), ["decision"] = "NO_SATISFECHA", ["revision"] = "1" })).StatusCode == 403, "I02 floor role cannot forge review POST");
            check((int)(await director.Form("/reportes?handler=Save", new() { ["TemplateId"] = foreignTemplate.Id.ToString(), ["TemplateName"] = "Forbidden change", ["Configuration"] = "{}" })).StatusCode == 404, "I02 Director cannot edit another user's personal report template");
            check((int)(await director.Form("/reportes?handler=Delete", new() { ["TemplateId"] = foreignTemplate.Id.ToString() })).StatusCode == 404, "I02 Director cannot delete another user's personal report template");
            db.ChangeTracker.Clear();
            check((await db.ReportTemplates.SingleAsync(x => x.Id == foreignTemplate.Id)).Name == "Private template", "I02 denied template writes leave original data intact");
            var export = await director.Client.GetAsync("/reportes?handler=Export&F.Task=%27%20OR%201%3D1--");
            check(export.IsSuccessStatusCode && JsonDocument.Parse(await export.Content.ReadAsStringAsync()).RootElement.GetProperty("rows").GetArrayLength() == 0, "I01 real report export treats SQL payload as data and returns no unrelated rows");
        }
        finally { foreach (var browser in browsers) browser.Dispose(); }
    }
}
