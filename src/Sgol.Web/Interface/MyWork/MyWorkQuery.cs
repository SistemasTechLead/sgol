using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;

namespace Sgol.Web.Presentation.MyWork;

public sealed class MyWorkQuery
{
    private static readonly HashSet<string> Names = new(["periodId", "taskState", "taskCursor", "noticeStatus", "noticeCursor",
        "queryPeriodId", "taskCode", "executionStatus", "condition", "responsiblePersonId", "cursor"], StringComparer.Ordinal);
    private static readonly HashSet<string> Tasks = new(["TAR-0005", "TAR-0007", "TAR-0008", "TAR-0011", "TAR-0018", "TAR-0026", "TAR-0092", "TAR-0093"], StringComparer.Ordinal);
    public Dictionary<string, string?> Values { get; } = new(StringComparer.Ordinal);
    public string? this[string key] => Values.GetValueOrDefault(key);
    public static bool CanonicalId(string? text, out Guid id) =>
        Guid.TryParseExact(text, "D", out id) && id != Guid.Empty && text == id.ToString("D");
    public static bool TryRead(IQueryCollection query, out MyWorkQuery result)
    {
        result = new();
        foreach (var (name, values) in query)
        {
            if (!Names.Contains(name) || values.Count != 1 || values[0] is not { } value || value.Length > 32768) return false;
            // Native selects send the empty option; never forward it to the API.
            if (value.Length == 0)
            {
                if (name is "taskState" or "noticeStatus" or "taskCode" or "executionStatus" or "condition") continue;
                return false;
            }
            if (name is "periodId" or "queryPeriodId" or "responsiblePersonId" && !CanonicalId(value, out _)) return false;
            if (name == "taskState" && value is not ("FUTURA" or "DISPONIBLE" or "VENCIDA" or "CONCLUIDA") ||
                name == "noticeStatus" && value is not ("ALL" or "UNREAD" or "READ") ||
                name == "taskCode" && !Tasks.Contains(value) ||
                name == "executionStatus" && value is not ("PENDIENTE" or "CONCLUIDA") ||
                name == "condition" && value is not ("VENCIDA" or "NO_VENCIDA")) return false;
            result.Values[name] = value;
        }
        return true;
    }
    public string Href(string anchor, params (string Key, string? Value)[] changes)
    {
        var values = new Dictionary<string, string?>(Values, StringComparer.Ordinal);
        foreach (var (key, value) in changes)
            if (value is null) values.Remove(key); else values[key] = value;
        return QueryHelpers.AddQueryString("/mi-trabajo", values) + "#" + anchor;
    }
    public string Filter(string section) => section switch
    {
        "tasks" => this["periodId"] + "|" + this["taskState"],
        "notices" => this["noticeStatus"] ?? "ALL",
        _ => string.Join("|", new[] { this["queryPeriodId"], this["taskCode"], this["executionStatus"], this["condition"], this["responsiblePersonId"] })
    };
    public IEnumerable<KeyValuePair<string, string?>> HiddenExcept(params string[] keys) =>
        Values.Where(pair => !keys.Contains(pair.Key, StringComparer.Ordinal));
}
