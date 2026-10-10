using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Obicon.Server.Data;

namespace Obicon.Server.Health;

/// <summary>
/// Health check verifying the PostgreSQL database answers a simple query.
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly IDbContextFactory<ObiconDbContext> _dbFactory;

    public DatabaseHealthCheck(IDbContextFactory<ObiconDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
            var canConnect = await db.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy("PostgreSQL database is reachable")
                : new HealthCheckResult(context.Registration.FailureStatus, "PostgreSQL database is not reachable");
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, $"PostgreSQL database check failed: {ex.Message}");
        }
    }
}
