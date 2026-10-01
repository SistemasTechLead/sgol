using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Sgol.Evidence.Contracts;

namespace Sgol.Web.Presentation.MyWork;

public sealed record ConclusionIntention(Guid Actor, Guid Obligation, Guid Key, string ETag, DateTimeOffset ExpiresAt)
{ public override string ToString() => "ConclusionIntention"; }
public sealed record ProtectedEvidenceReview(Guid Actor, DateTimeOffset ExpiresAt, EvidenceReviewDetails Review)
{ public override string ToString() => "ProtectedEvidenceReview"; }
public sealed class ConclusionIntentionProtector(IDataProtectionProvider provider)
{
    private readonly IDataProtector intent = provider.CreateProtector("SGOL.FRONT-018.conclusion.v1");
    private readonly IDataProtector review = provider.CreateProtector("SGOL.FRONT-018.review.v1");
    public string Protect(ConclusionIntention value) => intent.Protect(JsonSerializer.Serialize(value));
    public string Protect(ProtectedEvidenceReview value) => review.Protect(JsonSerializer.Serialize(value));
    public ConclusionIntention? Read(string token, Guid actor, Guid obligation, DateTimeOffset now)
    {
        var value = Read<ConclusionIntention>(intent, token);
        return value is not null && value.Actor == actor && value.Obligation == obligation && value.Key != Guid.Empty && value.ExpiresAt > now ? value : null;
    }
    public ProtectedEvidenceReview? ReadReview(string token, Guid actor, Guid obligation, DateTimeOffset now)
    {
        var value = Read<ProtectedEvidenceReview>(review, token);
        return value is not null && value.Actor == actor && value.Review?.ObligationId == obligation && value.ExpiresAt > now ? value : null;
    }
    private static T? Read<T>(IDataProtector protector, string token)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 24000) return default;
        try { return JsonSerializer.Deserialize<T>(protector.Unprotect(token)); }
        catch (Exception e) when (e is CryptographicException or JsonException) { return default; }
    }
}
