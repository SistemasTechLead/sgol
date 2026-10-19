namespace Sgol.Web.Infrastructure.Http;

public static class HttpContextCorrelationExtensions
{
    public static string GetCorrelationId(this HttpContext context) =>
        CorrelationIdMiddleware.GetCorrelationId(context);
}
