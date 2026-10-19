using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Sgol.Web.Presentation.MyWork;

public sealed record EvidenceIntention(Guid Actor, Guid Obligation, string Requirement, string Operation,
    Guid Key, Guid CompleteKey, Guid ContributionKey, DateTimeOffset ExpiresAt, JsonElement Body, Guid? FileId = null,
    Guid? ReplacementItem = null, long? ReplacementRowVersion = null, int? ReplacementVersionNo = null, string? Reason = null)
{
    public override string ToString() => "EvidenceIntention";
}

public sealed class EvidenceIntentionProtector(IDataProtectionProvider provider)
{
    private readonly IDataProtector protector = provider.CreateProtector("SGOL.FRONT-017.evidence-intention.v1");
    public string Protect(EvidenceIntention intention) => protector.Protect(JsonSerializer.Serialize(intention));
    public EvidenceIntention? Read(string token, Guid actor, Guid obligation, string operation, DateTimeOffset now)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 24000) return null;
        try
        {
            var value = JsonSerializer.Deserialize<EvidenceIntention>(protector.Unprotect(token));
            return value is not null && value.Actor == actor && value.Obligation == obligation && value.Operation == operation &&
                value.Key != Guid.Empty && value.CompleteKey != Guid.Empty && value.ContributionKey != Guid.Empty && value.ExpiresAt > now ? value : null;
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException) { return null; }
    }
}
