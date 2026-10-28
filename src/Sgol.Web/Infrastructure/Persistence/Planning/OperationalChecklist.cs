using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Sgol.Planning.Contracts;

namespace Sgol.Web.Infrastructure.Persistence.Planning;

public sealed record ChecklistPoint(Guid Id, string Text);
public sealed record ChecklistAnswer(string Definition, Guid[] Checked);
public sealed record ChecklistDraft(Guid Actor, DateTimeOffset At, Guid[] Checked);

public static class OperationalChecklist
{
    public static ChecklistPoint[] Points(string? json)
    {
        try { return JsonSerializer.Deserialize<ChecklistPoint[]>(json ?? "[]") ?? []; }
        catch (JsonException) { throw new DirectionPlanException("TASK_CHECKLIST_INVALID", 422); }
    }

    public static string Normalize(string? json, bool required)
    {
        if (!required) return "[]";
        var points = Points(json);
        if (points.Length == 0 || points.Any(p => p is null || p.Id == Guid.Empty || string.IsNullOrWhiteSpace(p.Text) ||
            p.Text.Trim().Length > 1000 || p.Text.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t'))) ||
            points.Select(p => p.Id).Distinct().Count() != points.Length)
            throw new DirectionPlanException("TASK_CHECKLIST_INVALID", 422);
        return JsonSerializer.Serialize(points.Select(p => p with { Text = p.Text.Trim() }));
    }

    public static string Fingerprint(string json) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    public static Guid[] Checked(string? json)
    {
        try { return JsonSerializer.Deserialize<ChecklistDraft>(json ?? "null")?.Checked ?? []; }
        catch (JsonException) { return []; }
    }
    public static Guid[] ValidateAnswer(string definition, string? answer, bool complete)
    {
        ChecklistAnswer? input;
        try { input = JsonSerializer.Deserialize<ChecklistAnswer>(answer ?? "null"); }
        catch (JsonException) { throw new DirectionPlanException("TASK_CHECKLIST_INVALID", 422); }
        if (input is null || input.Definition != Fingerprint(definition))
            throw new DirectionPlanException("TASK_CHECKLIST_CHANGED", 409);
        var points = Points(definition);
        var selected = input.Checked;
        var ids = points.Select(p => p.Id).ToHashSet();
        if (selected is null || selected.Distinct().Count() != selected.Length || selected.Any(id => !ids.Contains(id)))
            throw new DirectionPlanException("TASK_CHECKLIST_INVALID", 422);
        if (points.Length == 0 || complete && selected.Length != points.Length)
            throw new DirectionPlanException("TASK_CHECKLIST_PENDING", 422);
        return selected;
    }
    public static bool Complete(string definition, string? draft)
    {
        var points = Points(definition); var selected = Checked(draft);
        return points.Length > 0 && points.Select(p => p.Id).ToHashSet().SetEquals(selected);
    }
    public static string Reconcile(string oldDefinition, string newDefinition, string draft)
    {
        var unchanged = Points(oldDefinition).Intersect(Points(newDefinition)).Select(p => p.Id).ToHashSet();
        try {
            var previous = JsonSerializer.Deserialize<ChecklistDraft>(draft);
            return previous is null ? "null" : JsonSerializer.Serialize(previous with { Checked = previous.Checked.Where(unchanged.Contains).ToArray() });
        } catch (JsonException) { return "null"; }
    }
    public static string[] Changes(string before, string after)
    {
        var a = Points(before); var b = Points(after); var changes = new List<string>();
        foreach (var p in a.Where(p => !b.Any(n => n.Id == p.Id))) changes.Add($"Punto eliminado: {p.Text}");
        foreach (var p in b) {
            var old = a.SingleOrDefault(n => n.Id == p.Id);
            if (old is null) changes.Add($"Punto agregado: {p.Text}");
            else if (old.Text != p.Text) changes.Add($"Punto modificado: {old.Text} → {p.Text}");
        }
        if (!a.Select(p => p.Id).SequenceEqual(b.Select(p => p.Id)) && changes.Count == 0) changes.Add("Se reordenarán los puntos del checklist.");
        return changes.ToArray();
    }
}
