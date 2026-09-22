namespace Sgol.Web.Infrastructure.Http;

public static class HttpPrimitivesApplicationBuilderExtensions
{
    public static IApplicationBuilder UseSgolHttpPrimitives(this IApplicationBuilder app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseExceptionHandler();
        app.UseStatusCodePages();

        return app;
    }
}
