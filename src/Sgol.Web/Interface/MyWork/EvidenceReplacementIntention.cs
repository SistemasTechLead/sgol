using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Sgol.Web.Presentation.MyWork;

public sealed record EvidenceReplacementIntention(Guid Actor, Guid Obligation, Guid Item, string Requirement,
    long RowVersion, int VersionNo, DateTimeOffset ExpiresAt)
{ public override string ToString() => "EvidenceReplacementIntention"; }
public sealed class EvidenceReplacementIntentionProtector(IDataProtectionProvider provider)
{
    private readonly IDataProtector protector = provider.CreateProtector("SGOL.FRONT-018.replacement.v1");
    public string Protect(EvidenceReplacementIntention value) => protector.Protect(JsonSerializer.Serialize(value));
    public EvidenceReplacementIntention? Read(string token, Guid actor, Guid obligation, DateTimeOffset now)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 24000) return null;
        try
        {
            var v = JsonSerializer.Deserialize<EvidenceReplacementIntention>(protector.Unprotect(token));
            return v is not null && v.Actor == actor && v.Obligation == obligation && v.Item != Guid.Empty && v.RowVersion > 0 && v.VersionNo > 0 && v.ExpiresAt > now ? v : null;
        }
        catch (Exception e) when (e is CryptographicException or JsonException) { return null; }
    }
}
