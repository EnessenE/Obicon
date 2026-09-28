using Microsoft.Extensions.Configuration;
using Obicon.Server.Configuration;

namespace Obicon.Server.Configuration;

public class JsonConfigRepository : IConfigRepository
{
    private readonly IConfiguration _configuration;
    private const string ConfigSection = "ServerSettings";

    public JsonConfigRepository(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task<ServerSettings> GetConfigAsync()
    {
        var config = _configuration.GetSection(ConfigSection).Get<ServerSettings>() ?? new ServerSettings();
        return Task.FromResult(config);
    }

    public Task SaveConfigAsync(ServerSettings config)
    {
        // In a real implementation, this would save to a JSON file
        // For now, we just return as we're using in-memory configuration
        return Task.CompletedTask;
    }
}
