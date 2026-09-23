using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Sgol.Web.Presentation.ProblemDetails;

namespace Sgol.Web.Presentation.ApiClient;

public interface ISgolApiClient
{
    Task<ApiResponse<T>> SendAsync<T>(ApiRequest request, CancellationToken cancellationToken = default);
}

public sealed class SgolApiClient(HttpClient httpClient, IHttpContextAccessor contextAccessor) : ISgolApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<int> ErrorStatuses = [400, 401, 403, 404, 409, 412, 413, 415, 422, 423, 428, 429, 500, 503];

    public async Task<ApiResponse<T>> SendAsync<T>(ApiRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var context = contextAccessor.HttpContext ?? throw new InvalidOperationException("Se requiere una solicitud Razor activa.");
        if (!ValidPath(request.Path) || !Uri.TryCreate($"{context.Request.Scheme}://{context.Request.Host}", UriKind.Absolute, out var origin) ||
            origin.Scheme is not ("http" or "https"))
            throw new ArgumentException("La ruta debe pertenecer a /api/v1 del mismo origen.", nameof(request));

        var mutation = request.Method != HttpMethod.Get && request.Method != HttpMethod.Head;
        if (mutation && string.IsNullOrWhiteSpace(request.CsrfToken))
            throw new ArgumentException("La mutación requiere un token CSRF explícito.", nameof(request));
        if (!mutation && (request.Intent is not null || request.Body is not null || request.CsrfToken is not null))
            throw new ArgumentException("La lectura no admite cuerpo, CSRF ni intención de mutación.", nameof(request));
        if (request.IfMatch is not null && !StrongEtag(request.IfMatch))
            throw new ArgumentException("If-Match requiere un ETag fuerte entre comillas.", nameof(request));

        var body = request.Body is null ? [] : JsonSerializer.SerializeToUtf8Bytes(request.Body, JsonOptions);
        request.Intent?.Bind(request.Method, request.Path, request.IfMatch, body);
        var target = new Uri(origin, request.Path);
        if (!target.AbsolutePath.StartsWith("/api/v1/", StringComparison.Ordinal) || target.Host != origin.Host ||
            target.Port != origin.Port || target.Scheme != origin.Scheme) throw new ArgumentException("La ruta debe pertenecer a /api/v1 del mismo origen.", nameof(request));
        using var outbound = new HttpRequestMessage(request.Method, target);
        if (request.Body is not null)
        {
            outbound.Content = new ByteArrayContent(body);
            outbound.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = Encoding.UTF8.WebName };
        }
        if (request.IfMatch is not null) outbound.Headers.TryAddWithoutValidation("If-Match", request.IfMatch);
        if (request.Intent is not null) outbound.Headers.TryAddWithoutValidation("Idempotency-Key", request.Intent.Key.ToString("D"));
        if (mutation) outbound.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", request.CsrfToken);
        var sessionCookie = SessionCookie(context.Request.Headers.Cookie.ToString());
        if (sessionCookie is not null) outbound.Headers.TryAddWithoutValidation("Cookie", sessionCookie);

        HttpResponseMessage received;
        try { received = await httpClient.SendAsync(outbound, HttpCompletionOption.ResponseHeadersRead, cancellationToken); }
        catch (HttpRequestException) { throw new ApiProtocolException(); }
        using var response = received;
        var status = (int)response.StatusCode;
        var correlations = response.Headers.TryGetValues("X-Correlation-ID", out var headers)
            ? headers.Take(2).ToArray() : [];
        if (correlations.Length > 1) throw new ApiProtocolException();
        var headerCorrelation = correlations.SingleOrDefault();
        var etag = response.Headers.ETag?.ToString();
        if (etag is not null && !StrongEtag(etag)) throw new ApiProtocolException();

        if (status == 204 && request.Shape == ApiResponseShape.NoContent)
        {
            if (!ValidCorrelation(headerCorrelation) || response.Content.Headers.ContentLength is > 0) throw new ApiProtocolException();
            return new(status, default, null, headerCorrelation!, null, null, etag, false, null, null);
        }
        if (status is >= 200 and < 300 && request.Shape == ApiResponseShape.NoContent) throw new ApiProtocolException();

        var expectedMedia = status is >= 200 and < 300 ? "application/json" : "application/problem+json";
        if (response.Content.Headers.ContentType?.MediaType != expectedMedia) throw new ApiProtocolException();
        JsonDocument document;
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            document = await JsonDocument.ParseAsync(stream, new JsonDocumentOptions { MaxDepth = 64 }, cancellationToken);
        }
        catch (JsonException) { throw new ApiProtocolException(); }
        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw new ApiProtocolException();
            if (status is >= 200 and < 300)
            {
                if (!root.TryGetProperty("data", out var data) || !root.TryGetProperty("meta", out var meta) ||
                    meta.ValueKind != JsonValueKind.Object || !TryCorrelation(meta, headerCorrelation, out var correlation))
                    throw new ApiProtocolException();
                var replayed = OptionalBool(meta, "replayed") ?? false;
                if (request.Shape == ApiResponseShape.Item)
                {
                    if (data.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined or JsonValueKind.Array) throw new ApiProtocolException();
                    var value = Deserialize<T>(data);
                    // Several integrated endpoints report recovery in data.result, not meta.replayed.
                    replayed |= data.ValueKind == JsonValueKind.Object && data.TryGetProperty("result", out var result) &&
                        result.ValueKind == JsonValueKind.String && result.GetString() == "RECUPERADA";
                    return new(status, value, null, correlation!, null, null, etag, replayed, null, null);
                }
                if (data.ValueKind != JsonValueKind.Array || !meta.TryGetProperty("count", out var countElement) ||
                    countElement.ValueKind != JsonValueKind.Number ||
                    !countElement.TryGetInt32(out var count) || count < 0 || count != data.GetArrayLength()) throw new ApiProtocolException();
                string? cursor = null;
                if (meta.TryGetProperty("nextCursor", out var cursorElement) && cursorElement.ValueKind != JsonValueKind.Null)
                {
                    if (cursorElement.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(cursorElement.GetString())) throw new ApiProtocolException();
                    cursor = cursorElement.GetString();
                }
                var items = Deserialize<List<T>>(data);
                return new(status, default, items, correlation!, cursor, count, etag, replayed, null, null);
            }
            if (!ErrorStatuses.Contains(status) || !root.TryGetProperty("status", out var problemStatus) ||
                problemStatus.ValueKind != JsonValueKind.Number ||
                !problemStatus.TryGetInt32(out var declaredStatus) || declaredStatus != status ||
                !TryCorrelation(root, headerCorrelation, out var problemCorrelation)) throw new ApiProtocolException();
            string? code = null;
            if (root.TryGetProperty("code", out var codeElement))
            {
                if (codeElement.ValueKind != JsonValueKind.String) throw new ApiProtocolException();
                code = codeElement.GetString();
            }
            if ((code is null && status != 500) || code is not null && !ValidCode(code)) throw new ApiProtocolException();
            var presentation = ProblemDetailsPresenter.Present(status, code, problemCorrelation);
            return new(status, default, null, problemCorrelation!, null, null, null, false, code, presentation);
        }
    }

    private static bool ValidPath(string path)
    {
        var pathOnly = path.Split('?', 2)[0];
        return pathOnly.StartsWith("/api/v1/", StringComparison.Ordinal) &&
            !pathOnly.Contains("//", StringComparison.Ordinal) && !pathOnly.Contains('\\') &&
            !pathOnly.Contains("/../", StringComparison.Ordinal) && !pathOnly.Contains("/./", StringComparison.Ordinal) &&
            !path.Contains('#') && Uri.TryCreate(path, UriKind.Relative, out _);
    }

    private static bool StrongEtag(string value) => value.Length >= 3 && value[0] == '"' && value[^1] == '"' &&
        !value.AsSpan(1, value.Length - 2).Contains('"') && !value.Contains('\r') && !value.Contains('\n');

    private static string? SessionCookie(string raw)
    {
        var matches = raw.Split(';', StringSplitOptions.TrimEntries)
            .Where(part => part.StartsWith("__Host-SGOL-Session=", StringComparison.Ordinal)).Take(2).ToArray();
        if (matches.Length > 1) throw new ApiProtocolException();
        return matches.SingleOrDefault();
    }

    private static bool ValidCorrelation(string? text) => Guid.TryParseExact(text, "D", out var id) &&
        id != Guid.Empty && id.Version == 7 && text == id.ToString("D");

    private static bool TryCorrelation(JsonElement container, string? header, out string? correlation)
    {
        correlation = null;
        if (!container.TryGetProperty("correlationId", out var value) || value.ValueKind != JsonValueKind.String ||
            !ValidCorrelation(value.GetString())) return false;
        correlation = value.GetString();
        return header is null || header == correlation;
    }

    private static bool? OptionalBool(JsonElement meta, string name)
    {
        if (!meta.TryGetProperty(name, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new ApiProtocolException()
        };
    }

    private static bool ValidCode(string code) => code.Length is > 0 and <= 80 &&
        code.All(character => character is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_');

    private static T Deserialize<T>(JsonElement value)
    {
        try { return value.Deserialize<T>(JsonOptions) ?? throw new ApiProtocolException(); }
        catch (JsonException) { throw new ApiProtocolException(); }
    }
}
