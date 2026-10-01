using System.Globalization;
using Microsoft.AspNetCore.WebUtilities;
using Sgol.Web.Presentation.Reporting;

namespace Sgol.Web.Presentation.Auditing;

public sealed class AuditQuery
{
    public static readonly string[] Fields = ["mode", "from", "to", "actorUserId", "resourceType", "resourceId", "action", "outcome", "correlationId", "level", "traceObligationId", "limit", "cursor"];
    private static readonly string[] GeneralOnly = ["actorUserId", "resourceType", "resourceId", "correlationId", "level"];
    public Dictionary<string, string?> Values { get; } = new(StringComparer.Ordinal);
    public string? Value(string key) => Values.GetValueOrDefault(key);
    public bool Selected => Value("from") is not null;
    public string Mode => Value("mode") ?? (Value("traceObligationId") is null ? "events" : "trace");
    public string Hash => string.Join('|', Fields.Where(f => f != "cursor").Select(f => Value(f) ?? ""));
    public string Href(string? cursor = null) => QueryHelpers.AddQueryString("/auditoria", Values.Where(p => p.Key != "cursor")
        .Append(new("cursor", cursor)).ToDictionary(p => p.Key, p => p.Value));
    public string Reset() => QueryHelpers.AddQueryString("/auditoria", Values.Where(p => p.Key is "from" or "to").ToDictionary(p => p.Key, p => p.Value));
    public static bool TryRead(IQueryCollection input, out AuditQuery query)
    {
        query = new();
        if (input.Keys.Any(k => !Fields.Contains(k, StringComparer.Ordinal)) || input.Any(p => p.Value.Count != 1)) return false;
        foreach (var item in input) if (!string.IsNullOrEmpty(item.Value)) query.Values[item.Key] = item.Value.ToString();
        if (query.Value("mode") is { } mode && mode is not ("events" or "trace")) return false;
        if (query.Value("mode") == "events" && query.Value("traceObligationId") is not null) return false;
        if (query.Value("mode") == "trace" && query.Value("traceObligationId") is null) return false;
        if (query.Values.Count == 0) return true;
        if (!Utc(query.Value("from"), out var from) || !Utc(query.Value("to"), out var to) || to <= from || to - from > TimeSpan.FromDays(31)) return false;
        foreach (var field in new[] { "actorUserId", "resourceId", "correlationId", "traceObligationId" })
            if (query.Value(field) is { } id && !IndicatorQuery.CanonicalId(id)) return false;
        foreach (var field in new[] { "resourceType", "action", "outcome" })
            if (query.Value(field) is { } value && (value.Length is < 1 or > 128 || value.Any(c => !(c is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '.' or ':' or '-')))) return false;
        var current = query;
        if (query.Value("resourceId") is not null && query.Value("resourceType") is null ||
            query.Value("traceObligationId") is not null && GeneralOnly.Any(f => current.Value(f) is not null) ||
            query.Value("level") is { } level && !Sgol.Identity.Contracts.CanonicalRole.IsDefined(level) ||
            query.Value("limit") is { } limit && (!int.TryParse(limit, NumberStyles.None, CultureInfo.InvariantCulture, out var count) || count is < 1 or > 100) ||
            query.Value("cursor") is { Length: > 32768 }) return false;
        return true;
    }
    private static bool Utc(string? value, out DateTimeOffset result)
    {
        result = default;
        return value is not null && value.EndsWith('Z') && value.Contains('T', StringComparison.Ordinal) &&
            DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out result) && result.Offset == TimeSpan.Zero;
    }
}
