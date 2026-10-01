using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Sgol.Continuity.Contracts;

namespace Sgol.Web.Presentation.Continuity;

public sealed record ContinuityIntention(Guid Actor, Guid? Resource, Guid Key, string? ETag, string Reason,
    DateTimeOffset ExpiresAt, RecoveryReconciliationDetails? Report)
{
    public string Path => Resource is { } id ? $"/api/v1/continuity/reconciliations/{id:D}/approval" : "/api/v1/continuity/reconciliations";
    public override string ToString() => "ContinuityIntention";
}
public sealed class ContinuityIntentionProtector(IDataProtectionProvider provider)
{
    private readonly IDataProtector protector = provider.CreateProtector("SGOL.FRONT-020.continuity.intention.v1");
    public string Protect(ContinuityIntention value) => protector.Protect(JsonSerializer.Serialize(value));
    public ContinuityIntention? Read(string? token, Guid actor, Guid? resource, DateTimeOffset now)
    {
        if (token is null || token.Length > 24000) return null;
        try
        {
            var value = JsonSerializer.Deserialize<ContinuityIntention>(protector.Unprotect(token));
            return value is not null && value.Actor == actor && value.Resource == resource && value.Key != Guid.Empty && value.ExpiresAt > now ? value : null;
        }
        catch (Exception e) when (e is CryptographicException or JsonException) { return null; }
    }
}
