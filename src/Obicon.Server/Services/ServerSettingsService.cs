using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Obicon.Server.Configuration;
using Obicon.Server.Data;
using Obicon.Server.Models;
using Obicon.Server.Models.Responses;

namespace Obicon.Server.Services;

/// <summary>
/// Resolves server settings: values pinned by appsettings or environment variables are forced
/// and read-only; everything else can be overridden at runtime (stored in the database).
/// Effective values are cached and the cache is invalidated on change.
/// </summary>
public partial class ServerSettingsService : IServerSettingsService
{
    private const string ConfigSection = "ServerSettings";

    private static readonly JsonSerializerOptions ListOptions = new(JsonSerializerDefaults.Web);

    private readonly IDbContextFactory<ObiconDbContext> _dbFactory;

    private readonly IConfiguration _configuration;
    private readonly NodePolicyBroadcaster _policyBroadcaster;
    private readonly ILogger<ServerSettingsService> _logger;

    private readonly ConcurrentDictionary<string, object> _cache = new();

    public ServerSettingsService(
        IDbContextFactory<ObiconDbContext> dbFactory,
        IConfiguration configuration,
        NodePolicyBroadcaster policyBroadcaster,
        ILogger<ServerSettingsService> logger)
    {
        _dbFactory = dbFactory;

        _configuration = configuration;
        _policyBroadcaster = policyBroadcaster;
        _logger = logger;
    }

    public async Task<IEnumerable<ServerSettingResponse>> GetAllAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var overrides = await db.ServerSettingValues.ToDictionaryAsync(s => s.Key, s => s.Value);

        return ServerSettingDefinitions.All.Select(definition =>
        {
            object value;
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
                    value = _configuration[$"{ConfigSection}:{definition.Key}"] ?? definition.Default;
                }
                else if (overrides.TryGetValue(definition.Key, out var stored))
                {
                    source = "Database";
                    value = stored;
                }
                else
                {
                    value = definition.Default;
                }

                // Collection settings carry their typed list on the wire, whatever
                // their source; scalar settings already hold their plain string
                value = TryConvert(definition, value, out var converted) ? converted : value;
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

    public async Task<ServerSettingResponse> SetAsync(string key, JsonElement value)
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

        var canonical = Canonicalize(definition, value);

        await _dbFactory.ExecuteAsync(async db =>
        {
            var stored = await db.ServerSettingValues.FindAsync(key);
            if (stored == null)
            {
                db.ServerSettingValues.Add(new ServerSettingValue { Key = key, Value = canonical, UpdatedAt = DateTime.UtcNow });
            }
            else
            {
                stored.Value = canonical;
                stored.UpdatedAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();
        });

        _cache.TryRemove(key, out _);
        LogSettingChanged(key, key == "AuthHeader" ? "***" : canonical);
        Metrics.ServerMetrics.Action("setting_changed");

        // Node-facing settings propagate to connected nodes immediately
        if (key is "NodeLogShippingEnabled" or "NodeLocalLoggingEnabled" or "NodeExternalIpResolvingEnabled")
        {
            await _policyBroadcaster.BroadcastAsync(
                await GetAsync<bool>("NodeLogShippingEnabled"),
                await GetAsync<bool>("NodeLocalLoggingEnabled"),
                await GetAsync<bool>("NodeExternalIpResolvingEnabled"));
        }

        return new ServerSettingResponse
        {
            Key = definition.Key,
            Description = definition.Description,
            Value = TryConvert(definition, canonical, out var converted) ? converted : canonical,
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
    /// The effective raw value of a stored setting: forced configuration, database override,
    /// or default. Collection defaults serialize to their JSON text form here.
    /// </summary>
    private string EffectiveRawValue(ServerSettingDefinition definition, Dictionary<string, string> overrides)
    {
        if (IsForced(definition.Key))
        {
            return _configuration[$"{ConfigSection}:{definition.Key}"] ?? definition.Default as string ?? string.Empty;
        }

        if (overrides.TryGetValue(definition.Key, out var stored))
        {
            return stored;
        }

        return definition.Default as string ?? JsonSerializer.Serialize(definition.Default, ListOptions);
    }

    /// <summary>
    /// Computes the value of a read-only setting from the settings it derives from.
    /// </summary>
    private static string ComputeReadOnly(string key, string frequencyPresets)
    {
        if (key == "SchedulerLoopIntervalSeconds")
        {
            List<int>? presets = null;
            try
            {
                presets = JsonSerializer.Deserialize<List<int>>(frequencyPresets, ListOptions);
            }
            catch (JsonException)
            {
                // Normalize falls back to the default presets for anything unusable
            }

            return FrequencyPresets.SchedulerIntervalSeconds(presets)
                .ToString(CultureInfo.InvariantCulture);
        }

        throw new ArgumentException($"No computation for read-only setting: {key}");
    }

    private bool IsForced(string key)
    {
        return _configuration.GetSection($"{ConfigSection}:{key}").Exists();
    }

    /// <summary>
    /// Converts a raw stored value to the setting's type. Collection settings parse their
    /// JSON text into the typed list; an unusable collection value yields the empty list,
    /// so consumers apply their documented fallback (defaults, or "all types").
    /// </summary>
    private static bool TryConvert(ServerSettingDefinition definition, object value, out object converted)
    {
        try
        {
            // A value already of the setting's type (e.g. a typed default) passes through
            if (definition.ValueType.IsInstanceOfType(value))
            {
                converted = value;
                return true;
            }

            var raw = value.ToString() ?? string.Empty;
            if (definition.ValueType == typeof(int))
            {
                converted = int.Parse(raw, CultureInfo.InvariantCulture);
                return true;
            }
            if (definition.ValueType == typeof(bool))
            {
                converted = bool.Parse(raw);
                return true;
            }
            if (definition.ValueType == typeof(List<string>))
            {
                converted = DeserializeList(raw) ?? new List<string>();
                return true;
            }
            if (definition.ValueType == typeof(List<int>))
            {
                var items = DeserializeList(raw);
                if (items == null || items.All(item => int.TryParse(item, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)))
                {
                    converted = items == null
                        ? new List<int>()
                        : items.Select(item => int.Parse(item, NumberStyles.Integer, CultureInfo.InvariantCulture)).ToList();
                    return true;
                }
                converted = new List<int>();
                return false;
            }

            converted = raw;
            return true;
        }
        catch (Exception)
        {
            converted = definition.Default;
            return false;
        }
    }

    /// <summary>
    /// Deserializes a JSON array of strings; null when the value is empty or unparseable.
    /// </summary>
    private static List<string>? DeserializeList(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(raw, ListOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Converts the incoming JSON value of a PUT to the canonical text stored in the
    /// database: plain text for scalar settings, JSON array text for collections.
    /// </summary>
    private static string Canonicalize(ServerSettingDefinition definition, JsonElement value)
    {
        if (definition.ValueType == typeof(bool))
        {
            return value.ValueKind switch
            {
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed ? "true" : "false",
                _ => throw new ArgumentException($"Setting {definition.Key} expects a bool value")
            };
        }

        if (definition.ValueType == typeof(int))
        {
            var parsed = value.ValueKind switch
            {
                JsonValueKind.Number => int.TryParse(value.GetRawText(), out var number) ? number : (int?)null,
                JsonValueKind.String when int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) => number,
                _ => null
            };
            return parsed?.ToString(CultureInfo.InvariantCulture)
                ?? throw new ArgumentException($"Setting {definition.Key} expects an int value");
        }

        if (definition.ValueType == typeof(List<string>))
        {
            if (value.ValueKind != JsonValueKind.Array)
            {
                throw new ArgumentException($"Setting {definition.Key} expects a list of strings");
            }
            var items = value.EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.GetRawText())
                .ToList();
            return JsonSerializer.Serialize(items, ListOptions);
        }

        if (definition.ValueType == typeof(List<int>))
        {
            if (value.ValueKind != JsonValueKind.Array)
            {
                throw new ArgumentException($"Setting {definition.Key} expects a list of integers");
            }
            var items = value.EnumerateArray().Select(item => item.ToString()).ToList();
            if (!items.All(item => int.TryParse(item, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)))
            {
                throw new ArgumentException($"Setting {definition.Key} expects a list of integers");
            }
            return JsonSerializer.Serialize(items, ListOptions);
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : throw new ArgumentException($"Setting {definition.Key} expects a string value");
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Setting {Key} changed to {Value}")]
    private partial void LogSettingChanged(string key, string value);
}
