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
        var overrides = await LoadOverridesAsync(db);

        return ServerSettingDefinitions.All.Select(definition =>
        {
            object value;
            var source = "Default";
            var forced = false;

            if (definition.IsReadOnly)
            {
                source = "Derived";
                value = ComputeReadOnly(definition.Key, EffectivePresets(overrides));
            }
            else
            {
                forced = IsForced(definition.Key);
                if (forced)
                {
                    source = "Configuration (forced)";
                }
                else if (overrides.ContainsKey(definition.Key))
                {
                    source = "Database";
                }

                // Collection settings carry their typed list on the wire, whatever
                // their source; scalar settings already hold their plain string
                value = TryConvert(definition, EffectiveValue(definition, overrides), out var converted) ? converted : definition.Default;
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
            // List settings replace their item rows wholesale and keep a count row in
            // server_setting_values, so an explicitly empty list stays distinct from
            // "no override"; scalar settings upsert their text row
            if (canonical is List<string> items)
            {
                var rows = await db.ServerSettingListValues.Where(v => v.Key == key).ToListAsync();
                db.ServerSettingListValues.RemoveRange(rows);
                db.ServerSettingListValues.AddRange(items.Select((item, position) => new ServerSettingListValue
                {
                    Key = key,
                    Position = position,
                    Item = item
                }));

                var count = await db.ServerSettingValues.FindAsync(key);
                var countText = items.Count.ToString(CultureInfo.InvariantCulture);
                if (count == null)
                {
                    db.ServerSettingValues.Add(new ServerSettingValue { Key = key, Value = countText, UpdatedAt = DateTime.UtcNow });
                }
                else
                {
                    count.Value = countText;
                    count.UpdatedAt = DateTime.UtcNow;
                }
            }
            else
            {
                var stored = await db.ServerSettingValues.FindAsync(key);
                if (stored == null)
                {
                    db.ServerSettingValues.Add(new ServerSettingValue { Key = key, Value = (string)canonical, UpdatedAt = DateTime.UtcNow });
                }
                else
                {
                    stored.Value = (string)canonical;
                    stored.UpdatedAt = DateTime.UtcNow;
                }
            }

            await db.SaveChangesAsync();
        });

        _cache.TryRemove(key, out _);
        LogSettingChanged(key, key == "AuthHeader" ? "***" : CanonicalLogText(canonical));
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
        var overrides = await LoadOverridesAsync(db);

        var value = definition.IsReadOnly
            ? ComputeReadOnly(definition.Key, EffectivePresets(overrides))
            : TryConvert(definition, EffectiveValue(definition, overrides), out var result)
                ? result
                : throw new InvalidOperationException($"Stored value for {key} is not a valid {definition.ValueType.Name}");

        _cache[key] = value;
        return (T)value;
    }

    private static ServerSettingDefinition FindDefinition(string key)
    {
        return ServerSettingDefinitions.All.First(d => d.Key == key);
    }

    /// <summary>
    /// Loads every runtime override: scalar settings as their stored text, list
    /// settings as their ordered item rows. A list override's count row in
    /// server_setting_values stays behind as the presence marker when its list is
    /// explicitly empty.
    /// </summary>
    private static async Task<Dictionary<string, object>> LoadOverridesAsync(ObiconDbContext db)
    {
        var overrides = new Dictionary<string, object>();
        foreach (var scalar in await db.ServerSettingValues.ToListAsync())
        {
            overrides[scalar.Key] = scalar.Value;
        }
        foreach (var group in (await db.ServerSettingListValues.ToListAsync()).GroupBy(v => v.Key))
        {
            overrides[group.Key] = group.OrderBy(v => v.Position).Select(v => v.Item).ToList();
        }
        return overrides;
    }

    /// <summary>
    /// The effective value of a setting: forced configuration, database override, or
    /// the typed default. Configuration scalars arrive as text and native arrays bind
    /// to the setting's type, so forced lists never need JSON-in-string forms.
    /// </summary>
    private object EffectiveValue(ServerSettingDefinition definition, Dictionary<string, object> overrides)
    {
        if (IsForced(definition.Key))
        {
            var section = _configuration.GetSection($"{ConfigSection}:{definition.Key}");
            return section.Value ?? section.Get(definition.ValueType) ?? definition.Default;
        }

        if (overrides.TryGetValue(definition.Key, out var stored))
        {
            return stored;
        }

        return definition.Default;
    }

    /// <summary>
    /// The effective FrequencyPresetsSeconds list, normalized for the scheduler's
    /// read-only setting.
    /// </summary>
    private List<int> EffectivePresets(Dictionary<string, object> overrides)
    {
        var definition = FindDefinition("FrequencyPresetsSeconds");
        return TryConvert(definition, EffectiveValue(definition, overrides), out var converted)
            ? (List<int>)converted
            : [];
    }

    /// <summary>
    /// Computes the value of a read-only setting from the settings it derives from.
    /// </summary>
    private static int ComputeReadOnly(string key, List<int> frequencyPresets)
    {
        if (key == "SchedulerLoopIntervalSeconds")
        {
            return FrequencyPresets.SchedulerIntervalSeconds(frequencyPresets);
        }

        throw new ArgumentException($"No computation for read-only setting: {key}");
    }

    private bool IsForced(string key)
    {
        return _configuration.GetSection($"{ConfigSection}:{key}").Exists();
    }

    /// <summary>
    /// Converts an effective value to the setting's type: values already of the
    /// setting's type pass through (typed defaults, bound configuration, stored list
    /// rows), scalar text parses, and an unusable collection value yields the empty
    /// list, so consumers apply their documented fallback (defaults, or "all types").
    /// </summary>
    private static bool TryConvert(ServerSettingDefinition definition, object value, out object converted)
    {
        try
        {
            if (definition.ValueType.IsInstanceOfType(value))
            {
                converted = value;
                return true;
            }

            var raw = value.ToString() ?? string.Empty;
            if (definition.ValueType == typeof(int))
            {
                converted = int.Parse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture);
                return true;
            }
            if (definition.ValueType == typeof(bool))
            {
                converted = bool.Parse(raw);
                return true;
            }
            // Scalar text for a list definition is the presence marker of an
            // explicitly empty list override (a non-empty override has its items
            // in server_setting_list_values instead)
            if (definition.ValueType == typeof(List<string>))
            {
                converted = value as List<string> ?? [];
                return true;
            }
            if (definition.ValueType == typeof(List<int>))
            {
                var items = value as List<string> ?? [];
                if (items.All(item => int.TryParse(item, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)))
                {
                    converted = items
                        .Select(item => int.Parse(item, NumberStyles.Integer, CultureInfo.InvariantCulture))
                        .ToList();
                    return true;
                }
                converted = new List<int>();
                return false;
            }

            converted = raw;
            return definition.ValueType == typeof(string);
        }
        catch (Exception)
        {
            if (definition.ValueType == typeof(List<string>))
            {
                converted = new List<string>();
            }
            else if (definition.ValueType == typeof(List<int>))
            {
                converted = new List<int>();
            }
            else
            {
                converted = definition.Default;
            }
            return false;
        }
    }

    /// <summary>
    /// Converts the incoming JSON value of a PUT to its canonical stored form:
    /// plain text for scalar settings, the ordered item list for collections.
    /// </summary>
    private static object Canonicalize(ServerSettingDefinition definition, JsonElement value)
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
            return value.EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.GetRawText())
                .ToList()!;
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
            return items;
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : throw new ArgumentException($"Setting {definition.Key} expects a string value");
    }

    /// <summary>
    /// Human-readable form of a canonical value for the change log line: scalar text
    /// as-is, list items comma-joined.
    /// </summary>
    private static string CanonicalLogText(object canonical)
    {
        return canonical is List<string> items ? string.Join(", ", items) : (string)canonical;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Setting {Key} changed to {Value}")]
    private partial void LogSettingChanged(string key, string value);
}
