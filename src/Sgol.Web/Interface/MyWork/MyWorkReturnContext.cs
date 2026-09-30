using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Sgol.Web.Presentation.MyWork;

public sealed class MyWorkReturnContext(IDataProtectionProvider provider, Guid actor)
{
    private sealed record Context(Guid Actor, Dictionary<string, string?> Values);
    private readonly IDataProtector protector = provider.CreateProtector("SGOL.FRONT-016.return.v1");
    public string Protect(MyWorkQuery query) => protector.Protect(JsonSerializer.Serialize(new Context(actor,
        query.HiddenExcept("taskCursor", "noticeCursor", "cursor").ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal))));
    public string Read(string? token)
    {
        if (token is null || token.Length > 32768) return "/mi-trabajo";
        try
        {
            var context = JsonSerializer.Deserialize<Context>(protector.Unprotect(token));
            if (context is null || context.Actor != actor || context.Values is null) return "/mi-trabajo";
            var values = context.Values.ToDictionary(p => p.Key, p => new StringValues(p.Value), StringComparer.Ordinal);
            return MyWorkQuery.TryRead(new QueryCollection(values), out var query) ? query.Href("consulta-tareas") : "/mi-trabajo";
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException) { return "/mi-trabajo"; }
    }
}
