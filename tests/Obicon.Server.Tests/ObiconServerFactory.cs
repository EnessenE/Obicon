using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Obicon.Server.Tests;

/// <summary>
/// Boots the real server in-memory with an isolated SQLite database per instance.
/// Derived classes can enable node auto-enrollment by forcing the setting through
/// an environment variable, exactly like production configuration would.
/// </summary>
public class ObiconServerFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// Whether NodeAutoEnrollmentEnabled is forced to true via environment variable.
    /// </summary>
    protected virtual bool EnrollmentEnabled => false;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={TempDbPath()}");

        Environment.SetEnvironmentVariable(
            "ServerSettings__NodeAutoEnrollmentEnabled",
            EnrollmentEnabled ? "true" : null);
    }

    private static string TempDbPath()
    {
        return Path.Combine(Path.GetTempPath(), $"obicon-test-{Guid.NewGuid():N}.db");
    }
}

/// <summary>
/// Server factory with node auto-enrollment forced on.
/// </summary>
public class EnrollmentEnabledServerFactory : ObiconServerFactory
{
    protected override bool EnrollmentEnabled => true;
}
