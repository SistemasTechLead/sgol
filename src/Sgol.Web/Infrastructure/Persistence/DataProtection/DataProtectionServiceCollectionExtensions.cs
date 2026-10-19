using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Sgol.Web.Infrastructure.Persistence.DataProtection;

internal static class DataProtectionServiceCollectionExtensions
{
    internal static IServiceCollection AddSgolDataProtection(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var applicationName = configuration["DataProtection:ApplicationName"];
        var certificateText = configuration["DataProtection:WrappingCertificate"];
        var certificatePassword = configuration["DataProtection:WrappingCertificatePassword"];
        var builder = services.AddDataProtection();

        var anyPortableSetting = new[] { applicationName, certificateText, certificatePassword }
            .Any(value => !string.IsNullOrWhiteSpace(value));
        if (!anyPortableSetting)
        {
            // Host builders register their effective environment before composition. Raw
            // configuration keys can be absent or overridden after that choice was made.
            var hostEnvironment = services.LastOrDefault(descriptor =>
                descriptor.ServiceType == typeof(IHostEnvironment))?.ImplementationInstance as IHostEnvironment;
            var environment = hostEnvironment?.EnvironmentName ??
                configuration["ASPNETCORE_ENVIRONMENT"] ?? configuration["DOTNET_ENVIRONMENT"];
            if (environment is "Staging" or "Production")
            {
                throw new InvalidOperationException("Portable Data Protection configuration is required.");
            }

            return services;
        }

        if (string.IsNullOrWhiteSpace(applicationName) || string.IsNullOrWhiteSpace(certificateText) ||
            string.IsNullOrWhiteSpace(certificatePassword) ||
            certificateText.Contains("REQUIRED_", StringComparison.OrdinalIgnoreCase) ||
            certificatePassword.Contains("REQUIRED_", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Data Protection configuration is incomplete.");
        }

        var connectionString = configuration.GetConnectionString("Sgol");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("PostgreSQL is required for the Data Protection key ring.");
        }

        _ = new NpgsqlConnectionStringBuilder(connectionString);
        X509Certificate2 certificate;
        try
        {
            certificate = X509CertificateLoader.LoadPkcs12(
                Convert.FromBase64String(certificateText),
                certificatePassword,
                X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);
        }
        catch (Exception exception) when (exception is FormatException or CryptographicException)
        {
            throw new InvalidOperationException("Data Protection wrapping certificate is invalid.", exception);
        }

        builder.SetApplicationName(applicationName).ProtectKeysWithCertificate(certificate);
        services.Configure<KeyManagementOptions>(options =>
            options.XmlRepository = new PostgreSqlXmlRepository(connectionString));
        services.AddSingleton(certificate);
        return services;
    }
}
