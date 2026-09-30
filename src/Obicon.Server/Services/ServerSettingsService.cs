using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Obicon.Server.Configuration;
using Obicon.Server.Data;
using Obicon.Server.Models;
using Obicon.Server.Models.Responses;

namespace Obicon.Server.Services;

/// <summary>
/// Resolves server settings: values pinned by appsettings or environment variables are forced
/// and read-only; everything else can be overridden at runtime (stored in SQLite).
/// Effective values are cached and the cache is invalidated on change.
/// </summary>
public class ServerSettingsService : IServerSettingsService
{
    private const string ConfigSection = "ServerSettings";

    private readonly IDbContextFactory<ObiconDbContext> _dbFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ServerSettingsService> _logger;

    private readonly ConcurrentDictionary<string, object> _cache = new();

    public ServerSettingsService(
        IDbContextFactory<ObiconDbContext> dbFactory,
        IConfiguration configuration,
        ILogger<ServerSettingsService> logger)
    {
        _dbFactory = dbFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<IEnumerable<ServerSettingResponse>> GetAllAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var overrides = await db.ServerSettingValues.ToDictionaryAsync(s => s.Key, s => s.Value);

        return ServerSettingDefinitions.All.Select(definition =>
        {
            var value = definition.Default;
            var source = "Default";
            var forced = false;

            if (definition.IsReadOnly)
            {
                source = "Derived";
                value = ComputeReadOnly(definition.Key, EffectiveRawValue(FindDefinition("FrequencyPresetsSeconds"), overrides));
            }
            else
            {
                forced = IsForced(definition.Key);
                if (forced)
                {
                    source = "Configuration (forced)";
                    value = _configuration[$"{ConfigSection}:{definition.Key}"] ?? value;
                }
                else if (overrides.TryGetValue(definition.Key, out var stored))
                {
                    source = "Database";
                    value = stored;
                }
            }

            return new ServerSettingResponse
            {
                Key = definition.Key,
                Description = definition.Description,
                Value = value,
                IsForced = forced,
                IsReadOnly = definition.IsReadOnly,
                Source = source,
                Group = definition.Group
            };
        });
    }

    public async Task<ServerSettingResponse> SetAsync(string key, string value)
    {
        var definition = ServerSettingDefinitions.All.FirstOrDefault(d => d.Key == key)
            ?? throw new ArgumentException($"Unknown setting: {key}");

        if (definition.IsReadOnly)
        {
            throw new InvalidOperationException($"Setting {key} is read-only; it is derived from FrequencyPresetsSeconds");
        }

        if (IsForced(key))
        {
            throw new InvalidOperationException($"Setting {key} is forced by appsettings or an environment variable and cannot be changed");
        }

        if (!TryConvert(definition, value, out var converted))
        {
            throw new ArgumentException($"Setting {key} expects a {definition.ValueType.Name} value");
        }

        await using var db = await _dbFactory.CreateDbContextAsync();
        var stored = await db.ServerSettingValues.FindAsync(key);
        if (stored == null)
        {
            db.ServerSettingValues.Add(new ServerSettingValue { Key = key, Value = value, UpdatedAt = DateTime.UtcNow });
        }
        else
        {
            stored.Value = value;
            stored.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();

        _cache.TryRemove(key, out _);
        _logger.LogInformation("Setting {Key} changed to {Value}", key, key == "AuthHeader" ? "***" : value);
        Metrics.ServerMetrics.Action("setting_changed");

        return new ServerSettingResponse
        {
            Key = key,
            Description = definition.Description,
            Value = value,
            IsForced = false,
            Source = "Database",
            Group = definition.Group
        };
    }

    public async Task<T> GetAsync<T>(string key)
    {
        if (_cache.TryGetValue(key, out var cached))
        {
            return (T)cached;
        }

        var definition = ServerSettingDefinitions.All.FirstOrDefault(d => d.Key == key)
            ?? throw new ArgumentException($"Unknown setting: {key}");

        await using var db = await _dbFactory.CreateDbContextAsync();
        var overrides = await db.ServerSettingValues.ToDictionaryAsync(s => s.Key, s => s.Value);

        var value = definition.IsReadOnly
            ? ComputeReadOnly(definition.Key, EffectiveRawValue(FindDefinition("FrequencyPresetsSeconds"), overrides))
            : EffectiveRawValue(definition, overrides);

        var converted = TryConvert(definition, value, out var result)
            ? result
            : throw new InvalidOperationException($"Stored value for {key} is not a valid {definition.ValueType.Name}");

        _cache[key] = converted;
        return (T)converted;
    }

    private static ServerSettingDefinition FindDefinition(string key)
    {
        return ServerSettingDefinitions.All.First(d => d.Key == key);
    }

    /// <summary>
    /// The effective raw value of a stored setting: forced configuration, database override, or default.
    /// </summary>
    private string EffectiveRawValue(ServerSettingDefinition definition, Dictionary<string, string> overrides)
    {
        if (IsForced(definition.Key))
        {
            return _configuration[$"{ConfigSection}:{definition.Key}"] ?? definition.Default;
        }

        return overrides.TryGetValue(definition.Key, out var stored) ? stored : definition.Default;
    }

    /// <summary>
    /// Computes the value of a read-only setting from the settings it derives from.
    /// </summary>
    private static string ComputeReadOnly(string key, string frequencyPresets)
    {
        if (key == "SchedulerLoopIntervalSeconds")
        {
            return FrequencyPresets.SchedulerIntervalSeconds(frequencyPresets).ToString();
        }

        throw new ArgumentException($"No computation for read-only setting: {key}");
    }

    private bool IsForced(string key)
    {
        return _configuration.GetSection($"{ConfigSection}:{key}").Exists();
    }

    private static bool TryConvert(ServerSettingDefinition definition, string value, out object converted)
    {
        try
        {
            if (definition.ValueType == typeof(int))
            {
                converted = int.Parse(value);
                return true;
            }
            if (definition.ValueType == typeof(bool))
            {
                converted = bool.Parse(value);
                return true;
            }
            converted = value;
            return true;
        }
        catch (Exception)
        {
            converted = definition.Default;
            return false;
        }
    }
}
