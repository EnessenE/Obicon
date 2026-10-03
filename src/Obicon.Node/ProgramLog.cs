using Microsoft.Extensions.Logging;

/// <summary>
/// LoggerMessage-based logging helpers for the top-level statements in Program,
/// which cannot declare partial methods of their own.
/// </summary>
internal static partial class ProgramLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Obicon Node v{Version} starting; metrics on http://{MetricsHost}:{MetricsPort}/metrics")]
    internal static partial void LogStarting(ILogger logger, string version, string metricsHost, int metricsPort);
}
