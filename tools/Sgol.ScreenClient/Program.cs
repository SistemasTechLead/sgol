using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

const string localOrigin = "http://127.0.0.1:17841";
var home = args.Contains("--test") ? Path.Combine(AppContext.BaseDirectory, "test-state") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SGOL", "Pantalla");
var credentialFile = Path.Combine(home, "device.bin");
if (args.Contains("--self-test"))
{
    var value = RandomNumberGenerator.GetBytes(32);
    var encrypted = WindowsCredentialStore.Protect(value);
    if (encrypted.SequenceEqual(value) || !WindowsCredentialStore.Unprotect(encrypted).SequenceEqual(value)) throw new Exception("DPAPI failed.");
    Console.WriteLine("PASS Windows DPAPI CurrentUser round trip; no persistent credential created.");
    return;
}
Directory.CreateDirectory(home);
var configPath = Path.Combine(AppContext.BaseDirectory, "pantalla.json");
var config = JsonSerializer.Deserialize<ClientConfiguration>(await File.ReadAllTextAsync(configPath)) ?? throw new Exception("Missing screen configuration.");
if (!Uri.TryCreate(config.ServerOrigin, UriKind.Absolute, out var server) || server.Scheme != "https" ||
    server.AbsolutePath != "/" || server.UserInfo.Length != 0 || server.Query.Length != 0 || server.Fragment.Length != 0)
    throw new Exception("A trusted HTTPS server origin is required.");
if (args.Contains("--test") && server.Host != "localhost") throw new Exception("Client integration checks require a localhost server.");
using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = server, Timeout = TimeSpan.FromSeconds(12) };
using var gate = new SemaphoreSlim(1, 1);
var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.WebHost.UseUrls(localOrigin);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 1024);
var app = builder.Build();
app.MapGet("/", async (HttpContext context) =>
{
    // Listen only on loopback; reject DNS rebinding, cross-origin fetches and iframes.
    if (context.Request.Host.Value != "127.0.0.1:17841" ||
        context.Request.Headers["Sec-Fetch-Dest"] is var dest && dest.Count > 0 && dest != "document")
    { context.Response.StatusCode = 403; return; }
    context.Response.Headers.CacheControl = "no-store";
    // Form navigations need a concrete Origin for the server's loopback check.
    // The local origin contains no credentials; the ticket stays in the POST body.
    context.Response.Headers["Referrer-Policy"] = "origin";
    var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
    context.Response.Headers.ContentSecurityPolicy = $"default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action {server.GetLeftPart(UriPartial.Authority)}; script-src 'nonce-{nonce}'";
    context.Response.ContentType = "text/html; charset=utf-8";
    await gate.WaitAsync(context.RequestAborted);
    try
    {
        var state = ReadState();
        if (state is null)
        {
            state = new DeviceState(Guid.Empty, WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32)), null, false);
            SaveState(state);
        }
        if (state.ScreenId != Guid.Empty)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/screens/ticket");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", $"{state.ScreenId:D}.{state.Secret}");
            using var response = await client.SendAsync(request, context.RequestAborted);
            if (response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.Accepted)
            {
                var result = await response.Content.ReadFromJsonAsync<TicketResponse>(context.RequestAborted);
                if (result?.Ticket is not { Length: 43 }) throw new Exception("Invalid login pass.");
                if (!state.Linked) SaveState(state with { Linked = true, ApprovalCode = null });
                var action = WebUtility.HtmlEncode(new Uri(server, "/pantalla/entrar").AbsoluteUri);
                await context.Response.WriteAsync($"<!doctype html><html lang='es'><meta charset='utf-8'><meta name='referrer' content='origin'><title>Conectando SGOL</title><body><p>Abriendo tablero…</p><form method='post' action='{action}'><input type='hidden' name='ticket' value='{WebUtility.HtmlEncode(result.Ticket)}'></form><script nonce='{nonce}'>document.forms[0].submit()</script></body></html>", context.RequestAborted);
                return;
            }
            if (response.StatusCode == HttpStatusCode.Accepted)
            { await Page(context, "Autoriza esta computadora", $"Código de vinculación: <strong>{WebUtility.HtmlEncode(state.ApprovalCode)}</strong><p>Comparte este código en la conversación donde se está preparando la pantalla. Esta página continuará sola al autorizarla.</p>"); return; }
            if (state.Linked || response.StatusCode is not HttpStatusCode.Unauthorized)
            { await Page(context, "Pantalla sin acceso", "SGOL no autorizó el acceso. Si se revocó la pantalla, debe vincularse de nuevo desde administración."); return; }
        }
        using var pairing = await client.PostAsJsonAsync("/api/v1/screens/link", new
        {
            Code = config.AccountCode,
            CredentialHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(state.Secret))),
            DeviceName = Environment.MachineName
        }, context.RequestAborted);
        if (!pairing.IsSuccessStatusCode)
        { await Page(context, "No se pudo vincular", "Espera unos minutos. La pantalla puede estar vinculada a otra computadora o tener una solicitud pendiente."); return; }
        var link = await pairing.Content.ReadFromJsonAsync<LinkResponse>(context.RequestAborted) ?? throw new Exception("Invalid pairing response.");
        SaveState(state with { ScreenId = link.ScreenId, ApprovalCode = link.ApprovalCode });
        await Page(context, "Autoriza esta computadora", $"Código de vinculación: <strong>{WebUtility.HtmlEncode(link.ApprovalCode)}</strong><p>Comparte este código en la conversación donde se está preparando la pantalla. Esta página continuará sola al autorizarla.</p>");
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
    { await Page(context, "Esperando conexión", "No fue posible conectar con SGOL. Revisa la red y el certificado del servidor. Volveremos a intentar automáticamente."); }
    catch (CryptographicException)
    { await Page(context, "Credencial protegida", "Abre la pantalla con la misma cuenta de Windows con la que se instaló. La credencial no puede abrirse desde otra cuenta o computadora."); }
    catch (Exception)
    { await Page(context, "Esperando pantalla", "No se pudo preparar el acceso. Se volverá a intentar; si persiste, revisa la instalación de Pantalla SGOL."); }
    finally { gate.Release(); }
});
await app.StartAsync();
if (!args.Contains("--sin-abrir")) OpenEdge();
// On a dedicated Windows account, recover if Edge closes entirely.
var sessionId = Process.GetCurrentProcess().SessionId;
using var watchdog = new PeriodicTimer(TimeSpan.FromSeconds(15));
var watch = Task.Run(async () =>
{
    while (await watchdog.WaitForNextTickAsync(app.Lifetime.ApplicationStopping))
        if (!args.Contains("--sin-abrir") && !Process.GetProcessesByName("msedge").Any(p => p.SessionId == sessionId)) OpenEdge();
});
await app.WaitForShutdownAsync();

DeviceState? ReadState() => File.Exists(credentialFile)
    ? JsonSerializer.Deserialize<DeviceState>(WindowsCredentialStore.Unprotect(File.ReadAllBytes(credentialFile))) : null;
void SaveState(DeviceState state)
{
    var bytes = JsonSerializer.SerializeToUtf8Bytes(state);
    try
    {
        File.WriteAllBytes(credentialFile + ".tmp", WindowsCredentialStore.Protect(bytes));
        File.Move(credentialFile + ".tmp", credentialFile, true);
    }
    finally { CryptographicOperations.ZeroMemory(bytes); }
}
static Task Page(HttpContext context, string title, string content) => context.Response.WriteAsync(
    $"<!doctype html><html lang='es'><meta charset='utf-8'><meta http-equiv='refresh' content='8'><title>{WebUtility.HtmlEncode(title)}</title><body><h1>{WebUtility.HtmlEncode(title)}</h1><p>{content}</p><p>Reintentando automáticamente…</p></body></html>", context.RequestAborted);
void OpenEdge()
{
    var edge = new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) }
        .Select(p => Path.Combine(p, "Microsoft", "Edge", "Application", "msedge.exe")).FirstOrDefault(File.Exists);
    if (edge is null) return;
    var start = new ProcessStartInfo(edge) { UseShellExecute = false, CreateNoWindow = true };
    foreach (var argument in new[] { "--kiosk", localOrigin + "/", "--edge-kiosk-type=fullscreen", "--kiosk-idle-timeout-minutes=0", "--no-first-run", "--user-data-dir=" + Path.Combine(home, "Edge") }) start.ArgumentList.Add(argument);
    Process.Start(start);
}

sealed record ClientConfiguration(string ServerOrigin, string AccountCode);
sealed record DeviceState(Guid ScreenId, string Secret, string? ApprovalCode, bool Linked);
sealed record LinkResponse(Guid ScreenId, string ApprovalCode);
sealed record TicketResponse(string Ticket);
