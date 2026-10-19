using Sgol.BuildingBlocks.Identifiers;
using Sgol.BuildingBlocks.Time;

namespace Sgol.Web.Infrastructure.Http;

public static class HttpPrimitivesServiceCollectionExtensions
{
    public static IServiceCollection AddSgolHttpPrimitives(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IUuidGenerator, Uuid7Generator>();
        services.AddExceptionHandler<SafeExceptionHandler>();
        services.AddScoped<SystemErrorPageRenderer>();
        services.Configure<Microsoft.AspNetCore.Mvc.MvcOptions>(options => options.Filters.Add(new SystemErrorAntiforgeryFilter()));
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
                context.ProblemDetails.Extensions["correlationId"] =
                    context.HttpContext.GetCorrelationId();
            };
        });

        return services;
    }
}
