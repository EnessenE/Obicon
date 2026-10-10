namespace Obicon.Server.Configuration;

/// <summary>
/// Normalizing helpers for the FrequencyPresetsSeconds setting, shared by the settings
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
    /// Normalizes configured presets into a sorted list of distinct positive values,
    /// falling back to the default presets when nothing usable remains.
    /// </summary>
    public static List<int> Normalize(IEnumerable<int>? presets)
    {
        var normalized = (presets ?? [])
            .Where(seconds => seconds > 0)
            .Distinct()
            .OrderBy(seconds => seconds)
            .ToList();

        return normalized.Count > 0 ? normalized : Default.ToList();
    }

    /// <summary>
    /// The scheduler loop interval in seconds: the lowest configured preset, so the
    /// fastest configured test fires on time.
    /// </summary>
    public static int SchedulerIntervalSeconds(IEnumerable<int>? presets)
    {
        return Math.Max(1, Normalize(presets).Min());
    }
}
