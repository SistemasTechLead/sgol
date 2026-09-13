using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Sgol.Web.Infrastructure.Persistence.DataProtection;

public sealed class DataProtectionReadinessHealthCheck(IDataProtectionProvider provider) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var protector = provider.CreateProtector("SGOL.Readiness.v1");
            const string probe = "synthetic-readiness-probe";
            var protectedValue = protector.Protect(probe);
            return Task.FromResult(protector.Unprotect(protectedValue) == probe
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy());
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy());
        }
    }
}
