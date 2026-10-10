using System.Text.Json;
using Obicon.Server.Models.Responses;

namespace Obicon.Server.Services;

public interface IServerSettingsService
{
    Task<IEnumerable<ServerSettingResponse>> GetAllAsync();
    Task<ServerSettingResponse> SetAsync(string key, JsonElement value);
    Task<T> GetAsync<T>(string key);
}
