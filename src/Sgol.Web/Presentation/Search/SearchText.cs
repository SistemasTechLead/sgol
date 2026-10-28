using System.Globalization;
using System.Text;

namespace Sgol.Web.Presentation.Search;

/// <summary>Comparison for user-facing searches; leaves stored and displayed text unchanged.</summary>
public static class SearchText
{
    public static string SqlNormalize(string value) => throw new InvalidOperationException("SQL only");

    public static string Normalize(string? value)
    {
        var result = new StringBuilder();
        foreach (var character in (value ?? "").Normalize(NormalizationForm.FormD))
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category is not (UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark))
                result.Append(character);
        }
        return result.ToString().ToLowerInvariant();
    }

    public static bool Contains(string? text, string? query) =>
        Normalize(text).Contains(Normalize(query), StringComparison.Ordinal);
}
