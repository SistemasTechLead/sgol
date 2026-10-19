using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Identity;
using Sgol.Web.Infrastructure.Persistence.Planning;

internal static class ScreenDeviceChecks
{
    public static async Task RunAsync(IServiceProvider services, Guid actor, Uri origin)
    {
        if (origin.Host != "localhost" || origin.Scheme != "https") throw new Exception("HTTP checks require an isolated localhost HTTPS server.");
        var db = services.GetRequiredService<SgolDbContext>();
        var registry = services.GetRequiredService<ScreenAccountService>();
        var devices = services.GetRequiredService<ScreenDeviceService>();
        var code = "SCREEN-HTTP-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(4));
        var account = await registry.CreateAsync(actor, code, "Pantalla HTTP de prueba", "Isolated integration checks");
        var secret = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        using var native = NewClient();
        using var browser = NewClient();
        using var response = await native.PostAsJsonAsync("/api/v1/screens/link", new
        { Code = code, CredentialHash = ScreenDeviceService.Hash(secret), DeviceName = "ISOLATED-CHECKS" });
        Ensure(response.StatusCode == HttpStatusCode.OK, "Pairing request");
        using var link = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var approval = link.RootElement.GetProperty("approvalCode").GetString()!;
        native.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", $"{account.Id:D}.{secret}");
        Ensure((await native.PostAsync("/api/v1/screens/ticket", null)).StatusCode == HttpStatusCode.Accepted, "Approval required before ticket issuance");
        await devices.ApproveAsync(actor, approval);
        db.ChangeTracker.Clear();
        try { await devices.ApproveAsync(actor, approval); throw new Exception("Pairing code replay was accepted."); }
        catch (ScreenDeviceException ex) when (ex.Status == 409) { }
        try { await devices.ApproveAsync(account.Id, approval); throw new Exception("Screen administered pairing."); }
        catch (Sgol.Planning.Contracts.DirectionPlanException ex) when (ex.Status == 403) { }

        var ticket = await Ticket();
        var entered = await Enter(browser, ticket);
        Ensure(entered.StatusCode == HttpStatusCode.Found && entered.Headers.Location?.OriginalString == "/tablero", "Browser ticket entry");
        var board = await browser.GetAsync("/tablero");
        var html = await board.Content.ReadAsStringAsync();
        Ensure(board.StatusCode == HttpStatusCode.OK && html.Contains("data-screen=\"true\"") && !html.Contains("href=\"/mi-trabajo\""), "Read-only board rendering");
        Ensure((await browser.GetAsync("/tablero/rol-de-tareas")).StatusCode == HttpStatusCode.OK, "Weekly board");
        Ensure((await browser.GetAsync("/tablero?handler=Refresh")).StatusCode == HttpStatusCode.OK, "Board refresh");
        Ensure((await browser.GetAsync("/pantalla")).StatusCode == HttpStatusCode.OK, "Proactive session reconnection page");
        foreach (var path in new[] { "/mi-trabajo", "/configuracion", "/tareas", "/asignar-tarea", "/personas-y-accesos", "/planificacion", "/reportes", "/api/v1/auth/session", "/api/v1/auth/csrf", "/Board/Index", "/tablero/otro", "/acceso", "/api/v1/screens/ticket" })
            Ensure((await browser.GetAsync(path)).StatusCode == HttpStatusCode.Forbidden, "Screen boundary: " + path);
        Ensure((await browser.PostAsync("/tablero?handler=Refresh", null)).StatusCode == HttpStatusCode.Forbidden, "Screen POST rejection");
        var replay = await Enter(browser, ticket);
        Ensure(replay.Headers.Location?.OriginalString == "/pantalla", "One-use ticket replay rejected");

        var expiring = await Ticket();
        var expiringHash = ScreenDeviceService.Hash(expiring);
        await db.Set<ScreenBrowserTicket>().Where(x => x.TicketHash == expiringHash)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAt, DateTimeOffset.UtcNow.AddSeconds(-1)));
        Ensure((await Enter(browser, expiring)).Headers.Location?.OriginalString == "/pantalla", "Expired ticket rejection");

        var concurrent = await Ticket();
        using var race1 = NewClient(); using var race2 = NewClient();
        var racers = await Task.WhenAll(Enter(race1, concurrent), Enter(race2, concurrent));
        Ensure(racers.Count(r => r.Headers.Location?.OriginalString == "/tablero") == 1, "Concurrent ticket consumed exactly once");
        using var emptyBrowser = NewClient();
        Ensure((await Enter(emptyBrowser, await Ticket())).Headers.Location?.OriginalString == "/tablero" &&
            (await emptyBrowser.GetAsync("/tablero")).StatusCode == HttpStatusCode.OK, "Recovery with no browser cookies");
        var reader = services.GetRequiredService<LiveBoardReader>();
        Ensure((await reader.ReadAsync(actor, null)).Version == (await reader.ReadAsync(account.Id, null)).Version, "Existing user's board data remains identical");
        var ledger = await OperationalCapacityService.LedgerAsync(db, DateTimeOffset.UtcNow, default);
        Ensure(ledger.Workers.All(x => x.Id != account.Id) && ledger.DayCapacity(account.Id, ledger.Today) == 0, "Active device excluded from planning and capacity");

        var unused = await Ticket();
        await devices.RevokeAsync(actor, code, "Integration test revocation");
        Ensure((await browser.GetAsync("/tablero")).Headers.Location?.OriginalString == "/pantalla", "Revocation invalidates browser session");
        Ensure((await native.PostAsync("/api/v1/screens/ticket", null)).StatusCode == HttpStatusCode.Unauthorized, "Revocation invalidates credential");
        using var revokedBrowser = NewClient();
        Ensure((await Enter(revokedBrowser, unused)).Headers.Location?.OriginalString == "/pantalla", "Revocation invalidates unconsumed tickets");
        using var anonymous = NewClient();
        Ensure((await anonymous.GetAsync("/tablero")).Headers.Location?.OriginalString == "/acceso", "Anonymous board still requires login");
        Ensure((await anonymous.GetAsync("/api/v1/auth/session")).StatusCode == HttpStatusCode.Unauthorized, "Ordinary session still requires authentication");
        Console.WriteLine("PASS HTTP pairing approval, one-use and expired tickets, concurrent redemption, cookie-free reconnection and revocation.");
        Console.WriteLine("PASS daily/weekly board and refresh; all other pages/APIs/POSTs blocked; ordinary anonymous authentication retained.");
        Console.WriteLine("PASS active device has zero capacity and no worker eligibility; ordinary and device board data match.");

        HttpClient NewClient() => new(new HttpClientHandler
        {
            AllowAutoRedirect = false, CookieContainer = new CookieContainer(),
            // Isolated localhost test server only; the deployed client uses normal TLS validation.
            ServerCertificateCustomValidationCallback = (request, _, _, _) => request.RequestUri?.Host == "localhost"
        }) { BaseAddress = origin, Timeout = TimeSpan.FromSeconds(15) };
        async Task<string> Ticket()
        {
            using var result = await native.PostAsync("/api/v1/screens/ticket", null);
            Ensure(result.StatusCode == HttpStatusCode.OK, "Native credential exchange");
            using var data = JsonDocument.Parse(await result.Content.ReadAsStringAsync());
            return data.RootElement.GetProperty("ticket").GetString()!;
        }
    }
    private static async Task<HttpResponseMessage> Enter(HttpClient client, string value)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/pantalla/entrar")
        { Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["ticket"] = value }) };
        message.Headers.Add("Origin", "http://127.0.0.1:17841");
        return await client.SendAsync(message);
    }
    private static void Ensure(bool success, string message) { if (!success) throw new Exception(message); }
}
