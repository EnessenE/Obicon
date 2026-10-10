using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Obicon.Server.Data;

/// <summary>
/// Design-time factory for the EF Core tooling (dotnet ef): builds the context with the
/// Npgsql provider without booting the whole host, so migrations can be generated and
/// scripted without a running server or database.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ObiconDbContext>
{
    /// <summary>
    /// Connection string the tooling uses. Never connected to when only generating
    /// migrations; a real connection is required for script execution only.
    /// Default: "Host=localhost;Database=obicon;Username=postgres;Password=postgres".
    /// </summary>
    public const string ToolingConnectionString = "Host=localhost;Database=obicon;Username=postgres;Password=postgres";

    public ObiconDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ObiconDbContext>()
            .UseNpgsql(ToolingConnectionString)
            .Options;
        return new ObiconDbContext(options);
    }
}
