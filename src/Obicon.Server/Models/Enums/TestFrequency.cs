namespace Obicon.Server.Models.Enums;

/// <summary>
/// Frequency intervals for test execution.
/// </summary>
public enum TestFrequency
{
    /// <summary>
    /// Execute test every 10 seconds.
    /// </summary>
    TenSeconds,

    /// <summary>
    /// Execute test every 30 seconds.
    /// </summary>
    ThirtySeconds,

    /// <summary>
    /// Execute test every 1 minute.
    /// </summary>
    OneMinute,

    /// <summary>
    /// Execute test every 2 minutes.
    /// </summary>
    TwoMinutes,

    /// <summary>
    /// Execute test every 5 minutes.
    /// </summary>
    FiveMinutes,

    /// <summary>
    /// Execute test every 10 minutes.
    /// </summary>
    TenMinutes,

    /// <summary>
    /// Execute test every 1 hour.
    /// </summary>
    OneHour
}
