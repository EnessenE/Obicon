namespace Obicon.Server.Tests;

/// <summary>
/// Recurring setting values shared across the server tests, hoisted into static
/// readonly fields per CA1861: constant array arguments passed to frequently
/// called methods allocate on every call.
/// </summary>
internal static class TestDefaults
{
    /// <summary>
    /// The default TestMetricsLabels setting value.
    /// </summary>
    public static readonly string[] MetricLabels = ["test_type", "test_name", "node_name", "node_labels"];

    /// <summary>
    /// The default FrequencyPresetsSeconds setting value.
    /// </summary>
    public static readonly int[] FrequencyPresets = [10, 30, 60, 120, 300, 600, 3600];

    /// <summary>
    /// A short FrequencyPresetsSeconds override used by several tests.
    /// </summary>
    public static readonly int[] OverrideFrequencyPresets = [15, 45];
}
