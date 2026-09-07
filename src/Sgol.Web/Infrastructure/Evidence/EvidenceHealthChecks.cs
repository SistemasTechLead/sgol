using Microsoft.Extensions.Diagnostics.HealthChecks;
using Sgol.Evidence.Contracts;

namespace Sgol.Web.Infrastructure.Evidence;

public sealed class EvidenceStorageHealthCheck(IPrivateObjectStorage storage) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await storage.CheckAvailabilityAsync(cancellationToken);
            return HealthCheckResult.Healthy("Private evidence storage is available.");
        }
        catch (Exception exception) when (exception is EvidenceInfrastructureException or OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Private evidence storage is unavailable.");
        }
    }
}

public sealed class ClamAvHealthCheck(IFileMalwareScanner scanner) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await scanner.CheckAvailabilityAsync(cancellationToken);
            return HealthCheckResult.Healthy("The evidence scanner is available.");
        }
        catch (Exception exception) when (exception is EvidenceInfrastructureException or OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("The evidence scanner is unavailable.");
        }
    }
}
