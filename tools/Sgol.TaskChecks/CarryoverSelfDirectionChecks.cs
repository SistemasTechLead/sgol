using System.Text.Json;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Sgol.Identity.Contracts;
using Sgol.Planning.Contracts;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Planning;

internal static class CarryoverSelfDirectionChecks
{
    private sealed class FailApproval : Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor
    {
        public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int>> SavingChangesAsync(
            Microsoft.EntityFrameworkCore.Diagnostics.DbContextEventData data, Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (data.Context!.ChangeTracker.Entries<OperationalCarryoverRequest>().Any(e => e.Entity.State == "APROBADA"))
                throw new InvalidOperationException("Injected approval persistence failure");
            return ValueTask.FromResult(result);
        }
    }
    private static async Task RollbackCheck(SgolDbContext db, QaFixtures.Actors fixture, QaFixtures.Actors own,
        CorrectionSchedulingChecks.Clock clock, OperationalCarryoverService service, OperationalAssignment row)
    {
        var preview = await service.PreviewPostponementAsync(fixture.Director, [row.Id], "ERROR_DE_ASIGNACION", "QA rollback", default);
        Check(preview.Conflicts.Length == 0, "rollback fixture has valid preview");
        var before = JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().SingleAsync(a => a.Id == row.Id));
        var audits = await db.AuditEvents.CountAsync(); var intent = Guid.NewGuid();
        await using (var isolated = new SgolDbContext(new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(db.Database.GetConnectionString()).AddInterceptors(new FailApproval()).Options)) {
            var failing = new OperationalCarryoverService(isolated, clock, CorrectionSchedulingChecks.Service(isolated, clock));
            try { await failing.SubmitPostponementAsync(fixture.Director, [row.Id], "ERROR_DE_ASIGNACION", "QA rollback", preview.Fingerprint, intent, default); throw new Exception("failure not injected"); }
            catch (InvalidOperationException e) when (e.Message == "Injected approval persistence failure") { }
        }
        Check(!await db.Set<OperationalCarryoverRequest>().AnyAsync(r => r.IntentKey == intent) && audits == await db.AuditEvents.CountAsync(), "approval failure rolls back request and both audits");
        Check(before == JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().SingleAsync(a => a.Id == row.Id)), "approval failure preserves assignment");
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS self direction: " + message); }
    public static async Task Run(SgolDbContext db)
    {
        var fixture = await QaFixtures.CreateAsync(db);
        var clock = new CorrectionSchedulingChecks.Clock { UtcNow = TemporalEligibilityContext.At(new(2026,11,5), 810) };
        var service = new OperationalCarryoverService(db, clock, CorrectionSchedulingChecks.Service(db, clock));
        var person = await db.AppUsers.Where(u => u.Id == fixture.Director).Select(u => u.PersonId).SingleAsync();
        var own = fixture with { Worker = fixture.Director, Person = person };
        async Task<OperationalAssignment> Source()
        {
            var row = await CorrectionSchedulingChecks.Source(db, own, clock);
            row.AssignedRole = CanonicalRole.Direction;
            row.CompletedAt = null; row.CompletedBy = null; row.CompletionKind = null; row.ReviewStatus = null;
            db.Update(row); await db.SaveChangesAsync(); db.ChangeTracker.Clear(); return row;
        }
        var rows = new[] { await Source(), await Source() };
        var selected = rows.Select(r => r.Id).ToArray();
        var preview = await service.PreviewPostponementAsync(fixture.Director, selected, "ERROR_DE_ASIGNACION", "QA automática", default);
        Check(preview.Conflicts.Length == 0, "director preview valid: " + string.Join("; ", preview.Conflicts));
        var intent = Guid.NewGuid();
        var id = await service.SubmitPostponementAsync(fixture.Director, selected, "ERROR_DE_ASIGNACION", "QA automática", preview.Fingerprint, intent, default);
        db.ChangeTracker.Clear();
        var approved = await db.Set<OperationalCarryoverRequest>().AsNoTracking().SingleAsync(r => r.Id == id);
        Check(approved.State == "APROBADA" && approved.DecidedBy == fixture.Director && approved.Results().Length == 2, "submission approves every item with director attribution");
        foreach (var item in preview.Items) {
            var row = await db.OperationalAssignments.AsNoTracking().SingleAsync(a => a.Id == item.AssignmentId);
            Check(row.StartsAt == item.ProposedStart && row.DueAt == item.Proposed && row.Revision == item.Revision + 1, "dates applied exactly once");
        }
        Check(!(await service.InboxAsync(fixture.Director, default)).Any(r => r.Id == id && r.State == "PENDIENTE"), "no second approval pending");
        Check(await db.AuditEvents.CountAsync(a => a.ResourceId == id) == 2, "submission and approval audits retained");
        var after = JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().Where(a => selected.Contains(a.Id)).OrderBy(a => a.Id).ToArrayAsync());
        Check(id == await service.SubmitPostponementAsync(fixture.Director, selected, "ERROR_DE_ASIGNACION", "QA automática", preview.Fingerprint, intent, default), "submission retry returns same approved request");
        Check(after == JsonSerializer.Serialize(await db.OperationalAssignments.AsNoTracking().Where(a => selected.Contains(a.Id)).OrderBy(a => a.Id).ToArrayAsync()) && await db.AuditEvents.CountAsync(a => a.ResourceId == id) == 2, "retry does not apply or audit twice");
        var staleRow = await Source();
        var stale = await service.PreviewPostponementAsync(fixture.Director, [staleRow.Id], "ERROR_DE_ASIGNACION", "QA obsoleta", default);
        var staleIntent = Guid.NewGuid();
        await db.OperationalAssignments.Where(a => a.Id == staleRow.Id).ExecuteUpdateAsync(s => s.SetProperty(a => a.LoadMinutes, a => a.LoadMinutes + 1));
        try { await service.SubmitPostponementAsync(fixture.Director, [staleRow.Id], "ERROR_DE_ASIGNACION", "QA obsoleta", stale.Fingerprint, staleIntent, default); throw new Exception("stale proposal accepted"); }
        catch (DirectionPlanException e) { Check(e.Code == "ARRASTRE_CONFLICTO", "stale proposal rejected"); }
        Check(!await db.Set<OperationalCarryoverRequest>().AnyAsync(r => r.IntentKey == staleIntent), "stale submission creates no request");
        await RollbackCheck(db, fixture, own, clock, service, await Source());
        var concurrentRow = await Source();
        var cp = await service.PreviewPostponementAsync(fixture.Director, [concurrentRow.Id], "ERROR_DE_ASIGNACION", "QA concurrencia", default);
        var concurrentIntent = Guid.NewGuid();
        async Task<Guid> ConcurrentSubmit() {
            await using var context = new SgolDbContext(new DbContextOptionsBuilder<SgolDbContext>().UseNpgsql(db.Database.GetConnectionString()).Options);
            var other = new OperationalCarryoverService(context, clock, CorrectionSchedulingChecks.Service(context, clock));
            return await other.SubmitPostponementAsync(fixture.Director, [concurrentRow.Id], "ERROR_DE_ASIGNACION", "QA concurrencia", cp.Fingerprint, concurrentIntent, default);
        }
        var concurrentIds = await Task.WhenAll(ConcurrentSubmit(), ConcurrentSubmit());
        Check(concurrentIds[0] == concurrentIds[1] && await db.AuditEvents.CountAsync(a => a.ResourceId == concurrentIds[0]) == 2, "concurrent repeated submission authorizes once");
        var workerRow = await CorrectionSchedulingChecks.Source(db, fixture, clock);
        workerRow.CompletedAt = null; workerRow.CompletedBy = null; workerRow.CompletionKind = null; workerRow.ReviewStatus = null;
        db.Update(workerRow); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var wp = await service.PreviewPostponementAsync(fixture.Worker, [workerRow.Id], "ERROR_DE_ASIGNACION", "QA otros roles", default);
        Check(wp.Conflicts.Length == 0, "ordinary supervised request still valid");
        var wid = await service.SubmitPostponementAsync(fixture.Worker, [workerRow.Id], "ERROR_DE_ASIGNACION", "QA otros roles", wp.Fingerprint, Guid.NewGuid(), default);
        try { await service.DecideAsync(fixture.Worker, wid, 1, false, "Intento propio", default); throw new Exception("self decision allowed for worker"); }
        catch (DirectionPlanException e) { Check(e.Status == 403, "worker self decision denied"); }
        foreach (var role in new[] { CanonicalRole.Subcoordination, CanonicalRole.Administration })
        {
            await db.RoleAssignmentVersions.Where(r => r.UserId == fixture.Worker).ExecuteUpdateAsync(s => s.SetProperty(r => r.RoleCode, role));
            Check(!(await service.InboxAsync(fixture.Worker, default)).Any(r => r.Id == wid), role + " own request excluded");
            try { await service.PreviewDecisionAsync(fixture.Worker, new(wid, workerRow.Id, workerRow.Revision, "REJECT", null, null, "Intento propio", Guid.NewGuid()), default); throw new Exception("self resolution allowed"); }
            catch (DirectionPlanException e) { Check(e.Status == 403, role + " self resolution denied"); }
            var roleRow = await CorrectionSchedulingChecks.Source(db, fixture, clock);
            roleRow.CompletedAt = null; roleRow.CompletedBy = null; roleRow.CompletionKind = null; roleRow.ReviewStatus = null;
            db.Update(roleRow); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var rolePreview = await service.PreviewPostponementAsync(fixture.Worker, [roleRow.Id], "ERROR_DE_ASIGNACION", "QA rol pendiente", default);
            var roleId = await service.SubmitPostponementAsync(fixture.Worker, [roleRow.Id], "ERROR_DE_ASIGNACION", "QA rol pendiente", rolePreview.Fingerprint, Guid.NewGuid(), default);
            Check((await db.Set<OperationalCarryoverRequest>().AsNoTracking().SingleAsync(r => r.Id == roleId)).State == "PENDIENTE" &&
                (await db.OperationalAssignments.AsNoTracking().SingleAsync(a => a.Id == roleRow.Id)).DueAt == roleRow.DueAt, role + " new submission stays pending without date changes");
        }
        await db.RoleAssignmentVersions.Where(r => r.UserId == fixture.Worker).ExecuteUpdateAsync(s => s.SetProperty(r => r.RoleCode, CanonicalRole.SalesFloor));
        await service.DecideAsync(fixture.Director, wid, 1, false, "Supervisión ordinaria", default);
        Check((await service.InboxAsync(fixture.Director, default)).Any(r => r.PersonId == person && r.State != "PENDIENTE"), "own history visible");
        var incident = await Source();
        var ip = await service.PreviewAsync(fixture.Director, incident.StartsAt.AddMinutes(1), 1, "FALLA_SISTEMA_HERRAMIENTA", "QA incidente propio", default);
        Check(ip.Conflicts.Length == 0, "own incident preview: " + string.Join("; ", ip.Conflicts));
        var iid = await service.SubmitAsync(fixture.Director, ip.End, ip.Minutes, ip.Cause, ip.Explanation, ip.Fingerprint, Guid.NewGuid(), default);


        db.ChangeTracker.Clear();
        Check((await db.Set<OperationalCarryoverRequest>().AsNoTracking().SingleAsync(r => r.Id == iid)).DecidedBy == fixture.Director, "integral own incident approval records director");
        await db.AppUsers.Where(u => u.Id == fixture.Director).ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, "INACTIVA"));
        try { await service.InboxAsync(fixture.Director, default); throw new Exception("inactive director allowed"); }
        catch (DirectionPlanException e) { Check(e.Status == 403, "inactive director denied"); }
        await db.AppUsers.Where(u => u.Id == fixture.Director).ExecuteUpdateAsync(s => s.SetProperty(u => u.Status, "ACTIVA"));
        var formRow = await Source();
        await CarryoverResolutionHttpChecks.Run(db, own, clock, async () =>
        {
            using var client = new HttpClient(new HttpClientHandler { CookieContainer = new(), AllowAutoRedirect = true }) { BaseAddress = new Uri("https://localhost:7291") };
            string Value(string html, string name) {
                var tag = Regex.Matches(html, "<input[^>]+>").Select(m => m.Value).First(x => x.Contains($"name=\"{name}\""));
                return WebUtility.HtmlDecode(Regex.Match(tag, "value=\"([^\"]*)\"").Groups[1].Value);
            }
            async Task<string> Post(string url, string html, Dictionary<string,string> data) {
                data["__RequestVerificationToken"] = Value(html, "__RequestVerificationToken");
                var response = await client.PostAsync(url, new FormUrlEncodedContent(data));
                Check(response.IsSuccessStatusCode, "HTTP form " + url + " " + response.StatusCode);
                return await response.Content.ReadAsStringAsync();
            }
            var html = await client.GetStringAsync("/mi-trabajo/arrastre");
            var fields = new Dictionary<string,string> { ["Selected"] = formRow.Id.ToString(), ["PostponeCause"] = "ERROR_DE_ASIGNACION", ["PostponeExplanation"] = "QA formulario director", ["PostponeIntent"] = Guid.NewGuid().ToString() };
            html = await Post("/mi-trabajo/arrastre?handler=PreviewPostponement", html, fields);
            Check(html.Contains("Confirmar y aplicar arrastre"), "director button explains immediate effect");
            fields["PostponeFingerprint"] = Value(html, "PostponeFingerprint");
            Check(!html.Contains("No hay un supervisor inmediato activo"), "director form has no missing-supervisor error");
            html = await Post("/mi-trabajo/arrastre?handler=SubmitPostponement", html, fields);
            var saved = await db.Set<OperationalCarryoverRequest>().AsNoTracking().SingleAsync(r => r.PersonId == person && r.Explanation == "QA formulario director");
            Check(saved.State == "APROBADA" && saved.DecidedBy == fixture.Director && saved.Results().Length == 1, "HTTP submit automatically approves without supervision POST");
            Check(WebUtility.HtmlDecode(html).Contains("Petición autorizada automáticamente"), "HTTP success explains applied authorization");
            Check(!(await service.InboxAsync(fixture.Director, default)).Any(r => r.Id == saved.Id && r.State == "PENDIENTE"), "HTTP submitted request absent from pending supervision");
            Check((await db.OperationalAssignments.AsNoTracking().SingleAsync(a => a.Id == formRow.Id)).DueAt == saved.Items().Single().Proposed, "HTTP submit applies proposed due date");
            if (Environment.GetEnvironmentVariable("SGOL_CARRYOVER_BROWSER_SCRIPT") is {} script) {
                var browserRow = await Source();
                var start = new System.Diagnostics.ProcessStartInfo("node") { UseShellExecute = false };
                start.ArgumentList.Add(script); start.ArgumentList.Add(browserRow.Id.ToString());
                using var browser = System.Diagnostics.Process.Start(start)!;
                await browser.WaitForExitAsync(); Check(browser.ExitCode == 0, "browser flow and responsive text");
                var browserRequest = (await service.MineAsync(fixture.Director, default)).Single(r => r.Items().Any(i => i.AssignmentId == browserRow.Id));
                Check(browserRequest.State == "APROBADA" && browserRequest.DecidedBy == fixture.Director, "browser submit persisted automatic approval");
            }
        }, directorMyWork: true);
    }
}
