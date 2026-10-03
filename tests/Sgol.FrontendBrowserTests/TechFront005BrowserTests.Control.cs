using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using Sgol.Cv05Demo;
using Xunit;

namespace Sgol.FrontendBrowserTests;

public sealed partial class TechFront005BrowserTests
{
    private async Task ControlAsync()
    {
        var from = DateTimeOffset.UtcNow.AddDays(-1).UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        var to = DateTimeOffset.UtcNow.AddDays(1).UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        foreach (var (page, index) in pages.Select((p, i) => (p, i)))
        {
            var before = await fixture.Front020RowsAsync();
            await page.GotoAsync($"/indicadores?operationisoYear={year}&operationisoWeek={week}");
            await Assertions.Expect(page.Locator("#operation")).ToContainTextAsync("Base de obligaciones");
            Assert.Equal(index == 0 ? 1 : 0, await page.Locator("#direction").CountAsync());
            await VerifyIndicatorsAsync(page, index, "/api/v1/indicators", "operation");
            if (index == 0)
            {
                await page.GotoAsync($"/indicadores?operationisoYear={year}&operationisoWeek={week}&directionisoYear={year}&directionisoWeek={week}");
                await VerifyIndicatorsAsync(page, index, "/api/v1/direction/overview", "direction");
            }
            Same(before, await fixture.Front020RowsAsync());
            await Capture(page, "R9-indicadores-" + fixture.Accounts[index].Role);
            await page.GotoAsync("/auditoria?from=" + Uri.EscapeDataString(from) + "&to=" + Uri.EscapeDataString(to));
            await Assertions.Expect(page.Locator("#audit")).ToContainTextAsync("Consulta UTC");
            Same(before, await fixture.Front020RowsAsync());
            await Capture(page, "R9-auditoria-" + fixture.Accounts[index].Role);
            if (index != 0) Assert.Equal(403, (await page.GotoAsync("/continuidad"))!.Status);
        }
        var tracePath = "/auditoria?mode=trace&from=" + Uri.EscapeDataString(from) + "&to=" + Uri.EscapeDataString(to) +
            "&traceObligationId=" + obligations["TAR-0007"];
        var initialTrace = await Direction.GotoAsync(tracePath);
        var apiTrace = await contexts[0].APIRequest.GetAsync("/api/v1/audit-events?from=" + Uri.EscapeDataString(from) +
            "&to=" + Uri.EscapeDataString(to) + "&traceObligationId=" + obligations["TAR-0007"]);
        var traceCode = "NONE";
        if (apiTrace.Status != 200)
        {
            using var error = JsonDocument.Parse(await apiTrace.BodyAsync());
            traceCode = error.RootElement.TryGetProperty("code", out var code) && code.GetString() == "AUDIT_SCOPE_INCONSISTENT"
                ? "AUDIT_SCOPE_INCONSISTENT" : "OTHER_CONTRACT_FAILURE";
        }
        await File.WriteAllTextAsync(Path.Combine(directory, "trace-diagnostic.json"),
            "{\"status\":" + initialTrace!.Status + ",\"apiStatus\":" + apiTrace.Status + ",\"code\":\"" + traceCode + "\"}");
        await VerifyPersistedChainAsync();
        var account = fixture.Accounts[0];
        using var client = owner.CreateClient();
        var cookie = (await contexts[0].CookiesAsync()).Single(c => c.Name == "__Host-SGOL-Session");
        client.DefaultRequestHeaders.Add("Cookie", cookie.Name + "=" + cookie.Value);
        var csrf = await HostedAuthenticationClient.GetCsrfAsync(client, CancellationToken.None);
        var runtime = new Cv05RecoveryRuntime(owner, new(new(account.UserId, account.PersonId, account.UserName,
            account.Role, account.TemporaryPassword, account.NewPassword, "TF005"), client, csrf));
        await runtime.PrepareAsync(CancellationToken.None);
        var matched = await RequestThroughInterface();
        await runtime.CaptureAsync(matched, CancellationToken.None);
        var restore = await runtime.RestoreAsync(matched, CancellationToken.None);
        var reconciled = await runtime.ReconcileAsync(matched, restore, [0], CancellationToken.None);
        using (reconciled.Details) { Assert.Equal(0, reconciled.Exit); }
        var path = "/continuidad/reconciliaciones/" + matched;
        var views = await fixture.Front020ViewsAsync();
        await Direction.GotoAsync(path);
        Assert.Equal(views + 1, await fixture.Front020ViewsAsync());
        await Assertions.Expect(Direction.Locator("[name=reason]")).ToBeVisibleAsync();
        await Capture(Direction, "R9-restauracion-MATCHED");
        var beforeApproval = await fixture.Front020RowsAsync();
        await Direction.Locator("[name=reason]").FillAsync("Aceptación sintética del resultado coincidente");
        await Submit(Direction, Direction.GetByRole(AriaRole.Button, new() { Name = "Preparar aprobación", Exact = true }), "Prepare");
        Same(beforeApproval, await fixture.Front020RowsAsync());
        var intention = await Direction.Locator("form[data-continuity-send] [name=intention]").InputValueAsync();
        var token = await Direction.Locator("form[data-continuity-send] [name=__RequestVerificationToken]").InputValueAsync();
        await Submit(Direction, Direction.Locator("form[data-continuity-send] button[type=submit]"), "Send");
        await Assertions.Expect(Direction.Locator("[data-front-success]")).ToContainTextAsync("Aceptación de la reconciliación registrada");
        var approved = await fixture.Front020RowsAsync();
        var replay = await contexts[0].APIRequest.PostAsync(path + "?handler=Send", new()
        { Form = contexts[0].APIRequest.CreateFormData().Set("intention", intention).Set("__RequestVerificationToken", token) });
        Assert.Equal(200, replay.Status); Same(approved, await fixture.Front020RowsAsync());
        await Capture(Direction, "R9-aceptacion-auditada");
        var different = await RequestThroughInterface();
        await runtime.CaptureAsync(different, CancellationToken.None);
        restore = await runtime.RestoreAsync(different, CancellationToken.None);
        await runtime.MutateRestoreAsync("evidence_version", CancellationToken.None);
        var mismatch = await runtime.ReconcileAsync(different, restore, [2], CancellationToken.None);
        using (mismatch.Details) { Assert.Equal(2, mismatch.Exit); }
        await Direction.GotoAsync("/continuidad/reconciliaciones/" + different);
        Assert.Equal(0, await Direction.Locator("[name=reason]").CountAsync());
        await Capture(Direction, "R9-restauracion-DIFFERENT");
        await using var db = fixture.TechFront005Context();
        Assert.Equal(2, await db.RecoveryReconciliations.CountAsync());
        Assert.Equal(0, await db.RecoveryReconciliationEvents.CountAsync(e => e.ReconciliationId == different && e.EventType == "RECOVERY_RECONCILIATION_APPROVED"));
        var traced = await Direction.GotoAsync(tracePath);
        output.WriteLine("TECH_FRONT005 AUDIT_TRACE_HTTP " + traced!.Status);
        Assert.Equal(200, traced.Status);
        Assert.True(await Direction.Locator("#audit tbody tr").CountAsync() > 0, "No backend chain was returned.");
        await Capture(Direction, "R9-traza-completa");
        foreach (var stage in new[] { "Configuración", "Asignación", "Evidencia", "Validación" })
            await Assertions.Expect(Direction.Locator("#audit")).ToContainTextAsync(stage + ": Con hechos registrados");
    }

    private async Task VerifyIndicatorsAsync(IPage page, int actorIndex, string endpoint, string section)
    {
        // Every expected obligation was created and completed through the preceding interface journeys.
        var expectedBase = actorIndex == 3 ? 2L : 7L;
        var response = await page.Context.APIRequest.GetAsync(endpoint + "?" + Period + "&limit=100");
        Assert.Equal(200, response.Status);
        using var body = JsonDocument.Parse(await response.BodyAsync());
        var data = body.RootElement.GetProperty("data");
        var actualBase = data.GetProperty("baseObligationsCount").GetInt64();
        if (actualBase != expectedBase)
        {
            await using var database = fixture.TechFront005Context();
            await File.WriteAllTextAsync(Path.Combine(directory, "indicator-base-diagnostic.json"),
                JsonSerializer.Serialize(new
                {
                    role = fixture.Accounts[actorIndex].Role,
                    section,
                    year,
                    week,
                    expectedBase,
                    actualBase,
                    trackedObligations = obligations.Count,
                    persistedObligations = await database.WorkObligations.CountAsync(),
                    nonManualObligations = await database.WorkObligations.CountAsync(item => item.ManualTaskCode == null),
                }));
        }
        Assert.Equal(expectedBase, data.GetProperty("baseObligationsCount").GetInt64());
        var counts = new[] { ("pending", "Pendientes", 0L), ("concluded", "Concluidas", expectedBase),
            ("validated", "Validadas", expectedBase), ("nonCompliant", "Incumplidas", 1L) };
        foreach (var (property, label, expected) in counts)
        {
            Assert.Equal(expected, data.GetProperty(property).GetProperty("count").GetInt64());
            Assert.Equal(expectedBase, data.GetProperty(property).GetProperty("denominator").GetInt64());
            var row = page.Locator("#" + section + " table").First.Locator("tbody tr").Filter(new() { HasTextString = label });
            await Assertions.Expect(row.Locator("td").Nth(0)).ToHaveTextAsync(expected.ToString(CultureInfo.InvariantCulture));
            await Assertions.Expect(row.Locator("td").Nth(1)).ToHaveTextAsync(expectedBase.ToString(CultureInfo.InvariantCulture));
        }
        var load = data.GetProperty("activeLoadByPerson");
        Assert.Equal(0, load.GetProperty("denominator").GetInt64());
        Assert.True(load.GetProperty("items").GetArrayLength() > 0);
        foreach (var item in load.GetProperty("items").EnumerateArray()) Assert.Equal(0, item.GetProperty("count").GetInt64());
        output.WriteLine("TECH_FRONT005 INDICATORS " + section + " role=" + fixture.Accounts[actorIndex].Role + " base=" + expectedBase);
    }

    private async Task<Guid> RequestThroughInterface()
    {
        await Direction.GotoAsync("/continuidad");
        var before = await fixture.Front020RowsAsync();
        await Direction.Locator("[name=reason]").FillAsync("Simulacro sintético integral de recuperación");
        await Submit(Direction, Direction.GetByRole(AriaRole.Button, new() { Name = "Preparar solicitud", Exact = true }), "Prepare");
        Same(before, await fixture.Front020RowsAsync());
        await Direction.Keyboard.PressAsync("Escape");
        await Assertions.Expect(Direction.Locator("dialog[open]")).ToHaveCountAsync(0);
        await Assertions.Expect(Direction.Locator("#prepare-continuity")).ToBeFocusedAsync();
        Same(before, await fixture.Front020RowsAsync());
        await Direction.Locator("#prepare-continuity").ClickAsync();
        await Submit(Direction, Direction.Locator("dialog[open] form[data-continuity-send] button[type=submit]"), "Send");
        await Assertions.Expect(Direction.Locator("[data-front-success]")).ToContainTextAsync("Solicitud de reconciliación registrada");
        await using var db = fixture.TechFront005Context();
        return await db.RecoveryReconciliations.OrderByDescending(r => r.RequestedAt).Select(r => r.Id).FirstAsync();
    }
}
