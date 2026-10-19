using System.Globalization;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using Sgol.Identity.Contracts;

namespace Sgol.Web.Presentation.Validation;

public sealed class ValidationQuery
{
    public static readonly string[] Fields = ["level", "responsiblePersonId", "isoYear", "isoWeek", "limit", "cursor"];
    public Dictionary<string, string?> Values { get; } = new(StringComparer.Ordinal);
    public string? Value(string section, string name) => Values.GetValueOrDefault(section + name);
    public bool HasFilters(string section) => Fields.Where(f => f is not ("cursor" or "limit")).Any(f => Value(section, f) is not null) || Value(section, "executionStatus") is not null;
    public string Hash(string section) => string.Join('|', Fields.Where(f => f != "cursor").Append("executionStatus").Select(f => Value(section, f) ?? ""));
    public string Href(string section, string? cursor = null) => QueryHelpers.AddQueryString("/validaciones", Values
        .Where(p => p.Key != section + "cursor").Append(new(section + "cursor", cursor)).ToDictionary(p => p.Key, p => p.Value)) + "#" + (section == "pending" ? "pendientes" : "supervision");
    public string Reset(string section) => QueryHelpers.AddQueryString("/validaciones", Values.Where(p => !p.Key.StartsWith(section, StringComparison.Ordinal)).ToDictionary(p => p.Key, p => p.Value));
    public Dictionary<string, string?> ApiValues(string section, string? cursor) => Fields.Where(f => f != "cursor")
        .Concat(section == "supervision" ? ["executionStatus"] : Array.Empty<string>()).ToDictionary(f => f, f => Value(section, f)).Append(new("cursor", cursor)).ToDictionary(p => p.Key, p => p.Value);
    public static bool TryRead(IQueryCollection input, out ValidationQuery result)
    {
        result = new();
        var allowed = new HashSet<string>(Fields.Select(f => "pending" + f).Concat(Fields.Append("executionStatus").Select(f => "supervision" + f)), StringComparer.Ordinal);
        if (input.Keys.Any(k => !allowed.Contains(k)) || input.Any(p => p.Value.Count != 1)) return false;
        foreach (var item in input) if (!string.IsNullOrWhiteSpace(item.Value)) result.Values[item.Key] = item.Value.ToString();
        foreach (var section in new[] { "pending", "supervision" })
        {
            if (result.Value(section, "level") is { } level && !CanonicalRole.IsDefined(level) ||
                result.Value(section, "responsiblePersonId") is { } person && (!Guid.TryParseExact(person, "D", out var id) || id == Guid.Empty || id.ToString("D") != person) ||
                result.Value(section, "limit") is { } limit && (!int.TryParse(limit, NumberStyles.None, CultureInfo.InvariantCulture, out var count) || count is < 1 or > 100) ||
                result.Value(section, "cursor") is { Length: > 32768 } ||
                result.Value(section, "executionStatus") is { } status && status is not ("PENDIENTE" or "CONCLUIDA")) return false;
            var year = result.Value(section, "isoYear"); var week = result.Value(section, "isoWeek");
            if (year is null != (week is null) || year is not null && (year.Length != 4 || !int.TryParse(year, out var y) || y is < 1 or > 9999 ||
                !int.TryParse(week, out var w) || w < 1 || w > ISOWeek.GetWeeksInYear(y))) return false;
        }
        return true;
    }
}
