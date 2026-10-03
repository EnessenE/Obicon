namespace Obicon.Server.Configuration;

public interface IConfigRepository
{
    Task<ServerSettings> GetConfigAsync();
    Task SaveConfigAsync(ServerSettings config);
}
