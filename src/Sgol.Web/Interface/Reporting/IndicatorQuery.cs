using System.Globalization;
using Microsoft.AspNetCore.WebUtilities;
using Sgol.Identity.Contracts;

namespace Sgol.Web.Presentation.Reporting;

public sealed class IndicatorQuery
{
    public static readonly string[] Fields = ["isoYear", "isoWeek", "level", "responsiblePersonId", "limit", "cursor"];
    public Dictionary<string, string?> Values { get; } = new(StringComparer.Ordinal);
    public string? Value(string section, string field) => Values.GetValueOrDefault(section + field);
    public bool Selected(string section) => Value(section, "isoYear") is not null;
    public string Hash(string section) => string.Join('|', Fields.Where(f => f != "cursor").Select(f => Value(section, f) ?? ""));
    public Dictionary<string, string?> Api(string section, string? cursor) => Fields.Where(f => f != "cursor")
        .ToDictionary(f => f, f => Value(section, f)).Append(new("cursor", cursor)).ToDictionary(p => p.Key, p => p.Value);
    public string Href(string section, string? cursor = null) => QueryHelpers.AddQueryString("/indicadores", Values
        .Where(p => p.Key != section + "cursor").Append(new(section + "cursor", cursor)).ToDictionary(p => p.Key, p => p.Value)) + "#" + section;
    public string Reset(string section) => QueryHelpers.AddQueryString("/indicadores", Values
        .Where(p => !p.Key.StartsWith(section, StringComparison.Ordinal) || p.Key == section + "isoYear" || p.Key == section + "isoWeek")
        .ToDictionary(p => p.Key, p => p.Value)) + "#" + section;
    public static bool TryRead(IQueryCollection input, out IndicatorQuery query)
    {
        query = new();
        var allowed = Fields.SelectMany(f => new[] { "operation" + f, "direction" + f }).ToHashSet(StringComparer.Ordinal);
        if (input.Keys.Any(k => !allowed.Contains(k)) || input.Any(p => p.Value.Count != 1)) return false;
        foreach (var item in input) if (!string.IsNullOrEmpty(item.Value)) query.Values[item.Key] = item.Value.ToString();
        foreach (var section in new[] { "operation", "direction" })
        {
            var y = query.Value(section, "isoYear"); var w = query.Value(section, "isoWeek");
            if (y is null != (w is null) || y is not null && (section == "operation" && y.Length != 4 ||
                !int.TryParse(y, NumberStyles.None, CultureInfo.InvariantCulture, out var year) || year is < 1 or > 9999 ||
                !int.TryParse(w, NumberStyles.None, CultureInfo.InvariantCulture, out var week) || week < 1 || week > ISOWeek.GetWeeksInYear(year))) return false;
            if (query.Value(section, "level") is { } level && !CanonicalRole.IsDefined(level) ||
                query.Value(section, "responsiblePersonId") is { } id && !CanonicalId(id) ||
                query.Value(section, "limit") is { } limit && (!int.TryParse(limit, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number is < 1 or > 100) ||
                query.Value(section, "cursor") is { Length: > 32768 }) return false;
        }
        return true;
    }
    public static bool CanonicalId(string value) => Guid.TryParseExact(value, "D", out var id) && id != Guid.Empty && id.ToString("D") == value;
}
