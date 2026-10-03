using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Obicon.Server.Tests;

/// <summary>
/// Tests pool membership cleanup: deleting a node removes its ID from every
/// pool it belongs to, so no stale ID keeps targeting a missing node.
/// </summary>
public class NodePoolMembershipTests : IClassFixture<ObiconServerFactory>
{
    private readonly HttpClient _client;

    public NodePoolMembershipTests(ObiconServerFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new(ObiconServerFactory.AuthHeader);
    }

    [Fact]
    public async Task DeleteNode_RemovesNodeFromAllPools()
    {
        var node = await _client.PostAsJsonAsync("/v1/nodes", new { name = "pool-member" });
        var nodeId = (await node.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        var pool = await _client.PostAsJsonAsync("/v1/pools", new { name = "edge-pool" });
        var poolId = (await pool.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        var setMembers = await _client.PutAsJsonAsync($"/v1/pools/{poolId}/nodes", new { nodeIds = new[] { nodeId } });
        Assert.True(setMembers.IsSuccessStatusCode, $"Setting pool members failed: {setMembers.StatusCode}");

        var delete = await _client.DeleteAsync($"/v1/nodes/{nodeId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var updated = await _client.GetFromJsonAsync<JsonElement>($"/v1/pools/{poolId}");
        var remaining = updated.GetProperty("nodeIds").EnumerateArray().ToList();
        Assert.DoesNotContain(remaining, id => id.GetString() == nodeId);
    }

    [Fact]
    public async Task DeleteNode_LeavesOtherPoolMembersIntact()
    {
        var kept = await _client.PostAsJsonAsync("/v1/nodes", new { name = "kept-node" });
        var keptId = (await kept.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
        var removed = await _client.PostAsJsonAsync("/v1/nodes", new { name = "removed-node" });
        var removedId = (await removed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        var pool = await _client.PostAsJsonAsync("/v1/pools", new { name = "mixed-pool" });
        var poolId = (await pool.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        var setMembers = await _client.PutAsJsonAsync($"/v1/pools/{poolId}/nodes", new { nodeIds = new[] { keptId, removedId } });
        Assert.True(setMembers.IsSuccessStatusCode, $"Setting pool members failed: {setMembers.StatusCode}");

        var delete = await _client.DeleteAsync($"/v1/nodes/{removedId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var updated = await _client.GetFromJsonAsync<JsonElement>($"/v1/pools/{poolId}");
        var remaining = updated.GetProperty("nodeIds").EnumerateArray().Select(id => id.GetString()).ToList();
        Assert.Equal(new[] { keptId }, remaining);
    }
}
