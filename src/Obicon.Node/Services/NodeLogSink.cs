using System.Globalization;
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using Obicon.Node.Configuration;
using Obicon.Shared.Models.Messages;
using Serilog.Core;
using Serilog.Events;

namespace Obicon.Node.Services;

/// <summary>
/// Serilog sink that captures every log event flowing through the node's logging
/// pipeline and queues it for shipping to the server. Sits next to the console
/// sink, so it sees the same events with all their metadata: level, rendered
/// message, exception, source context, and scope properties (e.g. JobId).
/// Entries from the shipping pipeline itself are skipped to avoid feedback loops.
/// </summary>
public class NodeLogSink : ILogEventSink
{
    /// <summary>
    /// Source contexts whose own log output must never be shipped, or every shipped
    /// entry would generate another entry.
    /// </summary>
    public const string ShippingSourcePrefix = "Obicon.Node.Services.NodeLogShipper";

    private const int MaxPropertyValueLength = 500;
    private const string SourceContextKey = "SourceContext";
    private const string MachineNameKey = "MachineName";

    private readonly NodeSettings _settings;
    private readonly NodeIdentityStore _identityStore;
    private readonly LogEventLevel _minimumLevel;

    /// <summary>
    /// Bounded queue of entries waiting to be shipped; oldest entries drop when full.
    /// </summary>
    public Channel<NodeLogMessage> Queue { get; } = System.Threading.Channels.Channel.CreateBounded<NodeLogMessage>(
        new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropOldest });

    public NodeLogSink(IOptions<NodeSettings> settings, NodeIdentityStore identityStore)
    {
        _settings = settings.Value;
        _identityStore = identityStore;
        _minimumLevel = ParseLevel(_settings.LogShippingMinLevel);
    }

    public void Emit(LogEvent logEvent)
    {
        if (logEvent.Level < _minimumLevel)
        {
            return;
        }

        // Never ship the shipping pipeline's own output: it would recurse
        if (logEvent.Properties.TryGetValue(SourceContextKey, out var sourceContext) &&
            sourceContext.ToString().Trim('"').StartsWith(ShippingSourcePrefix, StringComparison.Ordinal))
        {
            return;
        }

        var properties = new Dictionary<string, string>();
        foreach (var property in logEvent.Properties)
        {
            if (property.Key == MachineNameKey)
            {
                continue; // carried in the message envelope already
            }

            var value = property.Value?.ToString() ?? string.Empty;
            if (value.Length > MaxPropertyValueLength)
            {
                value = value[..MaxPropertyValueLength] + "…";
            }
            properties[TruncateKey(property.Key)] = value.Trim('"');
        }

        var nodeLog = new NodeLogMessage
        {
            NodeId = _identityStore.NodeId ?? string.Empty,
            NodeName = string.IsNullOrWhiteSpace(_settings.NodeName)
                ? Environment.MachineName
                : _settings.NodeName,
            NodeVersion = NodeInfo.Version,
            Timestamp = logEvent.Timestamp.UtcDateTime,
            Level = MapLevel(logEvent.Level),
            Message = logEvent.RenderMessage(CultureInfo.InvariantCulture),
            Exception = logEvent.Exception?.ToString(),
            Properties = properties.Count > 0 ? properties : null
        };

        Queue.Writer.TryWrite(nodeLog);
    }

    private static LogEventLevel ParseLevel(string value)
    {
        return Enum.TryParse<LogEventLevel>(value, ignoreCase: true, out var level) ? level : LogEventLevel.Information;
    }

    private static string MapLevel(LogEventLevel level) => level switch
    {
        LogEventLevel.Verbose or LogEventLevel.Debug => "Debug",
        LogEventLevel.Information => "Information",
        LogEventLevel.Warning => "Warning",
        _ => "Error"
    };

    private static string TruncateKey(string key)
    {
        return key.Length <= 200 ? key : key[..200] + "…";
    }
}
