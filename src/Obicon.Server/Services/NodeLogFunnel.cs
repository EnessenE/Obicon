using Obicon.Shared.Models.Messages;
using OpenTelemetry.Logs;

namespace Obicon.Server.Services;

/// <summary>
/// Forwards accepted node log entries into the OpenTelemetry logging pipeline, so they
/// leave through the OTLP logs exporter like every other log record. The node's identity
/// and the entry's own properties ride along as first-class fields.
/// </summary>
public interface INodeLogFunnel
{
    /// <summary>
    /// Forwards one accepted node log entry. The entry's attributes must survive into
    /// the exported record: node_id, node_name, node_version, source_context, and every
    /// shipped property (e.g. JobId, TestId) as fields.
    /// </summary>
    void Forward(string nodeId, NodeLogMessage entry);
}

/// <summary>
/// Default <see cref="INodeLogFunnel"/>: writes entries through a dedicated logger built
/// on the OpenTelemetry logger provider, keeping them out of the server's Serilog console
/// pipeline (which is fed separately by ShipNodeLogsToConsole).
/// </summary>
public class NodeLogFunnel : INodeLogFunnel
{
    /// <summary>
    /// Logger category of every forwarded node entry; the source_context field carries
    /// the node-side class that produced the entry.
    /// </summary>
    public const string LoggerCategory = "Obicon.NodeLog";

    private readonly ILogger _logger;

    public NodeLogFunnel(OpenTelemetryLoggerProvider provider)
    {
        _logger = LoggerFactory.Create(builder => builder.AddProvider(provider)).CreateLogger(LoggerCategory);
    }

    /// <summary>
    /// Test constructor: builds the funnel on any logger factory, e.g. one carrying an
    /// in-memory exporter to assert the attribute contract.
    /// </summary>
    internal NodeLogFunnel(ILoggerFactory factory)
    {
        _logger = factory.CreateLogger(LoggerCategory);
    }

    public void Forward(string nodeId, NodeLogMessage entry)
    {
        var level = MapLevel(entry.Level);
        if (!_logger.IsEnabled(level))
        {
            // Nothing exports at this level (e.g. no OTLP endpoint configured);
            // skip building the state instead of paying for a dropped record
            return;
        }

        var message = entry.Message;
        var state = new List<KeyValuePair<string, object?>>
        {
            new("node_id", nodeId),
            new("node_name", entry.NodeName),
            new("node_version", entry.NodeVersion),
            new("source_context", Extract(entry.Properties, "SourceContext") ?? "unknown")
        };

        // The entry's own properties become fields verbatim: JobId and TestId are the
        // join keys into the metrics, everything else rides along unchanged
        if (entry.Properties is { Count: > 0 } properties)
        {
            foreach (var property in properties)
            {
                state.Add(new KeyValuePair<string, object?>(property.Key, property.Value));
            }
        }

        _logger.Log(level, 0, state, null, (_, _) => message);
    }

    private static string? Extract(Dictionary<string, string>? properties, string key)
    {
        return properties is { Count: > 0 } && properties.TryGetValue(key, out var value)
            ? value.Trim('"')
            : null;
    }

    private static LogLevel MapLevel(string level)
    {
        return level switch
        {
            "Error" => LogLevel.Error,
            "Warning" => LogLevel.Warning,
            "Debug" => LogLevel.Debug,
            _ => LogLevel.Information
        };
    }
}
