using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Sgol.Web.Infrastructure.Http;

public static class LocalSharedTransport
{
    public static IServiceCollection AddLocalSharedTransport(this IServiceCollection services, IConfiguration configuration)
    {
        var proxyText = configuration["ReverseProxy:KnownProxy"];
        if (!string.IsNullOrWhiteSpace(proxyText))
        {
            if (!IPAddress.TryParse(proxyText, out var proxy) || proxy.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork ||
                !(IsPrivate(proxy) || IPAddress.IsLoopback(proxy)))
            {
                throw new InvalidOperationException("Controlled IPv4 proxy is required.");
            }

            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.ForwardLimit = 1;
                options.KnownProxies.Clear();
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Add(proxy);
                options.RequireHeaderSymmetry = true;
            });
        }

        return services;
    }

    public static IApplicationBuilder UseLocalSharedTransport(this IApplicationBuilder app, IConfiguration configuration)
    {
        if (!string.IsNullOrWhiteSpace(configuration["ReverseProxy:KnownProxy"]))
        {
            app.UseForwardedHeaders();
        }

        if (configuration.GetValue<bool>("LocalShared:SecurityHeaders"))
        {
            app.UseMiddleware<LocalSharedSecurityHeadersMiddleware>();
        }

        return app;
    }

    private static bool IsPrivate(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes[0] == 10 || bytes[0] == 172 && bytes[1] is >= 16 and <= 31 || bytes[0] == 192 && bytes[1] == 168;
    }
}

public sealed class LocalSharedSecurityHeadersMiddleware
{
    public const string NonceItem = "SGOL.CspNonce";
    private readonly RequestDelegate next;
    private readonly string evidenceOrigin;

    public LocalSharedSecurityHeadersMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        this.next = next;
        var text = configuration["LocalShared:EvidenceOrigin"];
        if (!Uri.TryCreate(text, UriKind.Absolute, out var origin) || origin.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(origin.UserInfo) || origin.AbsolutePath != "/" || !string.IsNullOrEmpty(origin.Query) ||
            !string.IsNullOrEmpty(origin.Fragment))
        {
            throw new InvalidOperationException("HTTPS evidence origin is required for security headers.");
        }

        evidenceOrigin = origin.GetLeftPart(UriPartial.Authority);
    }

    public Task InvokeAsync(HttpContext context)
    {
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        context.Items[NonceItem] = nonce;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.ContentSecurityPolicy =
                "default-src 'none'; base-uri 'self'; object-src 'none'; frame-ancestors 'none'; form-action 'self'; " +
                $"script-src 'self' 'nonce-{nonce}'; style-src 'self'; img-src 'self' data: blob:; media-src 'self' blob:; font-src 'self'; connect-src 'self' {evidenceOrigin}" +
                (context.Request.Path.StartsWithSegments("/reportes") ? "; frame-src blob:" : "");
            if (context.Response.ContentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) == true)
            {
                context.Response.Headers.CacheControl = "no-store";
            }

            return Task.CompletedTask;
        });
        return next(context);
    }
}

[HtmlTargetElement("script")]
public sealed class ScriptNonceTagHelper(IHttpContextAccessor accessor) : TagHelper
{
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (accessor.HttpContext?.Items[LocalSharedSecurityHeadersMiddleware.NonceItem] is string nonce)
        {
            output.Attributes.SetAttribute("nonce", nonce);
        }
    }
}
