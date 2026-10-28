using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Identity;

internal static class ScreenChecks
{
    public static async Task RunAsync(IServiceProvider provider, Guid actor, Action<bool, string> check)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        var before = await db.PersonalSessions.CountAsync();
        var accounts = scope.ServiceProvider.GetRequiredService<ScreenAccountService>();
        var devices = scope.ServiceProvider.GetRequiredService<ScreenDeviceService>();
        var account = await accounts.CreateAsync(actor, "SECURITY-SCREEN", "Isolated screen", "Security regression");
        var secret = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var link = await devices.RequestLinkAsync(account.Code, ScreenDeviceService.Hash(secret), "Isolated device", default);
        await devices.ApproveAsync(actor, link.ApprovalCode);
        var ticket = await devices.IssueTicketAsync(account.Id, secret, default);
        using var browser = new Browser("https://localhost:7287");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/pantalla/entrar")
        { Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["ticket"] = ticket! }) };
        request.Headers.Add("Origin", "http://127.0.0.1:17841");
        var entered = await browser.Client.SendAsync(request);
        check((int)entered.StatusCode == 302 && (await browser.Client.GetAsync("/tablero")).IsSuccessStatusCode, "S08 real PANTALLA ticket/cookie can read board");
        var denied = await browser.Client.PostAsync("/mi-trabajo", new FormUrlEncodedContent(new Dictionary<string, string>()));
        check((int)denied.StatusCode == 403 && await db.PersonalSessions.CountAsync() == before, "S08 PANTALLA remains read-only without personal session record");
    }
}
