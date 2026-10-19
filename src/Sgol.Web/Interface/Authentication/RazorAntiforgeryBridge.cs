using Microsoft.AspNetCore.Antiforgery;
using Sgol.Web.Presentation.ApiClient;

namespace Sgol.Web.Presentation.Authentication;

public sealed class RazorAntiforgeryBridge(IAntiforgery antiforgery, ISgolApiClient apiClient,
    Sgol.Web.Presentation.Navigation.IRazorSessionState? sessionState = null)
{
    public string Issue(HttpContext context) =>
        antiforgery.GetAndStoreTokens(context).RequestToken ?? throw new ApiProtocolException();

    public async Task<ApiResponse<T>?> SendValidatedAsync<T>(
        HttpContext context,
        ApiRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Method == HttpMethod.Get || request.Method == HttpMethod.Head || request.CsrfToken is not null ||
            !context.Request.HasFormContentType)
            throw new ApiProtocolException();
        try
        {
            await antiforgery.ValidateRequestAsync(context);
        }
        catch (AntiforgeryValidationException)
        {
            return null;
        }
        var form = await context.Request.ReadFormAsync(cancellationToken);
        var tokens = form["__RequestVerificationToken"];
        if (tokens.Count != 1 || string.IsNullOrWhiteSpace(tokens[0])) return null;
        ApiResponse<T> response;
        try
        {
            response = await apiClient.SendAsync<T>(request with { CsrfToken = tokens[0] }, cancellationToken);
        }
        catch (Exception) when (request.Path == "/api/v1/auth/logout")
        {
            ApiCookieBridge.Clear(context);
            sessionState?.Invalidate();
            throw;
        }
        if (response.Status == 401 || response.IsSuccess && request.Path is
            ("/api/v1/auth/login" or "/api/v1/auth/password/change" or "/api/v1/auth/mfa/confirm" or
             "/api/v1/auth/mfa/verify" or "/api/v1/auth/recovery-codes/regenerate" or "/api/v1/auth/logout"))
            sessionState?.Invalidate();
        if (response.IsSuccess && request.Path.StartsWith("/api/v1/auth/", StringComparison.Ordinal) &&
            request.Path != "/api/v1/auth/mfa/enroll")
            ApiCookieBridge.ClearCsrf(context);
        if (request.Path == "/api/v1/auth/logout" && !response.IsSuccess &&
            response.ErrorCode is not ("CSRF_INVALID" or "CSRF_INVALIDO"))
        {
            ApiCookieBridge.Clear(context);
            sessionState?.Invalidate();
        }
        return response;
    }
}
