using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Obicon.Shared.Models.Messages;

namespace Obicon.Node.Services;

/// <summary>
/// Background task that drains the log capture queue and ships each entry to the
/// server over the WebSocket connection. Entries are only sent while the node is
/// connected and the server announced LogShippingEnabled in its hello; everything
/// else is dropped, so the queue never fills with stale entries.
/// </summary>
public partial class NodeLogShipper : BackgroundService
{
    private const int BatchSize = 20;
    private static readonly TimeSpan IdleDelay = TimeSpan.FromMilliseconds(250);

    private readonly NodeLogSink _sink;
    private readonly IServerConnection _connection;
    private readonly NodeLoggingState _state;
    private readonly ILogger<NodeLogShipper> _logger;

    public NodeLogShipper(NodeLogSink sink, IServerConnection connection, NodeLoggingState state, ILogger<NodeLogShipper> logger)
    {
        _sink = sink;
        _connection = connection;
        _state = state;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarted();

        var reader = _sink.Queue.Reader;
        while (!stoppingToken.IsCancellationRequested)
        {
            NodeLogMessage entry;
            try
            {
                entry = await reader.ReadAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            // Nothing arrives here while shipping is off: drop instead of queueing
            if (!_state.ServerAllowsLogShipping || !_connection.IsConnected)
            {
                await DrainAndDropAsync(reader, stoppingToken);
                continue;
            }

            var shipped = 0;
            try
            {
                await SendEntryAsync(entry, stoppingToken);
                shipped++;

                // Drain whatever else is ready, so bursts leave in one go
                while (shipped < BatchSize && reader.TryRead(out var more))
                {
                    await SendEntryAsync(more, stoppingToken);
                    shipped++;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogShipFailed(ex);
            }

            if (shipped > 0)
            {
                LogShipped(shipped);
            }

            // Give the queue a moment to accumulate before reading again
            if (!reader.TryPeek(out _))
            {
                try
                {
                    await Task.Delay(IdleDelay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task SendEntryAsync(NodeLogMessage entry, CancellationToken cancellationToken)
    {
        await _connection.SendAsync(new WebSocketMessage
        {
            Type = MessageType.NodeLog,
            Data = entry
        });
    }

    private static async Task DrainAndDropAsync(System.Threading.Channels.ChannelReader<NodeLogMessage> reader, CancellationToken cancellationToken)
    {
        while (reader.TryRead(out _))
        {
            // drop; a short yield keeps this from spinning while entries keep arriving
            await Task.Yield();
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Log shipper started")]
    private partial void LogStarted();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not ship log entries to the server")]
    private partial void LogShipFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Shipped {Count} log entries to the server")]
    private partial void LogShipped(int count);
}
