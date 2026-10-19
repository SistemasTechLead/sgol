using Microsoft.Extensions.Primitives;

namespace Sgol.Web.Infrastructure.Http;

internal static class IdempotencyKeyHeader
{
    public static IdempotencyKeyParseResult Parse(HttpRequest request)
    {
        StringValues values = request.Headers["Idempotency-Key"];
        if (values.Count == 0)
        {
            return IdempotencyKeyParseResult.Required();
        }

        var value = values.Count == 1 ? values[0] : null;
        if (values.Count != 1 || string.IsNullOrEmpty(value) ||
            !string.Equals(value, value.Trim(), StringComparison.Ordinal) ||
            !Guid.TryParseExact(value, "D", out var key) || key == Guid.Empty)
        {
            return IdempotencyKeyParseResult.Invalid();
        }

        return IdempotencyKeyParseResult.Success(key);
    }
}

internal readonly record struct IdempotencyKeyParseResult(
    bool IsValid,
    Guid Key,
    string? ErrorCode,
    string? Detail)
{
    public static IdempotencyKeyParseResult Success(Guid key) => new(true, key, null, null);

    public static IdempotencyKeyParseResult Required() => new(
        false,
        Guid.Empty,
        "IDEMPOTENCY_KEY_REQUERIDA",
        "Idempotency-Key es obligatoria");

    public static IdempotencyKeyParseResult Invalid() => new(
        false,
        Guid.Empty,
        "IDEMPOTENCY_KEY_INVALIDA",
        "Idempotency-Key debe ser un UUID canónico no vacío");
}
