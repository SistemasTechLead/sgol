using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.RegularExpressions;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using Sgol.BuildingBlocks.Time;
using Sgol.Identity.Contracts;
using Sgol.Web.Infrastructure.Authentication;
using Sgol.Web.Infrastructure.Persistence;
using Sgol.Web.Infrastructure.Persistence.Bootstrap;
using Sgol.Web.Infrastructure.Persistence.Identity;

if (args.Contains("--keyring-recovery")) { FileKeyringChecks.Run(); return; }
var root = Path.GetFullPath(Environment.GetEnvironmentVariable("SGOL_SECURITY_ROOT") ?? ".");
var toolBuild = Environment.GetEnvironmentVariable("SGOL_SECURITY_BUILD_ROOT");
var output = Path.Combine(root, "artifacts/security-audit/implementation");
Directory.CreateDirectory(output);
var temporaryRoot = Path.Combine(output, "temporary"); Directory.CreateDirectory(temporaryRoot);
Environment.SetEnvironmentVariable("TMP", temporaryRoot); Environment.SetEnvironmentVariable("TEMP", temporaryRoot);
var adminSettings = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("SGOL_MAINTENANCE_CONNECTION"));
if (adminSettings.Host != "127.0.0.1" || adminSettings.Port != 5549 || adminSettings.Database != "postgres")
    throw new Exception("Security checks require the dedicated PostgreSQL instance on 5549.");
var name = "sgol_task_checks_security_" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
var results = new List<object>();
void Check(bool passed, string scenario) { results.Add(new { scenario, passed }); Console.WriteLine($"{(passed ? "PASS" : "FAIL")} {scenario}"); if (!passed) throw new Exception(scenario); }
await using (var admin = new NpgsqlConnection(adminSettings.ConnectionString))
{
    await admin.OpenAsync();
    await using var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin);
    await create.ExecuteNonQueryAsync();
}
adminSettings.Database = name;
var maintenance = adminSettings.ConnectionString;
var environment = new Dictionary<string, string?>
{
    ["SGOL_MAINTENANCE_CONNECTION"] = maintenance,
    ["SGOL_MIGRATION_PASSWORD"] = Environment.GetEnvironmentVariable("SGOL_MIGRATION_PASSWORD"),
    ["SGOL_RUNTIME_PASSWORD"] = Environment.GetEnvironmentVariable("SGOL_RUNTIME_PASSWORD")
};
await Run("dotnet", [toolBuild is null ? Path.Combine(root, "tools/Sgol.DatabaseSecurity/bin/Debug/net10.0/Sgol.DatabaseSecurity.dll") : Path.Combine(toolBuild, "bin/Sgol.DatabaseSecurity/debug/Sgol.DatabaseSecurity.dll")], environment);
adminSettings.Username = name + "_runtime"; adminSettings.Password = environment["SGOL_RUNTIME_PASSWORD"];
var runtime = adminSettings.ConnectionString;
using var rsa = RSA.Create(2048);
var certificateRequest = new CertificateRequest("CN=SGOL isolated security checks", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
using var wrapping = certificateRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(2));
var wrappingPassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
var wrappingBase64 = Convert.ToBase64String(wrapping.Export(X509ContentType.Pfx, wrappingPassword));
var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
{
    ["ConnectionStrings:Sgol"] = runtime, ["Authentication:RequireMfa"] = "false",
    ["DataProtection:ApplicationName"] = name, ["DataProtection:WrappingCertificate"] = wrappingBase64,
    ["DataProtection:WrappingCertificatePassword"] = wrappingPassword
}).Build();
var services = new ServiceCollection(); services.AddLogging(b => b.SetMinimumLevel(LogLevel.Error));
services.AddSingleton<IConfiguration>(configuration); services.AddSgolHostedAuthentication(); services.AddSgolPersistence(configuration);
var clock = new TestClock(); services.AddSingleton<IClock>(clock);
await using var provider = services.BuildServiceProvider();
var initial = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
Guid userId;
await using (var scope = provider.CreateAsyncScope())
{
    var seed = await scope.ServiceProvider.GetRequiredService<DirectionBootstrapService>().ExecuteAsync(new()
    { PersonStableCode = "SECURITY-CHECK", PersonDisplayName = "Security fixture", UserName = "security.fixture", InitialPassword = initial });
    userId = seed.UserId;
}
var s3Password = Environment.GetEnvironmentVariable("SGOL_SECURITY_S3_PASSWORD")!;
using var s3 = new AmazonS3Client("security-maintenance", s3Password, new AmazonS3Config
{ ServiceURL = "http://127.0.0.1:8339", ForcePathStyle = true, AuthenticationRegion = "us-east-1", MaxErrorRetry = 0 });
foreach (var bucket in new[] { "sgol-quarantine", "sgol-clean" })
    if (!((await s3.ListBucketsAsync()).Buckets ?? []).Any(b => b.BucketName == bucket)) await s3.PutBucketAsync(bucket);
// Actual full Web process; never map a subset of endpoints in a harness.
Process? web = null;
var keepBrowserRunning = false;
try
{
    web = await StartWeb(7287, false);
    using var a = new Browser("https://localhost:7287");
    using var b = new Browser("https://localhost:7287");
    Check((int)(await a.Client.GetAsync("/api/v1/auth/session")).StatusCode == 401, "S01 anonymous rejected");
    Check((int)(await a.Post("/api/v1/auth/login", new { userName = "security.fixture", password = initial }, false)).StatusCode == 400, "CSRF login required");
    Check((await a.Post("/api/v1/auth/login", new { userName = "security.fixture", password = initial })).IsSuccessStatusCode, "HTTP password challenge");
    Check(a.Cookie is null, "No full session before password challenge completion");
    Check((await a.Post("/api/v1/auth/password/change", new { currentPassword = initial, newPassword = password })).IsSuccessStatusCode, "HTTP change password commits session");
    var copiedA = a.Cookie!;
    Check((await b.Post("/api/v1/auth/login", new { userName = "security.fixture", password })).IsSuccessStatusCode, "S01 independent session B");
    Check((int)(await a.Post("/api/v1/auth/logout", new { }, false)).StatusCode == 400, "CSRF logout required");
    Check((int)(await a.Post("/api/v1/auth/logout", new { })).StatusCode == 204, "S01 logout A committed");
    Check(await Replay(copiedA) == 401, "S01 copied cookie A rejected");
    Check((await b.Client.GetAsync("/api/v1/auth/session")).IsSuccessStatusCode, "S01 session B survives");
    await using (var scope = provider.CreateAsyncScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<SgolDbContext>();
        Check(await db.PersonalSessions.CountAsync(x => x.UserId == userId && x.RevokedAt != null) == 1 &&
            await db.AuditEvents.CountAsync(x => x.Action == "AUTH_LOGOUT") == 1, "S01 one revocation and matching audit");
        var user = await db.AppUsers.SingleAsync(x => x.Id == userId);
        var original = b.Cookie!;
        // Create protected legacy/forged tickets using the actual keyring and scheme.
        var ticketOptions = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(HostedAuthenticationDefaults.Scheme);
        var principal = ticketOptions.TicketDataFormat.Unprotect(original)!.Principal;
        var identity = new ClaimsIdentity(principal.Identity);
        identity.RemoveClaim(identity.FindFirst(SgolClaimTypes.SessionId)!);
        var oldCookie = ticketOptions.TicketDataFormat.Protect(new AuthenticationTicket(new ClaimsPrincipal(identity),
            new AuthenticationProperties { IssuedUtc = DateTimeOffset.UtcNow, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(1) }, HostedAuthenticationDefaults.Scheme));
        Check(await Replay(oldCookie) == 401, "S03 legacy cookie rejected without replacing keyring");
        var sid = AuthenticationPrincipalFactory.ReadSessionId(principal)!.Value;
        var aged = ticketOptions.TicketDataFormat.Unprotect(original)!;
        aged.Properties.IssuedUtc = DateTimeOffset.UtcNow.AddMinutes(-20);
        var beforeRenewal = ticketOptions.TicketDataFormat.Protect(aged);
        using var renewClient = new HttpClient(new HttpClientHandler { UseCookies = false });
        renewClient.DefaultRequestHeaders.Add("Cookie", HostedAuthenticationDefaults.SessionCookie + "=" + beforeRenewal);
        var renewedResponse = await renewClient.GetAsync("https://localhost:7287/api/v1/auth/session");
        var renewedCookie = renewedResponse.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith(HostedAuthenticationDefaults.SessionCookie + "="))
            .Split(';')[0][(HostedAuthenticationDefaults.SessionCookie.Length + 1)..];
        var renewedTicket = ticketOptions.TicketDataFormat.Unprotect(renewedCookie)!;
        Check(AuthenticationPrincipalFactory.ReadSessionId(renewedTicket.Principal) == sid &&
            renewedTicket.Principal.FindFirstValue(SgolClaimTypes.AbsoluteExpiresAt) == principal.FindFirstValue(SgolClaimTypes.AbsoluteExpiresAt), "S02 renewal keeps session ID and absolute expiry");
        // Atomicity: force audit INSERT to fail only for logout in isolated PostgreSQL.
        await AdminSql("CREATE FUNCTION security_fail_logout() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.action='AUTH_LOGOUT' THEN RAISE EXCEPTION 'isolated audit fault'; END IF; RETURN NEW; END $$; CREATE TRIGGER security_fail_logout BEFORE INSERT ON audit_event FOR EACH ROW EXECUTE FUNCTION security_fail_logout();");
        Check((int)(await b.Post("/api/v1/auth/logout", new { })).StatusCode == 500, "S06 audit failure returns error");
        Check(await Replay(original) == 200, "S06 failed logout rolls back revocation");
        var formFailure = await b.Form("/mi-trabajo", new());
        Check((int)formFailure.StatusCode == 503 && (await formFailure.Content.ReadAsStringAsync()).Contains("No se pudo confirmar el cierre"), "S06 real Razor logout renders safe retry error");
        Check(b.Cookie == original && await Replay(original) == 200, "S06 real Razor failure preserves cookie and permits retry");
        await AdminSql("DROP TRIGGER security_fail_logout ON audit_event; DROP FUNCTION security_fail_logout();");
        // Races are tested through independent service scopes against the same persisted row.
        await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            await using var retry = provider.CreateAsyncScope();
            await retry.ServiceProvider.GetRequiredService<IHostedAuthenticationService>().RecordLogoutAsync(userId, sid, Guid.NewGuid());
        }));
        Check(await Replay(original) == 401 && await Replay(renewedCookie) == 401, "S02 concurrent revocation rejects original and renewed tickets");
        Check(await db.AuditEvents.CountAsync(x => x.Action == "AUTH_LOGOUT") == 2, "S06 retries append exactly one audit");
        var auth = scope.ServiceProvider.GetRequiredService<IHostedAuthenticationService>();
        var fresh = (await auth.LoginAsync("security.fixture", password, Guid.NewGuid())).Session!;
        var start = fresh.MfaAuthenticatedAt;
        clock.UtcNow = start.AddHours(23);
        Check(await auth.ValidateSessionAsync(userId, fresh.SecurityStamp, start, fresh.AbsoluteExpiresAt, sessionId: fresh.SessionId) != null, "S07 survives 23 hours idle");
        clock.UtcNow = start.AddDays(1);
        Check(await auth.ValidateSessionAsync(userId, fresh.SecurityStamp, start, fresh.AbsoluteExpiresAt, sessionId: fresh.SessionId) == null, "S07 expires exactly at absolute 24h");
        clock.UtcNow = DateTimeOffset.UtcNow;
        Check(await auth.ValidateSessionAsync(userId, fresh.SecurityStamp, start, fresh.AbsoluteExpiresAt, sessionId: Guid.NewGuid()) == null, "Missing persisted session rejected");
        Check(await auth.ValidateSessionAsync(userId, "bad-stamp", start, fresh.AbsoluteExpiresAt, sessionId: fresh.SessionId) == null, "S04 changed security stamp rejected");
    }
    foreach (var sql in new[] { "CREATE ROLE forbidden_security_role", "CREATE DATABASE forbidden_security_db", "CREATE TABLE public.forbidden_security_table(id int)",
        "ALTER TABLE app_user ADD COLUMN forbidden int", "DROP TABLE personal_session", $"SET ROLE \"{name}_migrator\"", "DELETE FROM audit_event", "TRUNCATE audit_event", "DELETE FROM data_protection_key", "DELETE FROM personal_session" })
    {
        await using var connection = new NpgsqlConnection(runtime); await connection.OpenAsync();
        try { await using var command = new NpgsqlCommand(sql, connection); await command.ExecuteNonQueryAsync(); Check(false, "D01 privileged operation must fail"); }
        catch (PostgresException ex) { Check(ex.SqlState == "42501", "D01 runtime denies " + sql.Split(' ')[0] + " (" + sql.Split(' ')[1] + ")"); }
    }
    // Fresh full process gives bounded attack tests their own rate-limit counters.
    web.Kill(true); await web.WaitForExitAsync(); web.Dispose(); web = await StartWeb(7287, false);
    using var attacker = new Browser("https://localhost:7287");
    foreach (var payload in new[] { "' OR '1'='1'--", "';SELECT pg_sleep(2)--", "\" OR 1=1/*", "x' UNION SELECT NULL--" })
        Check((int)(await attacker.Post("/api/v1/auth/login", new { userName = payload, password = "Synthetic invalid password 2026!" })).StatusCode == 401, "I01 SQLi login data rejected");
    using var director = new Browser("https://localhost:7287");
    await director.Post("/api/v1/auth/login", new { userName = "security.fixture", password });
    foreach (var path in new[] { "/tareas?search=%27%20OR%201%3D1--", "/reportes?F.Task=%27%3BDROP%20TABLE%20app_user--", "/mi-trabajo?WorkSearch=%27%20OR%201%3D1--" })
    {
        var response = await director.Client.GetAsync(path); var text = await response.Content.ReadAsStringAsync();
        Check((int)response.StatusCode < 500 && !text.Contains("Npgsql.PostgresException"), "I01 real Razor filter " + path.Split('?')[0]);
    }
    await OperationalHttpChecks.RunAsync(provider, userId, director, Check);
    await ScreenChecks.RunAsync(provider, userId, Check);
    // Genuine Razor login form and bridge, including protected flow and antiforgery cookies.
    using var form = new Browser("https://localhost:7287");
    var html = await form.Client.GetStringAsync("/acceso");
    var token = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
    Check(token.Length > 20, "Real Razor login form issues antiforgery token");
    var formResponse = await form.Client.PostAsync("/acceso", new FormUrlEncodedContent(new Dictionary<string, string>
    { ["__RequestVerificationToken"] = token, ["stepAction"] = "login", ["userName"] = "security.fixture", ["password"] = password }));
    Check((int)formResponse.StatusCode == 302 && form.Cookie != null, "Real Razor login form reaches API/services and issues cookie");
    var oldRoleCookie = director.Cookie!;
    await AdminSql($"UPDATE role_assignment_version SET role_code='PISO_VENTAS' WHERE user_id='{userId}' AND valid_to IS NULL;");
    foreach (var path in new[] { "/api/v1/users", "/api/v1/people", "/api/v1/direction-plans" })
        Check((int)(await director.Client.GetAsync(path)).StatusCode == 403, "I02 current role denies " + path);
    await AdminSql($"UPDATE role_assignment_version SET role_code='DIRECCION' WHERE user_id='{userId}' AND valid_to IS NULL;");
    // Readiness goes through the real registered PostgreSQL, keyring and S3 checks.
    using var monitor = new HttpClient { BaseAddress = new Uri("https://localhost:7287") };
    Check((await monitor.GetAsync("/health/ready")).IsSuccessStatusCode, "H02 full readiness PostgreSQL/DataProtection/S3");
    using (var spoof = new HttpRequestMessage(HttpMethod.Get, "/health/ready"))
    { spoof.Headers.Add("X-Forwarded-For", "127.0.0.1"); Check((int)(await monitor.SendAsync(spoof)).StatusCode == 403, "H01 spoofed readiness origin denied"); }
    var burst = await Task.WhenAll(Enumerable.Range(0, 64).Select(_ => monitor.GetAsync("/health/ready")));
    Check(burst.All(r => r.IsSuccessStatusCode), "H02 bounded monitor burst remains healthy"); foreach (var r in burst) r.Dispose();
    using (var post = new HttpClient())
        Check((int)(await post.PostAsync("http://127.0.0.1:8339/sgol-quarantine", new MultipartFormDataContent())).StatusCode == 405, "O02 storage POST Object blocked");
    await s3.PutObjectAsync(new PutObjectRequest { BucketName = "sgol-clean", Key = "security-recovery", ContentBody = "isolated evidence" });
    using (var obj = await s3.GetObjectAsync("sgol-clean", "security-recovery"))
        Check(await new StreamReader(obj.ResponseStream).ReadToEndAsync() == "isolated evidence", "O03 storage PUT/GET valid");
    await StorageChecks.RunAsync(s3, Environment.GetEnvironmentVariable("SGOL_SECURITY_RUNTIME_S3_PASSWORD")!, provider, Check);
    // Test MFA in a second profile, leaving the daily Development setting unchanged.
    web.Kill(true); await web.WaitForExitAsync(); web.Dispose(); web = await StartWeb(7287, true);
    using var mfa = new Browser("https://localhost:7287");
    Check((await mfa.Post("/api/v1/auth/login", new { userName = "security.fixture", password })).IsSuccessStatusCode && mfa.Cookie == null, "S05 MFA challenge creates no personal cookie");
    var enrollment = await (await mfa.Post("/api/v1/auth/mfa/enroll", new { })).Content.ReadFromJsonAsync<JsonElement>();
    var secret = enrollment.GetProperty("data").GetProperty("manualKey").GetString()!;
    var totp = Totp(secret, DateTimeOffset.UtcNow);
    var confirmed = await mfa.Post("/api/v1/auth/mfa/confirm", new { totpCode = totp });
    Check(confirmed.IsSuccessStatusCode && mfa.Cookie != null, "S05 MFA enrollment commits personal session");
    var completed = await confirmed.Content.ReadFromJsonAsync<JsonElement>();
    var recovery = completed.GetProperty("data").GetProperty("recoveryCodes")[0].GetString()!;
    using var mfaReplay = new Browser("https://localhost:7287");
    await mfaReplay.Post("/api/v1/auth/login", new { userName = "security.fixture", password });
    Check((int)(await mfaReplay.Post("/api/v1/auth/mfa/verify", new { totpCode = totp })).StatusCode == 401, "I04 TOTP same step replay rejected");
    var recovered = await mfaReplay.Post("/api/v1/auth/mfa/verify", new { recoveryCode = recovery });
    Check(recovered.IsSuccessStatusCode && mfaReplay.Cookie == null, "I04 recovery cannot issue full cookie before regeneration");
    Check((int)(await mfaReplay.Client.GetAsync("/api/v1/auth/session")).StatusCode == 401, "I04 incomplete recovery cannot authorize API");
    Check((await mfaReplay.Post("/api/v1/auth/recovery-codes/regenerate", new { currentPassword = password })).IsSuccessStatusCode && mfaReplay.Cookie != null, "I04 regeneration completes authentication");
    using var reused = new Browser("https://localhost:7287");
    await reused.Post("/api/v1/auth/login", new { userName = "security.fixture", password });
    Check((int)(await reused.Post("/api/v1/auth/mfa/verify", new { recoveryCode = recovery })).StatusCode == 401, "I04 consumed recovery code rejected");
    Check(await Replay(oldRoleCookie) == 401, "S04 MFA regeneration invalidates older stamp");
    using var expiredChallenge = new Browser("https://localhost:7287");
    await expiredChallenge.Post("/api/v1/auth/login", new { userName = "security.fixture", password });
    await AdminSql($"UPDATE authentication_challenge SET created_at=now()-interval '15 minutes', expires_at=now()-interval '1 minute' WHERE id=(SELECT id FROM authentication_challenge WHERE user_id='{userId}' ORDER BY created_at DESC LIMIT 1)");
    var expiredResponse = await expiredChallenge.Post("/api/v1/auth/mfa/verify", new { totpCode = "000000" });
    var expiredBody = await expiredResponse.Content.ReadAsStringAsync();
    Check((int)expiredResponse.StatusCode == 401 && expiredBody.Contains("DESAFIO_INVALIDO"), "I04 expired MFA challenge rejected before code verification");
    using var concurrentMfa = new Browser("https://localhost:7287");
    await concurrentMfa.Post("/api/v1/auth/login", new { userName = "security.fixture", password });
    var nextStepDelay = (31 - DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 30) * 1000;
    await Task.Delay((int)nextStepDelay);
    var concurrentCode = Totp(secret, DateTimeOffset.UtcNow);
    var csrfForRace = await concurrentMfa.Client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf");
    var raceCookies = concurrentMfa.CookieHeader;
    async Task<int> MfaRace()
    {
        using var raceClient = new HttpClient(new HttpClientHandler { UseCookies = false });
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://localhost:7287/api/v1/auth/mfa/verify") { Content = JsonContent.Create(new { totpCode = concurrentCode }) };
        request.Headers.Add("Cookie", raceCookies); request.Headers.Add("X-CSRF-TOKEN", csrfForRace.GetProperty("data").GetProperty("requestToken").GetString());
        using var response = await raceClient.SendAsync(request); return (int)response.StatusCode;
    }
    var raceStatuses = (await Task.WhenAll(MfaRace(), MfaRace())).Order().ToArray();
    Check(raceStatuses.SequenceEqual(new[] { 200, 401 }), "I04 concurrent MFA completion authenticates exactly once");
    var throttled = 0;
    await Parallel.ForEachAsync(Enumerable.Range(0, 650), new ParallelOptions { MaxDegreeOfParallelism = 4 }, async (_, ct) =>
    {
        using var response = await monitor.GetAsync("/api/v1/auth/csrf", ct);
        if ((int)response.StatusCode == 429 && response.Headers.RetryAfter != null) Interlocked.Increment(ref throttled);
    });
    Check(throttled > 0 && (await monitor.GetAsync("/health/live")).IsSuccessStatusCode, "H03 bounded 650-request attack receives 429/Retry-After; liveness survives");
    using (var slowServer = await StartWeb(7288, false, timeoutSeconds: 2))
    {
        try
        {
            using var tcp = new System.Net.Sockets.TcpClient(); await tcp.ConnectAsync("localhost", 7288);
            using var ssl = new System.Net.Security.SslStream(tcp.GetStream()); await ssl.AuthenticateAsClientAsync("localhost");
            using var slowBrowser = new Browser("https://localhost:7288");
            var slowCsrf = await slowBrowser.Client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf");
            var partialBody = "__RequestVerificationToken=" + Uri.EscapeDataString(slowCsrf.GetProperty("data").GetProperty("requestToken").GetString()!) + "&stepAction=login&userName=slow-fixture&password=x";
            var header = System.Text.Encoding.ASCII.GetBytes("POST /acceso HTTP/1.1\r\nHost: localhost:7288\r\nContent-Type: application/x-www-form-urlencoded\r\nCookie: " + slowBrowser.CookieHeader + "\r\nContent-Length: " + (partialBody.Length + 1024) + "\r\nConnection: close\r\n\r\n" + partialBody);
            var slowWatch = Stopwatch.StartNew();
            await ssl.WriteAsync(header); await ssl.FlushAsync();
            var responseBytes = new byte[8192]; using var slowBudget = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            var read = await ssl.ReadAsync(responseBytes, slowBudget.Token);
            var responseText = System.Text.Encoding.ASCII.GetString(responseBytes, 0, read);
            Check((responseText.StartsWith("HTTP/1.1 503") || responseText.StartsWith("HTTP/1.1 400")) && slowWatch.Elapsed >= TimeSpan.FromSeconds(1.5) && slowWatch.Elapsed < TimeSpan.FromSeconds(5),
                "H03 valid-CSRF slow form stopped by 2s deadline: " + responseText.Split('\r')[0]);
            using var healthyTraffic = new HttpClient();
            Check((await healthyTraffic.GetAsync("https://localhost:7288/health/live")).IsSuccessStatusCode, "H03 legitimate liveness recovers after slow client");
        }
        finally { slowServer.Kill(true); await slowServer.WaitForExitAsync(); }
    }
    if (args.Contains("--hold-browser"))
    {
        web.Kill(true); await web.WaitForExitAsync(); web.Dispose(); web = await StartWeb(7287, false);
        await File.WriteAllTextAsync(Path.Combine(output, "browser-fixture.private.json"), JsonSerializer.Serialize(new { userName = "security.fixture", password, processId = web.Id }));
        var release = Path.Combine(output, "release-browser");
        if (File.Exists(release)) File.Delete(release);
        keepBrowserRunning = true;
    }
}
finally
{
    if (!keepBrowserRunning && web is { HasExited: false }) { web.Kill(true); await web.WaitForExitAsync(); }
    if (!keepBrowserRunning) web?.Dispose();
    await File.WriteAllTextAsync(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new { database = name, results }, new JsonSerializerOptions { WriteIndented = true }));
}
await ReadinessChecks.RunAsync(Check);
await File.WriteAllTextAsync(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new { database = name, results }, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Security checks passed: {results.Count}; isolated database retained: {name}");
if (keepBrowserRunning)
{
    Console.WriteLine("Isolated browser verification ready on https://localhost:7287; waiting for release-browser marker.");
    while (!File.Exists(Path.Combine(output, "release-browser"))) await Task.Delay(500);
    if (web is { HasExited: false }) { web.Kill(true); await web.WaitForExitAsync(); }
    web?.Dispose();
}

async Task AdminSql(string sql) { await using var c = new NpgsqlConnection(maintenance); await c.OpenAsync(); await using var cmd = new NpgsqlCommand(sql, c); await cmd.ExecuteNonQueryAsync(); }
async Task<int> Replay(string cookie)
{
    using var c = new HttpClient(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false });
    c.DefaultRequestHeaders.Add("Cookie", HostedAuthenticationDefaults.SessionCookie + "=" + cookie);
    return (int)(await c.GetAsync("https://localhost:7287/api/v1/auth/session")).StatusCode;
}
async Task<Process> StartWeb(int port, bool requireMfa, int timeoutSeconds = 300)
{
    if (System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Any(x => x.Port == port))
        throw new Exception("Dedicated HTTP test port is already occupied; refusing to test an unknown process.");
    var start = new ProcessStartInfo("dotnet") { WorkingDirectory = Path.Combine(root, "src/Sgol.Web"), RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
    start.ArgumentList.Add(toolBuild is null ? Path.Combine(root, "src/Sgol.Web/bin/Debug/net10.0/Sgol.Web.dll") : Path.Combine(toolBuild, "bin/Sgol.Web/debug/Sgol.Web.dll"));
    foreach (var key in start.Environment.Keys.Where(k => k.StartsWith("SGOL_", StringComparison.Ordinal) || k.StartsWith("ConnectionStrings__", StringComparison.Ordinal) || k.StartsWith("Lan__", StringComparison.Ordinal)).ToList()) start.Environment.Remove(key);
    foreach (var (key, value) in new Dictionary<string, string>
    {
        ["DataProtection__ApplicationName"] = name, ["DataProtection__WrappingCertificate"] = wrappingBase64,
        ["DataProtection__WrappingCertificatePassword"] = wrappingPassword,
        ["ConnectionStrings__Sgol"] = runtime, ["ASPNETCORE_URLS"] = $"https://localhost:{port}",
        ["ASPNETCORE_ENVIRONMENT"] = "Development", ["DOTNET_ENVIRONMENT"] = "Development", ["Authentication__RequireMfa"] = requireMfa.ToString(),
        ["SgolApi__TrustedOrigin"] = $"https://localhost:{port}", ["ReverseProxy__KnownProxy"] = "",
        ["LocalShared__SecurityHeaders"] = "true", ["LocalShared__EvidenceOrigin"] = "https://localhost:8339",
        ["RequestLimits__TimeoutSeconds"] = timeoutSeconds.ToString(),
        ["Processes__OperationsEnabled"] = "false", ["Evidence__Storage__Endpoint"] = "http://127.0.0.1:8339",
        ["Evidence__Storage__Region"] = "us-east-1", ["Evidence__Storage__QuarantineBucket"] = "sgol-quarantine", ["Evidence__Storage__CleanBucket"] = "sgol-clean",
        ["Evidence__Storage__AccessKey"] = "security-runtime", ["Evidence__Storage__SecretKey"] = Environment.GetEnvironmentVariable("SGOL_SECURITY_RUNTIME_S3_PASSWORD")!,
        ["Evidence__Storage__AllowedUploadOrigins__0"] = $"https://localhost:{port}", ["Evidence__Storage__AllowInsecureTransport"] = "true",
        ["Evidence__Scanner__Host"] = "127.0.0.1", ["Evidence__Scanner__Port"] = "3319", ["Logging__LogLevel__Default"] = "Warning"
    }) start.Environment[key] = value;
    var process = Process.Start(start)!;
    var log = new StreamWriter(Path.Combine(output, $"web-{DateTime.UtcNow:HHmmssfff}.log")) { AutoFlush = true };
    var logGate = new object();
    process.OutputDataReceived += (_, e) => { if (e.Data != null) lock (logGate) log.WriteLine(e.Data); };
    process.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (logGate) log.WriteLine(e.Data); };
    process.BeginOutputReadLine(); process.BeginErrorReadLine();
    using var probe = new HttpClient();
    for (var i = 0; i < 60; i++)
    {
        if (process.HasExited) throw new Exception("Full Web process exited; inspect isolated log.");
        try { if ((await probe.GetAsync($"https://localhost:{port}/health/live")).IsSuccessStatusCode) return process; } catch (HttpRequestException) { }
        await Task.Delay(250);
    }
    process.Kill(true); throw new Exception("Full Web process did not start.");
}
async Task Run(string program, string[] arguments, Dictionary<string, string?> env)
{
    var info = new ProcessStartInfo(program) { UseShellExecute = false, CreateNoWindow = true };
    foreach (var arg in arguments) info.ArgumentList.Add(arg);
    foreach (var (key, value) in env) info.Environment[key] = value;
    using var process = Process.Start(info)!; await process.WaitForExitAsync();
    if (process.ExitCode != 0) throw new Exception("Isolated database security preparation failed.");
}
static string Totp(string base32, DateTimeOffset now)
{
    const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    var bits = 0; var count = 0; var bytes = new List<byte>();
    foreach (var c in base32.Replace(" ", "").TrimEnd('=').ToUpperInvariant()) { bits = (bits << 5) | alphabet.IndexOf(c); count += 5; if (count >= 8) { count -= 8; bytes.Add((byte)(bits >> count)); } }
    var data = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(now.ToUnixTimeSeconds() / 30));
    var hash = HMACSHA1.HashData(bytes.ToArray(), data); var offset = hash[^1] & 15;
    var number = ((hash[offset] & 127) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
    return (number % 1000000).ToString("D6");
}
sealed class TestClock : IClock { public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow; }
sealed class Browser : IDisposable
{
    public HttpClient Client { get; }
    private readonly CookieContainer cookies = new();
    public string CookieHeader => cookies.GetCookieHeader(Client.BaseAddress!);
    public string? Cookie => cookies.GetCookies(Client.BaseAddress!)[HostedAuthenticationDefaults.SessionCookie]?.Value;
    public Browser(string origin) => Client = new(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = cookies }) { BaseAddress = new Uri(origin) };
    public async Task<HttpResponseMessage> Post(string path, object body, bool csrf = true)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        if (csrf) { var token = await Client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf"); request.Headers.Add("X-CSRF-TOKEN", token.GetProperty("data").GetProperty("requestToken").GetString()); }
        return await Client.SendAsync(request);
    }
    public async Task<HttpResponseMessage> Form(string path, Dictionary<string, string> values)
    {
        var token = await Client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf");
        values["__RequestVerificationToken"] = token.GetProperty("data").GetProperty("requestToken").GetString()!;
        return await Client.PostAsync(path, new FormUrlEncodedContent(values));
    }
    public void Dispose() => Client.Dispose();
}
