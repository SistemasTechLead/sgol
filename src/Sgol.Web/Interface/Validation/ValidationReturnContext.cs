using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;

namespace Sgol.Web.Presentation.Validation;

public sealed class ValidationReturnContext(IDataProtectionProvider provider, Guid actor)
{
    private sealed record Context(Guid Actor, Dictionary<string, string?> Values, string Section, Guid Obligation);
    private readonly IDataProtector protector = provider.CreateProtector("SGOL.FRONT-019.return.v1");
    public string Protect(ValidationQuery query, string section, Guid obligation) => "v19." + protector.Protect(JsonSerializer.Serialize(new Context(actor, query.Values, section, obligation)));
    public string Read(string? token)
    {
        if (token is null || token.Length > 32768 || !token.StartsWith("v19.", StringComparison.Ordinal)) return "/validaciones";
        try
        {
            var data = JsonSerializer.Deserialize<Context>(protector.Unprotect(token[4..]));
            if (data is null || data.Actor != actor || data.Section is not ("pending" or "supervision") || data.Obligation == Guid.Empty || data.Values is null ||
                !ValidationQuery.TryRead(new QueryCollection(data.Values.ToDictionary(p => p.Key, p => new StringValues(p.Value))), out var query)) return "/validaciones";
            return QueryHelpers.AddQueryString("/validaciones", query.Values) + "#" + data.Section + "-" + data.Obligation.ToString("D");
        }
        catch (Exception e) when (e is CryptographicException or JsonException) { return "/validaciones"; }
    }
}
