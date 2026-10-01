using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Obicon.Server.Data;

namespace Obicon.Server.WebSockets;

/// <summary>
/// Periodically verifies that every live node connection still belongs to an
/// existing node, and closes the ones that don't. This catches connections that
/// outlived their node record, e.g. a node deleted while connected: without this
/// check such a connection keeps heartbeating as a connected ghost that no
/// longer shows up in the node list.
/// </summary>
public class ConnectionWatcher : BackgroundService
{
    private readonly NodeConnectionManager _connectionManager;
    private readonly IDbContextFactory<ObiconDbContext> _dbFactory;
    private readonly ILogger<ConnectionWatcher> _logger;

    public ConnectionWatcher(
        NodeConnectionManager connectionManager,
        IDbContextFactory<ObiconDbContext> dbFactory,
        ILogger<ConnectionWatcher> logger)
    {
        _connectionManager = connectionManager;
        _dbFactory = dbFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (!stoppingToken.IsCancellationRequested)
        {
            await CheckConnectionsAsync(stoppingToken);

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                {
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Closes every live connection whose node record no longer exists.
    /// </summary>
    public async Task CheckConnectionsAsync(CancellationToken cancellationToken = default)
    {
        var connections = _connectionManager.GetAllConnections().ToList();
        if (connections.Count == 0)
        {
            return;
        }

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var existingIds = (await db.Nodes
            .Select(n => n.Id)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        foreach (var connection in connections)
        {
            if (Guid.TryParse(connection.NodeId, out var id) && existingIds.Contains(id))
            {
                continue;
            }

            _logger.LogWarning(
                "Closing ghost connection from node {NodeId} ({NodeName}): the node no longer exists",
                connection.NodeId, connection.NodeName);
            await _connectionManager.DisconnectNodeAsync(connection.NodeId, "Node no longer exists");
            // The close alone is noticed by the receive loop; remove eagerly so the
            // ghost is gone immediately even if that loop is still unwinding
            _connectionManager.TryRemoveConnection(connection.NodeId);
        }
    }
}
