using System.Security.Cryptography;
using System.Text;
using Sgol.Web.Presentation.ProblemDetails;

namespace Sgol.Web.Presentation.ApiClient;

public enum ApiResponseShape { Item, Collection, NoContent }

public sealed record ApiRequest(
    HttpMethod Method,
    string Path,
    ApiResponseShape Shape,
    object? Body = null,
    string? IfMatch = null,
    ApiMutationIntent? Intent = null,
    string? CsrfToken = null)
{
    public override string ToString() => $"ApiRequest(Method={Method.Method})";
}

public sealed record ApiResponse<T>(
    int Status,
    T? Data,
    IReadOnlyList<T>? Items,
    string CorrelationId,
    string? NextCursor,
    int? Count,
    string? ETag,
    bool Replayed,
    string? ErrorCode,
    ProblemDetailsPresentation? Error)
{
    public bool IsSuccess => Error is null;
    public override string ToString() => $"ApiResponse(Status={Status}, CorrelationId={CorrelationId})";
}

public sealed class ApiMutationIntent
{
    private byte[]? _fingerprint;

    private ApiMutationIntent(Guid key) => Key = key;

    public Guid Key { get; }

    public static ApiMutationIntent New() => new(Guid.NewGuid());

    public static ApiMutationIntent FromKey(Guid key) => key == Guid.Empty
        ? throw new ArgumentException("La intención requiere una clave.", nameof(key))
        : new ApiMutationIntent(key);

    internal void Bind(HttpMethod method, string path, string? ifMatch, byte[] body)
    {
        var prefix = Encoding.UTF8.GetBytes($"{method.Method}\n{path}\n{ifMatch}\n");
        var combined = new byte[prefix.Length + body.Length];
        prefix.CopyTo(combined, 0);
        body.CopyTo(combined, prefix.Length);
        var fingerprint = SHA256.HashData(combined);
        if (_fingerprint is null)
        {
            _fingerprint = fingerprint;
        }
        else if (!CryptographicOperations.FixedTimeEquals(_fingerprint, fingerprint))
        {
            throw new InvalidOperationException("La intención ya se usó con otros datos.");
        }
    }
}

public sealed class ApiProtocolException : Exception
{
    public ApiProtocolException() : base("La respuesta de la API no cumple el contrato esperado.") { }
}
