using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Sgol.Evidence.Contracts;
using Sgol.Evidence.Technical;
using Sgol.JobInfrastructure;

namespace Sgol.Web.Infrastructure.Evidence;

public static class EvidenceInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddSgolEvidenceInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddOptions<EvidenceStorageOptions>()
            .Bind(configuration.GetSection(EvidenceStorageOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(options => EvidenceOptionsValidation.IsStorageValid(options, environment.EnvironmentName),
                "Evidence storage configuration is invalid.");
        services.AddOptions<EvidenceScannerOptions>()
            .Bind(configuration.GetSection(EvidenceScannerOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(EvidenceOptionsValidation.IsScannerValid, "Evidence scanner configuration is invalid.");

        var storage = configuration.GetSection(EvidenceStorageOptions.SectionName).Get<EvidenceStorageOptions>();
        var scanner = configuration.GetSection(EvidenceScannerOptions.SectionName).Get<EvidenceScannerOptions>();
        if (storage is not null && scanner is not null &&
            EvidenceOptionsValidation.IsStorageValid(storage, environment.EnvironmentName) &&
            EvidenceOptionsValidation.IsScannerValid(scanner))
        {
            AddAdapters(services);
        }
        else
        {
            AddFailClosedAdapters(services);
        }
        services.AddHealthChecks()
            .AddCheck<EvidenceStorageHealthCheck>("evidence-storage", tags: ["evidence-ready"])
            .AddCheck<ClamAvHealthCheck>("evidence-scanner", tags: ["evidence-ready"]);
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IOutboxHandler, EvidenceInspectionOutboxHandler>());
        return services;
    }

    internal static void AddAdapters(IServiceCollection services)
    {
        services.TryAddSingleton<IEvidenceObjectKeyFactory, CryptographicEvidenceObjectKeyFactory>();
        services.TryAddSingleton<IFileTechnicalValidator, FileTechnicalValidator>();
        services.TryAddSingleton<IAmazonS3>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<EvidenceStorageOptions>>().Value;
            var credentials = new BasicAWSCredentials(options.AccessKey, options.SecretKey);
            var clientConfiguration = new AmazonS3Config
            {
                ServiceURL = options.Endpoint,
                AuthenticationRegion = options.Region,
                ForcePathStyle = true,
                UseHttp = new Uri(options.Endpoint).Scheme == Uri.UriSchemeHttp,
                MaxErrorRetry = 0,
                Timeout = TimeSpan.FromSeconds(30)
            };
            return new AmazonS3Client(credentials, clientConfiguration);
        });
        services.TryAddSingleton<IPrivateObjectStorage, S3PrivateObjectStorage>();
        services.TryAddSingleton<IFileMalwareScanner, ClamAvScanner>();
        services.TryAddSingleton<EvidenceInspectionPipeline>();
    }

    internal static void AddFailClosedAdapters(IServiceCollection services)
    {
        services.TryAddSingleton<IEvidenceObjectKeyFactory, CryptographicEvidenceObjectKeyFactory>();
        services.TryAddSingleton<IFileTechnicalValidator, FileTechnicalValidator>();
        services.TryAddSingleton<IPrivateObjectStorage, UnavailablePrivateObjectStorage>();
        services.TryAddSingleton<IFileMalwareScanner, UnavailableMalwareScanner>();
        services.TryAddSingleton<EvidenceInspectionPipeline>();
    }
}
