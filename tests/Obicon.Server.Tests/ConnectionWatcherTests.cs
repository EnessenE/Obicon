using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Obicon.Server.WebSockets;
using Xunit;
using Xunit.Abstractions;

namespace Obicon.Server.Tests;

/// <summary>
/// Tests the connection watcher: connections whose node record no longer exists
/// are actively closed and removed, instead of heartbeating as connected ghosts.
/// </summary>
public class ConnectionWatcherTests : LoggedTest, IClassFixture<ObiconServerFactory>
{
    private readonly ObiconServerFactory _factory;
    private readonly HttpClient _client;

    public ConnectionWatcherTests(ITestOutputHelper output, ObiconServerFactory factory)
        : base(output)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new(ObiconServerFactory.AuthHeader);
    }

    [Fact]
    public async Task CheckConnections_RemovesGhostConnectionForDeletedNode()
    {
        var manager = _factory.Services.GetRequiredService<NodeConnectionManager>();
        var watcher = Watcher();

        // A node connects, then its record disappears (e.g. deleted while connected)
        var node = await _client.PostAsJsonAsync("/v1/nodes", new { name = "ghost-node" });
        var created = await node.Content.ReadFromJsonAsync<JsonElement>();
        var nodeId = created.GetProperty("id").GetString()!;

        Assert.True(manager.TryAddConnection(nodeId, "ghost-node", new FakeWebSocket()));

        var delete = await _client.DeleteAsync($"/v1/nodes/{nodeId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        // The delete closes the socket, but the connection entry stays until the
        // receive loop unwinds: exactly the ghost state the watcher must clean up
        Assert.NotNull(manager.GetConnection(nodeId));

        await watcher.CheckConnectionsAsync();

        Assert.Null(manager.GetConnection(nodeId));
    }

    [Fact]
    public async Task CheckConnections_KeepsConnectionsForExistingNodes()
    {
        var manager = _factory.Services.GetRequiredService<NodeConnectionManager>();
        var watcher = Watcher();

        var node = await _client.PostAsJsonAsync("/v1/nodes", new { name = "live-node" });
        var created = await node.Content.ReadFromJsonAsync<JsonElement>();
        var nodeId = created.GetProperty("id").GetString()!;

        try
        {
            Assert.True(manager.TryAddConnection(nodeId, "live-node", new FakeWebSocket()));

            await watcher.CheckConnectionsAsync();

            Assert.NotNull(manager.GetConnection(nodeId));
        }
        finally
        {
            await _client.DeleteAsync($"/v1/nodes/{nodeId}");
        }
    }

    private ConnectionWatcher Watcher()
    {
        return _factory.Services.GetRequiredService<IEnumerable<IHostedService>>()
            .OfType<ConnectionWatcher>()
            .First();
    }

    /// <summary>
    /// A WebSocket stub: open, accepts sends and closes without doing anything.
    /// </summary>
    private sealed class FakeWebSocket : WebSocket
    {
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override WebSocketState State => WebSocketState.Open;
        public override string SubProtocol => string.Empty;

        public override void Abort() { }
        public override void Dispose() { }

        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
            => throw new OperationCanceledException(cancellationToken);

        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
