using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Sgol.Web.Infrastructure.Authentication;

// A process-local, single-use capability. Never accepts an address supplied in a request header.
// Lifetime is bounded by the outbound request; a failed send disposes its ticket as well.
internal sealed class InternalLoginOrigin : IDisposable
{
    internal const string Header = "X-SGOL-Login-Ticket";
    private static readonly ConcurrentDictionary<string, (string Address, DateTimeOffset Expires)> Tickets = new();
    internal string Token { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    internal InternalLoginOrigin(HttpContext context) => Tickets[Token] =
        (context.Connection.RemoteIpAddress?.ToString() ?? "unknown", DateTimeOffset.UtcNow.AddSeconds(30));
    internal static string Resolve(HttpContext context)
    {
        var header = context.Request.Headers[Header];
        if (header.Count == 1 && header[0] is { Length: 64 } ticket && Tickets.TryRemove(ticket, out var origin) && origin.Expires > DateTimeOffset.UtcNow)
            return origin.Address;
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
    public void Dispose() => Tickets.TryRemove(Token, out _);
}
