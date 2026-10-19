using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Sgol.Web.Infrastructure.Persistence;

public sealed class PostgreSqlReadinessHealthCheck(SgolDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await dbContext.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy();
        }
        catch (Exception exception) when (
            exception is Npgsql.NpgsqlException or TimeoutException or OperationCanceledException or
            InvalidOperationException or ArgumentException)
        {
            return HealthCheckResult.Unhealthy();
        }
    }
}
