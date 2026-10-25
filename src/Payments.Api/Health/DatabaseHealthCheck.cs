using Microsoft.Extensions.Diagnostics.HealthChecks;
using Payments.Api.Data;

namespace Payments.Api.Health;

public sealed class DatabaseHealthCheck(PaymentsDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Cannot connect to the payments database.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Cannot connect to the payments database.", ex);
        }
    }
}
