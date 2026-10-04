using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace Obicon.Server.Tests;

/// <summary>
/// Tests direct test-target cleanup: deleting a node removes its ID from every
/// test that directly targets it, so no stale ID keeps queueing jobs for a
/// node that no longer exists (the scheduler and manual runs resolve targets
/// the same way, so both paths are covered by exercising the manual trigger).
/// </summary>
public class NodeTargetCleanupTests : LoggedTest, IClassFixture<ObiconServerFactory>
{
    private readonly HttpClient _client;

    public NodeTargetCleanupTests(ITestOutputHelper output, ObiconServerFactory factory)
        : base(output)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new(ObiconServerFactory.AuthHeader);
    }

    private async Task<string> CreateNodeAsync(string name)
    {
        var created = await _client.PostAsJsonAsync("/v1/nodes", new { name });
        created.EnsureSuccessStatusCode();
        return (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    [Fact]
    public async Task DeleteNode_RemovesNodeFromDirectTestTargets()
    {
        var keptId = await CreateNodeAsync("target-kept");
        var removedId = await CreateNodeAsync("target-removed");

        var created = await _client.PostAsJsonAsync("/v1/tests", new
        {
            name = "target-cleanup-test",
            type = 4,
            target = "example.com:443",
            nodeIds = new[] { removedId, keptId },
            frequency = 60,
            isActive = true,
            timeoutSeconds = 30
        });
        created.EnsureSuccessStatusCode();
        var testId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        var delete = await _client.DeleteAsync($"/v1/nodes/{removedId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        // The stale ID is gone from the test's direct targets; the surviving node stays
        var test = await _client.GetFromJsonAsync<JsonElement>($"/v1/tests/{testId}");
        var nodeIds = test.GetProperty("nodeIds").EnumerateArray().Select(n => n.GetString()).ToList();
        Assert.DoesNotContain(removedId, nodeIds);
        Assert.Contains(keptId, nodeIds);
    }

    [Fact]
    public async Task DeleteNode_DirectlyTargetedTest_QueuesNoJobsForDeletedNode()
    {
        var removedId = await CreateNodeAsync("queue-removed");

        var created = await _client.PostAsJsonAsync("/v1/tests", new
        {
            name = "queue-cleanup-test",
            type = 4,
            target = "example.com:443",
            nodeIds = new[] { removedId },
            frequency = 60,
            isActive = true,
            timeoutSeconds = 30
        });
        created.EnsureSuccessStatusCode();
        var testId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        var delete = await _client.DeleteAsync($"/v1/nodes/{removedId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        // A run of the now-empty test creates no jobs at all - before the fix this
        // queued a job for the deleted node on every due interval
        var run = await _client.PostAsync($"/v1/tests/{testId}/run", content: null);
        Assert.True(run.IsSuccessStatusCode, $"Triggering the run failed: {run.StatusCode}");

        var queue = await _client.GetFromJsonAsync<JsonElement[]>("/v1/queue") ?? [];
        Assert.DoesNotContain(queue, j => j.GetProperty("testId").GetString() == testId);
    }
}
