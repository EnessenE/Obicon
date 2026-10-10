using Npgsql;
using Testcontainers.PostgreSql;

namespace Obicon.Server.Tests;

/// <summary>
/// One PostgreSQL container shared by the whole test assembly: every
/// <see cref="ObiconServerFactory"/> gets its own database on it, so test classes stay
/// isolated without paying the container startup cost per class. The container starts
/// on first use and is removed by Testcontainers' watchdog when the test process exits.
/// Requires a running Docker engine.
/// </summary>
public static class TestPostgres
{
    private static readonly Lazy<PostgreSqlContainer> Container = new(StartContainer);

    private static PostgreSqlContainer StartContainer()
    {
        var container = new PostgreSqlBuilder("postgres:18-alpine")
            .WithDatabase("postgres")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
        container.StartAsync().GetAwaiter().GetResult();
        return container;
    }

    /// <summary>
    /// Connection string of the shared container's default database, used to create
    /// and drop the per-factory test databases.
    /// </summary>
    public static string MaintenanceConnectionString => Container.Value.GetConnectionString();

    /// <summary>
    /// Connection string pointing at the named per-factory database on the shared container.
    /// </summary>
    public static string ConnectionStringFor(string databaseName)
    {
        var builder = new NpgsqlConnectionStringBuilder(Container.Value.GetConnectionString())
        {
            Database = databaseName
        };
        return builder.ConnectionString;
    }
}
