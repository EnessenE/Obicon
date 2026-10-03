using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Obicon.Server.Configuration;
using Obicon.Server.Data;
using Xunit;

namespace Obicon.Server.Tests;

/// <summary>
/// Tests the server-side node compatibility verdict: the nodes API reports
/// versionSupported per node (same major.minor as the server), so the UI only
/// renders what the server already decided on the node's connection.
/// </summary>
public class NodeVersionSupportTests : IClassFixture<ObiconServerFactory>
{
    private readonly ObiconServerFactory _factory;
    private readonly HttpClient _client;

    public NodeVersionSupportTests(ObiconServerFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new(ObiconServerFactory.AuthHeader);
    }

    /// <summary>
    /// Writes a version onto the node record directly, as the WebSocket
    /// registration handler would when the node connects.
    /// </summary>
    private async Task SetNodeVersionAsync(Guid nodeId, string? version)
    {
        var dbFactory = _factory.Services.GetRequiredService<IDbContextFactory<ObiconDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync();
        var node = await db.Nodes.FindAsync(nodeId);
        Assert.NotNull(node);
        node.Version = version;
        await db.SaveChangesAsync();
    }

    private async Task<JsonElement> CreateNodeAsync()
    {
        var created = await _client.PostAsJsonAsync("/v1/nodes", new { name = "version-node" });
        created.EnsureSuccessStatusCode();
        return await created.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task VersionSupported_IsNull_WhenNodeNeverConnected()
    {
        var created = await CreateNodeAsync();
        Assert.Equal(JsonValueKind.Null, created.GetProperty("versionSupported").ValueKind);
    }

    [Fact]
    public async Task VersionSupported_IsTrue_ForServerVersion()
    {
        var created = await CreateNodeAsync();
        var id = created.GetProperty("id").GetGuid();
        await SetNodeVersionAsync(id, ServerInfo.Version);

        var node = await _client.GetFromJsonAsync<JsonElement>($"/v1/nodes/{id}");
        Assert.True(node.GetProperty("versionSupported").GetBoolean());
    }

    [Fact]
    public async Task VersionSupported_IsFalse_ForOtherMajorMinor()
    {
        var created = await CreateNodeAsync();
        var id = created.GetProperty("id").GetGuid();
        await SetNodeVersionAsync(id, "999.0.0");

        var node = await _client.GetFromJsonAsync<JsonElement>($"/v1/nodes/{id}");
        Assert.False(node.GetProperty("versionSupported").GetBoolean());
    }
}
