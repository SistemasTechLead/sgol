using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;

namespace Sgol.Web.Presentation.Auditing;

public sealed class AuditReturnContext(IDataProtectionProvider provider, Guid actor)
{
    private sealed record Destination(Guid Actor, Guid Event, string Href);
    private readonly ITimeLimitedDataProtector protector = provider.CreateProtector("SGOL.FRONT-020.audit.return.v1").ToTimeLimitedDataProtector();
    public string Protect(AuditQuery query, Guid id) => protector.Protect(JsonSerializer.Serialize(new Destination(actor, id, query.Href(query.Value("cursor")))), TimeSpan.FromMinutes(30));
    public string Read(string? token, Guid id)
    {
        if (token is null || token.Length > 60000) return "/auditoria";
        try
        {
            var value = JsonSerializer.Deserialize<Destination>(protector.Unprotect(token));
            if (value is not null && value.Actor == actor && value.Event == id && value.Href.StartsWith("/auditoria?", StringComparison.Ordinal) &&
                AuditQuery.TryRead(new QueryCollection(QueryHelpers.ParseQuery(value.Href[10..])), out _)) return value.Href + "#event-" + id.ToString("D");
        }
        catch (Exception e) when (e is CryptographicException or JsonException) { }
        return "/auditoria";
    }
}
