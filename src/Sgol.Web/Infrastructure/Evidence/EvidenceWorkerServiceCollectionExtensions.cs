using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sgol.Web.Infrastructure.Evidence;

namespace Sgol.JobInfrastructure;

public static class EvidenceWorkerServiceCollectionExtensions
{
    public static IServiceCollection AddSgolEvidenceWorkerInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddOptions<EvidenceStorageOptions>()
            .BindConfiguration(EvidenceStorageOptions.SectionName)
            .Validate(options => EvidenceOptionsValidation.IsStorageValid(options, environment.EnvironmentName),
                "Evidence storage configuration is invalid.");
        services.AddOptions<EvidenceScannerOptions>()
            .BindConfiguration(EvidenceScannerOptions.SectionName)
            .ValidateDataAnnotations()
            .Validate(EvidenceOptionsValidation.IsScannerValid, "Evidence scanner configuration is invalid.");
        var storage = configuration.GetSection(EvidenceStorageOptions.SectionName).Get<EvidenceStorageOptions>();
        var scanner = configuration.GetSection(EvidenceScannerOptions.SectionName).Get<EvidenceScannerOptions>();
        if (storage is not null && scanner is not null &&
            EvidenceOptionsValidation.IsStorageValid(storage, environment.EnvironmentName) &&
            EvidenceOptionsValidation.IsScannerValid(scanner))
        {
            EvidenceInfrastructureServiceCollectionExtensions.AddAdapters(services);
        }
        else
        {
            EvidenceInfrastructureServiceCollectionExtensions.AddFailClosedAdapters(services);
        }
        services.AddScoped<IOutboxHandler, EvidenceInspectionOutboxHandler>();
        services.AddScoped<IScheduledJob, CleanExpiredEvidenceUploadsJob>();
        return services;
    }
}
