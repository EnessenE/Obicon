namespace Obicon.Server.Configuration;

/// <summary>
/// Parsing helpers for the FrequencyPresetsSeconds setting, shared by the settings
/// service, test validation, and the scheduler loop.
/// </summary>
public static class FrequencyPresets
{
    /// <summary>
    /// Default frequency presets in seconds, used when nothing is configured or the
    /// configured value is unusable.
    /// </summary>
    public static readonly IReadOnlyList<int> Default = new[] { 10, 30, 60, 120, 300, 600, 3600 };

    /// <summary>
    /// Parses a comma-separated list of seconds into a sorted list of distinct positive values.
    /// Falls back to the default presets when nothing usable remains.
    /// </summary>
    public static List<int> Parse(string raw)
    {
        var presets = (raw ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => int.TryParse(part, out var seconds) ? seconds : 0)
            .Where(seconds => seconds > 0)
            .Distinct()
            .OrderBy(seconds => seconds)
            .ToList();

        return presets.Count > 0 ? presets : Default.ToList();
    }

    /// <summary>
    /// The scheduler loop interval in seconds: the lowest configured preset, so the
    /// fastest configured test fires on time.
    /// </summary>
    public static int SchedulerIntervalSeconds(string raw)
    {
        return Math.Max(1, Parse(raw).Min());
    }
}
