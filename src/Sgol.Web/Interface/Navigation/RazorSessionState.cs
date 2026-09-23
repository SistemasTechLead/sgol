using Sgol.Identity.Contracts;
using Sgol.Web.Presentation.ApiClient;

namespace Sgol.Web.Presentation.Navigation;

public interface IRazorSessionState
{
    Task<SessionSnapshot?> GetAsync(CancellationToken cancellationToken = default);
    bool IsInvalid { get; }
    void Invalidate();
}

public sealed class RazorSessionState(ISgolApiClient apiClient, IHttpContextAccessor? contextAccessor = null) : IRazorSessionState
{
    private Task<SessionSnapshot?>? snapshot;
    public bool IsInvalid { get; private set; }

    public Task<SessionSnapshot?> GetAsync(CancellationToken cancellationToken = default) =>
        snapshot ??= LoadAsync(cancellationToken);

    public void Invalidate()
    {
        IsInvalid = true;
        snapshot = Task.FromResult<SessionSnapshot?>(null);
    }

    private async Task<SessionSnapshot?> LoadAsync(CancellationToken cancellationToken)
    {
        ApiResponse<SessionSnapshot> response;
        try
        {
            response = await apiClient.SendAsync<SessionSnapshot>(
                new(HttpMethod.Get, "/api/v1/auth/session", ApiResponseShape.Item), cancellationToken);
        }
        catch (ApiProtocolException)
        {
            Invalidate();
            ClearBrowserCookies();
            return null;
        }
        if (!response.IsSuccess)
        {
            IsInvalid = response.Status == 401;
            return null;
        }
        var data = response.Data;
        if (data is null || data.UserId == Guid.Empty || data.PersonId == Guid.Empty ||
            data.BranchCode != "LOR-001" || data.RoleCode is not ("DIRECCION" or "ADMINISTRACION" or "SUBCOORDINACION" or "PISO_VENTAS") ||
            data.Permissions is null || data.IdleExpiresAt <= DateTimeOffset.UtcNow ||
            data.AbsoluteExpiresAt <= DateTimeOffset.UtcNow)
        {
            Invalidate();
            ClearBrowserCookies();
            return null;
        }
        return data;
    }

    private void ClearBrowserCookies()
    {
        if (contextAccessor?.HttpContext is { } context) ApiCookieBridge.Clear(context);
    }
}
