using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Sgol.Identity.Contracts;

namespace Sgol.Cv05Demo;

internal sealed record DemoAccount(
    Guid UserId,
    Guid PersonId,
    string UserName,
    string RoleCode,
    string TemporaryPassword,
    string NewPassword,
    string StableCode);

internal sealed record HostedSession(DemoAccount Account, HttpClient Client, string Csrf);

internal static class HostedAuthenticationClient
{
    public static async Task<HostedSession> CompleteFirstAccessAsync(
        HttpClient client,
        DemoAccount account,
        CancellationToken cancellationToken)
    {
        var csrf = await GetCsrfAsync(client, cancellationToken);
        using (var login = await PostAsync(client, "/api/v1/auth/login",
            new { userName = account.UserName, password = account.TemporaryPassword }, csrf, cancellationToken))
        {
            Require(login.StatusCode == HttpStatusCode.OK);
            Require(await DataStringAsync(login, "nextStep", cancellationToken) == AuthenticationNextStep.ChangePassword);
        }
        using (var changed = await PostAsync(client, "/api/v1/auth/password/change",
            new { currentPassword = account.TemporaryPassword, newPassword = account.NewPassword }, csrf, cancellationToken))
        {
            Require(changed.StatusCode == HttpStatusCode.OK);
            Require(await DataStringAsync(changed, "nextStep", cancellationToken) == AuthenticationNextStep.EnrollMfa);
        }
        string manualKey;
        using (var enrollment = await PostAsync(client, "/api/v1/auth/mfa/enroll", new { }, csrf, cancellationToken))
        {
            Require(enrollment.StatusCode == HttpStatusCode.OK);
            manualKey = await DataStringAsync(enrollment, "manualKey", cancellationToken);
        }
        using (var confirmed = await PostAsync(client, "/api/v1/auth/mfa/confirm",
            new { totpCode = ComputeTotp(manualKey, DateTimeOffset.UtcNow) }, csrf, cancellationToken))
        {
            Require(confirmed.StatusCode == HttpStatusCode.OK);
            using var document = JsonDocument.Parse(await confirmed.Content.ReadAsStringAsync(cancellationToken));
            Require(document.RootElement.GetProperty("data").GetProperty("recoveryCodes").GetArrayLength() == 10);
        }
        csrf = await GetCsrfAsync(client, cancellationToken);
        using (var state = await client.GetAsync("/api/v1/auth/session", cancellationToken))
        {
            Require(state.StatusCode == HttpStatusCode.OK);
        }
        return new HostedSession(account, client, csrf);
    }

    public static async Task<DemoAccount> ProvisionAccountAsync(
        HostedSession direction,
        string stableCode,
        string roleCode,
        CancellationToken cancellationToken)
    {
        var temporaryPassword = Cv05Infrastructure.NewPassword();
        var newPassword = Cv05Infrastructure.NewPassword();
        var userName = $"{stableCode.ToLowerInvariant()}.{Guid.CreateVersion7():N}";
        Guid personId;
        using (var person = await PostAsync(direction.Client, "/api/v1/people",
            new { stableCode, displayName = $"Persona sintética {stableCode}" }, direction.Csrf,
            cancellationToken, Guid.CreateVersion7()))
        {
            Require(person.StatusCode == HttpStatusCode.Created);
            personId = await DataGuidAsync(person, "id", cancellationToken);
        }
        Guid userId;
        using (var account = await PostAsync(direction.Client, "/api/v1/users",
            new { personId, userName, temporaryPassword }, direction.Csrf,
            cancellationToken, Guid.CreateVersion7()))
        {
            Require(account.StatusCode == HttpStatusCode.Created);
            userId = await DataGuidAsync(account, "id", cancellationToken);
        }
        using (var role = await PostAsync(direction.Client, $"/api/v1/users/{userId:D}/role-assignments",
            new { roleCode, reason = "Provisión sintética TECH-E2E-CV-05" }, direction.Csrf,
            cancellationToken, Guid.CreateVersion7()))
        {
            Require(role.StatusCode == HttpStatusCode.OK);
        }
        return new DemoAccount(userId, personId, userName, roleCode, temporaryPassword, newPassword, stableCode);
    }

    public static async Task<string> GetCsrfAsync(HttpClient client, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync("/api/v1/auth/csrf", cancellationToken);
        Require(response.StatusCode == HttpStatusCode.OK);
        return await DataStringAsync(response, "requestToken", cancellationToken);
    }

    public static Task<HttpResponseMessage> PostAsync(
        HttpClient client,
        string path,
        object body,
        string? csrf,
        CancellationToken cancellationToken,
        Guid? idempotencyKey = null,
        string? ifMatch = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        AddHeaders(request, csrf, idempotencyKey, ifMatch);
        return client.SendAsync(request, cancellationToken);
    }

    public static Task<HttpResponseMessage> PutAsync(
        HttpClient client,
        string path,
        object body,
        string? csrf,
        CancellationToken cancellationToken,
        Guid? idempotencyKey = null,
        string? ifMatch = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, path) { Content = JsonContent.Create(body) };
        AddHeaders(request, csrf, idempotencyKey, ifMatch);
        return client.SendAsync(request, cancellationToken);
    }

    public static async Task<string?> ProblemCodeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return document.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    public static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

    public static async Task<Guid> DataGuidAsync(HttpResponseMessage response, string property, CancellationToken cancellationToken)
    {
        var value = await DataStringAsync(response, property, cancellationToken);
        return Guid.ParseExact(value, "D");
    }

    public static async Task<string> DataStringAsync(HttpResponseMessage response, string property, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return document.RootElement.GetProperty("data").GetProperty(property).GetString()!;
    }

    public static void Require(bool condition)
    {
        if (!condition)
        {
            throw new DemoFailureException("LOGIN", "NONE", "CV05_AUTH_FAILED");
        }
    }

    private static void AddHeaders(
        HttpRequestMessage request,
        string? csrf,
        Guid? idempotencyKey,
        string? ifMatch)
    {
        if (csrf is not null)
        {
            request.Headers.Add("X-CSRF-TOKEN", csrf);
        }
        if (idempotencyKey is Guid key)
        {
            request.Headers.Add("Idempotency-Key", key.ToString("D"));
        }
        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }
    }

    private static string ComputeTotp(string base32Secret, DateTimeOffset instant)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var output = new List<byte>();
        var buffer = 0;
        var bitsLeft = 0;
        foreach (var character in base32Secret)
        {
            buffer = (buffer << 5) | alphabet.IndexOf(character);
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                output.Add((byte)(buffer >> (bitsLeft - 8)));
                bitsLeft -= 8;
            }
        }
        Span<byte> counter = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, instant.ToUnixTimeSeconds() / 30);
#pragma warning disable CA5350
        var hash = HMACSHA1.HashData(output.ToArray(), counter);
#pragma warning restore CA5350
        var offset = hash[^1] & 0x0f;
        var binary = ((hash[offset] & 0x7f) << 24) | (hash[offset + 1] << 16) |
            (hash[offset + 2] << 8) | hash[offset + 3];
        return (binary % 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }
}
