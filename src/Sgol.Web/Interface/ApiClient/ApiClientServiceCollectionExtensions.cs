using Microsoft.Extensions.Hosting;

namespace Sgol.Web.Presentation.ApiClient;

public static class ApiClientServiceCollectionExtensions
{
    public static IServiceCollection AddSgolApiClient(this IServiceCollection services,
        IConfiguration configuration, IHostEnvironment environment)
    {
        var value = configuration["SgolApi:TrustedOrigin"];
        Uri? origin = null;
        if (!string.IsNullOrWhiteSpace(value))
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out origin) || !IsOrigin(origin))
                throw new InvalidOperationException("The trusted API origin must be an HTTPS origin without credentials, path, query or fragment.");
            // Host filtering protects inbound traffic; SendAsync additionally checks scheme and port.
            var trustedHost = origin.HostNameType == UriHostNameType.IPv6 ? origin.Host : origin.IdnHost;
            // The approved OCI health probe calls HTTP 127.0.0.1:8080. This technical
            // host is not another credential-bridge origin: SendAsync still rejects it.
            var allowedHosts = new[] { trustedHost, "127.0.0.1" }.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            configuration["AllowedHosts"] = string.Join(';', allowedHosts);
            services.PostConfigure<Microsoft.AspNetCore.HostFiltering.HostFilteringOptions>(options =>
            {
                options.AllowedHosts = allowedHosts;
                options.AllowEmptyHosts = false;
            });
        }
        else if (environment.IsProduction() || environment.IsStaging())
            throw new InvalidOperationException("A trusted API origin is required.");

        services.AddHttpClient<ISgolApiClient, SgolApiClient>(client => client.BaseAddress = origin)
            .RemoveAllLoggers()
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                UseCookies = false,
                AllowAutoRedirect = false
            });
        return services;
    }

    internal static bool IsOrigin(Uri origin) => origin.IsAbsoluteUri && origin.Scheme == Uri.UriSchemeHttps &&
        origin.OriginalString.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && !origin.OriginalString.Contains('\\') &&
        origin.HostNameType != UriHostNameType.Unknown && !origin.IdnHost.Contains('*') && origin.Port > 0 &&
        origin.UserInfo.Length == 0 && origin.AbsolutePath == "/" && HasOnlyRootPath(origin) && origin.Query.Length == 0 && origin.Fragment.Length == 0;

    private static bool HasOnlyRootPath(Uri origin)
    {
        var pathIndex = origin.OriginalString.IndexOf('/', "https://".Length);
        return pathIndex < 0 || origin.OriginalString[pathIndex..] == "/";
    }
}
