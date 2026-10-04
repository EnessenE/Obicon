using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Obicon.Server.Tests;

/// <summary>
/// Boots the real server in-memory with an isolated PostgreSQL database per instance:
/// one database on the shared test container (see <see cref="TestPostgres"/>), created
/// up front and dropped on dispose. Derived classes can enable node auto-enrollment by
/// forcing the setting through an environment variable, exactly like production
/// configuration would.
/// </summary>
public class ObiconServerFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// The API auth header value forced onto the test server, so tests never depend
    /// on the appsettings default.
    /// </summary>
    public const string AuthHeader = "obicon-test-auth";

    private readonly string _databaseName = $"obicon_test_{Guid.NewGuid():N}";
    private bool _databaseCreated;

    /// <summary>
    /// Whether NodeAutoEnrollmentEnabled is forced to true via environment variable.
    /// </summary>
    protected virtual bool EnrollmentEnabled => false;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // The database must exist before the server boots, because it applies its
        // EF Core migrations on startup. The host can be built more than once per
        // factory instance, so the creation is idempotent.
        CreateDatabase();

        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", TestPostgres.ConnectionStringFor(_databaseName));
        builder.UseSetting("ServerSettings:AuthHeader", AuthHeader);

        // Server logs go to the running test's output through the "Xunit" sink
        // (XunitLoggingSinkExtensions, discovered in this assembly via Using) when
        // the test derives from LoggedTest; without an active scope the sink is silent.
        // Added through an in-memory source: UseSetting's flat keys leave an empty
        // intermediate value at WriteTo:1, which Serilog would read as a nameless sink
        builder.ConfigureAppConfiguration((context, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Serilog:Using:0"] = "Obicon.Server.Tests",
            ["Serilog:WriteTo:1:Name"] = "Xunit",
        }));

        Environment.SetEnvironmentVariable(
            "ServerSettings__NodeAutoEnrollmentEnabled",
            EnrollmentEnabled ? "true" : null);
    }

    protected override void Dispose(bool disposing)
    {
        // Best effort: drop the database even when a test failed mid-flight
        DropDatabase();
        base.Dispose(disposing);
    }

    private void CreateDatabase()
    {
        if (_databaseCreated)
        {
            return;
        }

        _databaseCreated = true;
        using var connection = new NpgsqlConnection(TestPostgres.MaintenanceConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE \"{_databaseName}\"";
        command.ExecuteNonQuery();
    }

    private void DropDatabase()
    {
        try
        {
            using var connection = new NpgsqlConnection(TestPostgres.MaintenanceConnectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            // Close the server's pooled connections first, or the drop is refused
            command.CommandText = $"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)";
            command.ExecuteNonQuery();
        }
        catch (Exception)
        {
            // Swallow cleanup failures; the container is discarded after the run anyway
        }
    }
}

/// <summary>
/// Server factory with node auto-enrollment forced on.
/// </summary>
public class EnrollmentEnabledServerFactory : ObiconServerFactory
{
    protected override bool EnrollmentEnabled => true;
}
