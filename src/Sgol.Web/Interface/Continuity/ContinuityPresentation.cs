using Sgol.Continuity.Contracts;
using Sgol.Web.Presentation.ApiClient;
using Sgol.Web.Presentation.Components;

namespace Sgol.Web.Presentation.Continuity;
public static class ContinuityPresentation
{
    public static StatusBadgeViewModel Badge(string status) => status switch
    {
        "REQUESTED" => new("Solicitada", "ⓘ", "info"),
        "REFERENCE_CAPTURING" => new("Capturando referencia", "◷", "info"),
        "REFERENCE_READY" => new("Referencia preparada", "ⓘ", "info"),
        "RESTORE_STARTED" => new("Restauración iniciada", "◷", "info"),
        "RECONCILING" => new("Comparando", "◷", "info"),
        "MATCHED" => new("Coincide; pendiente de aprobación", "✓", "info"),
        "DIFFERENT" => new("Diferencias detectadas", "△", "peligro"),
        "FAILED" => new("Falló la reconciliación", "△", "peligro"),
        "APPROVED" => new("Aprobada", "✓", "exito"),
        _ => new("Estado no reconocido: " + status, "ⓘ", "info")
    };
    public static bool CanApprove(RecoveryReconciliationDetails data) => data.Status == "MATCHED" && data.DifferenceCount == 0 &&
        data.Differences.Count == 0 && !data.DifferencesTruncated && data.ObservedRpoSeconds is >= 0 and <= 3600 && data.ObservedRtoSeconds is >= 0 and <= 14400;
    public static string? Reason(string? value)
    {
        var normalized = value?.Normalize(System.Text.NormalizationForm.FormC).Trim();
        return normalized is { Length: >= 1 and <= 500 } && !normalized.Any(c => c is '<' or '>' or '\r' or '\n' || char.IsControl(c) && c != '\t') ? normalized : null;
    }
    public static void Validate(RecoveryReconciliationDetails data, Guid? id, string? etag)
    {
        if (data is null || data.ReconciliationId == Guid.Empty || id is not null && data.ReconciliationId != id ||
            data.BranchId != Sgol.Organization.Contracts.BranchScope.LorettaId || data.Sequence < 1 || etag != $"\"{data.Sequence}\"" || data.RequestedAt.Offset != TimeSpan.Zero ||
            data.Differences is null || data.DifferenceCount < 0 || data.Differences.Count > 100000 ||
            data.Differences.Count > data.DifferenceCount || !data.DifferencesTruncated && data.Differences.Count != data.DifferenceCount || data.ObservedRpoSeconds < 0 || data.ObservedRtoSeconds < 0 ||
            string.IsNullOrWhiteSpace(data.Status)) throw new ApiProtocolException();
        var previous = 0;
        foreach (var row in data.Differences)
        {
            if (row is null || row.Ordinal <= previous || row.Ordinal > 100000 || string.IsNullOrWhiteSpace(row.Kind)) throw new ApiProtocolException();
            previous = row.Ordinal;
        }
        if (data.Status is "MATCHED" or "APPROVED" && (data.DifferenceCount != 0 || data.DifferencesTruncated)) throw new ApiProtocolException();
    }
}
