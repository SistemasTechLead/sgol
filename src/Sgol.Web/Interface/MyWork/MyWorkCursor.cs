using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.Components;

namespace Sgol.Web.Presentation.MyWork;

public sealed class MyWorkCursor(IDataProtectionProvider provider, Guid actor, string section, string filter)
{
    private sealed record Trail(Guid Actor, string Filter, string? Cursor, string?[] Previous);
    private readonly IDataProtector protector = provider.CreateProtector("SGOL.FRONT-016.cursor.v1", section);
    public string? Cursor { get; private set; }
    private string?[] previous = [];
    public bool Read(string? token)
    {
        if (token is null) return true;
        try
        {
            var trail = JsonSerializer.Deserialize<Trail>(protector.Unprotect(token));
            if (trail is null || trail.Actor != actor || trail.Filter != filter || trail.Previous is null ||
                trail.Previous.Length > 100 || string.IsNullOrEmpty(trail.Cursor)) return false;
            Cursor = trail.Cursor; previous = trail.Previous; return true;
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException) { return false; }
    }
    private string Protect(string? cursor, string?[] trail) =>
        protector.Protect(JsonSerializer.Serialize(new Trail(actor, filter, cursor, trail)));
    public CursorPaginationViewModel Links(string? next, Func<string?, string> href)
    {
        if (next is { Length: 0 }) throw new ApiProtocolException();
        var prev = previous.Length == 0 ? null : previous.Length == 1 && previous[0] is null
            ? href(null) : href(Protect(previous[^1], previous[..^1]));
        return new(prev, next is null ? null : href(Protect(next, [.. previous, Cursor])));
    }
}
