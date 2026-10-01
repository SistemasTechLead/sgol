using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Sgol.Web.Presentation.Validation;

public sealed record ValidationIntention(Guid Actor, Guid Obligation, Guid? Decision, Guid Key, string ETag, string Result,
    string Foundation, string? Reason, string? EscalationReason, DateTimeOffset ExpiresAt)
{
    public string Path => Decision is { } id ? $"/api/v1/validation-decisions/{id:D}/replacements" : $"/api/v1/obligations/{Obligation:D}/validation-decisions";
    public object Body => Decision is not null ? new { Result, Foundation, Reason } : (object)new { Result, Foundation, EscalationReason };
    public override string ToString() => "ValidationIntention";
}
public sealed class ValidationIntentionProtector(IDataProtectionProvider provider)
{
    private readonly IDataProtector protector = provider.CreateProtector("SGOL.FRONT-019.intention.v1");
    public string Protect(ValidationIntention value) => protector.Protect(JsonSerializer.Serialize(value));
    public ValidationIntention? Read(string? token, Guid actor, Guid obligation, DateTimeOffset now)
    {
        if (token is null || token.Length > 24000) return null;
        try
        {
            var value = JsonSerializer.Deserialize<ValidationIntention>(protector.Unprotect(token));
            return value is not null && value.Actor == actor && value.Obligation == obligation && value.Key != Guid.Empty &&
                value.Decision != Guid.Empty && value.ExpiresAt > now && ValidationPresentation.StrongEtag(value.ETag) ? value : null;
        }
        catch (Exception e) when (e is CryptographicException or JsonException) { return null; }
    }
}
