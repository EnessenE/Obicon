using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Obicon.Server.Tests;

/// <summary>
/// Boots the real server in-memory with an isolated SQLite database per instance.
/// Derived classes can enable node auto-enrollment by forcing the setting through
/// an environment variable, exactly like production configuration would.
/// </summary>
public class ObiconServerFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// The API auth header value forced onto the test server, so tests never depend
    /// on the appsettings default.
    /// </summary>
    public const string AuthHeader = "obicon-test-auth";

    /// <summary>
    /// Whether NodeAutoEnrollmentEnabled is forced to true via environment variable.
    /// </summary>
    protected virtual bool EnrollmentEnabled => false;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={TempDbPath()}");
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
