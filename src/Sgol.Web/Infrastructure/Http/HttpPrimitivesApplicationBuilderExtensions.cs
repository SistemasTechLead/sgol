namespace Sgol.Web.Infrastructure.Http;

public static class HttpPrimitivesApplicationBuilderExtensions
{
    public static IApplicationBuilder UseSgolHttpPrimitives(this IApplicationBuilder app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseExceptionHandler();
        app.UseWhen(SystemErrorPageRenderer.WantsHtml, html =>
            html.UseStatusCodePages(status => status.HttpContext.RequestServices
                .GetRequiredService<SystemErrorPageRenderer>().RenderAsync(status.HttpContext)));
        app.UseWhen(context => !SystemErrorPageRenderer.WantsHtml(context), other => other.UseStatusCodePages());

        return app;
    }
}
