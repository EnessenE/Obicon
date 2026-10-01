using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Obicon.Server.Metrics;
using Obicon.Server.Models.Responses;
using Obicon.Shared.Models.Enums;
using Xunit;

namespace Obicon.Server.Tests;

/// <summary>
/// Tests the test metrics settings: TestMetricsEnabled gates whether finished
/// runs are exported on /metrics, and TestMetricsIncludeNodeLabels attaches the
/// executing node's labels as the node_labels label. Settings changed here are
/// restored, because the fixture database is shared by every test in the class.
/// </summary>
public class TestMetricsTests : IClassFixture<ObiconServerFactory>
{
    private readonly ObiconServerFactory _factory;
    private readonly HttpClient _client;
    private readonly ITestMetricsEmitter _emitter;

    public TestMetricsTests(ObiconServerFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new("uwu");
        _emitter = factory.Services.GetRequiredService<ITestMetricsEmitter>();
    }

    /// <summary>
    /// Creates a node and applies labels to it, returning the node response.
    /// </summary>
    private async Task<NodeResponse> CreateNodeAsync(string name, params string[] labels)
    {
        var created = await _client.PostAsJsonAsync("/v1/nodes", new { name });
        created.EnsureSuccessStatusCode();
        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("id").GetGuid();

        var updated = await _client.PutAsJsonAsync($"/v1/nodes/{id}", new { name, labels });
        updated.EnsureSuccessStatusCode();
        return (await updated.Content.ReadFromJsonAsync<NodeResponse>())!;
    }

    /// <summary>
    /// Scrapes /metrics until the predicate passes, or fails after a timeout.
    /// </summary>
    private async Task<string> ScrapeUntilAsync(Func<string, bool> predicate)
    {
        var last = string.Empty;
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            last = await _client.GetStringAsync("/metrics");
            if (predicate(last))
            {
                return last;
            }
            await Task.Delay(50);
        }
        throw new Xunit.Sdk.XunitException($"Expected series never appeared on /metrics. Body:\n{string.Join("\n", last.Split("\n").Where(l => l.StartsWith("obicon", StringComparison.Ordinal)).Take(40))}");
    }

    [Fact]
    public async Task Defaults_AreEnabled()
    {
        var settings = await _client.GetFromJsonAsync<JsonElement>("/v1/settings");
        var byKey = settings.EnumerateArray().ToDictionary(s => s.GetProperty("key").GetString()!, s => s);
        Assert.Equal("true", byKey["TestMetricsEnabled"].GetProperty("value").GetString());
        Assert.Equal("true", byKey["TestMetricsIncludeNodeLabels"].GetProperty("value").GetString());
    }

    [Fact]
    public async Task Emit_IncludesNodeLabels_WhenEnabled()
    {
        var node = await CreateNodeAsync("labeled-node", "home", "edge");

        await _emitter.EmitAsync(job: null, test: null, node, TestJobStatus.Completed, 123);

        var scrape = await ScrapeUntilAsync(b => b.Contains($"node_id=\"{node.Id}\"") && b.Contains("obicon_tests_runs_total"));
        var series = scrape.Split("\n").First(l => l.Contains($"node_id=\"{node.Id}\"") && l.StartsWith("obicon_tests_runs_total", StringComparison.Ordinal));
        Assert.Contains("node_labels=\"edge,home\"", series);
    }

    [Fact]
    public async Task Emit_OmitsNodeLabels_WhenSettingDisabled()
    {
        await _client.PutAsJsonAsync("/v1/settings/TestMetricsIncludeNodeLabels", new { value = "false" });
        try
        {
            var node = await CreateNodeAsync("unlabeled-metrics-node", "edge");

            await _emitter.EmitAsync(job: null, test: null, node, TestJobStatus.Completed, 123);

            var scrape = await ScrapeUntilAsync(b => b.Contains($"node_id=\"{node.Id}\"") && b.Contains("obicon_tests_runs_total"));
            var series = scrape.Split("\n").First(l => l.Contains($"node_id=\"{node.Id}\"") && l.StartsWith("obicon_tests_runs_total", StringComparison.Ordinal));
            Assert.DoesNotContain("node_labels", series);
        }
        finally
        {
            await _client.PutAsJsonAsync("/v1/settings/TestMetricsIncludeNodeLabels", new { value = "true" });
        }
    }

    [Fact]
    public async Task Emit_IsNotExported_WhenMetricsDisabled()
    {
        var before = await CreateNodeAsync("before-disable-node");
        await _emitter.EmitAsync(job: null, test: null, before, TestJobStatus.Completed, 123);
        await ScrapeUntilAsync(b => b.Contains($"node_id=\"{before.Id}\"") && b.Contains("obicon_tests_runs_total"));

        await _client.PutAsJsonAsync("/v1/settings/TestMetricsEnabled", new { value = "false" });
        try
        {
            var after = await CreateNodeAsync("after-disable-node");
            await _emitter.EmitAsync(job: null, test: null, after, TestJobStatus.Completed, 123);

            // The disabled run is never recorded; give the exporter a moment, then the
            // series must be absent and the previously exported one frozen at 1
            await Task.Delay(TimeSpan.FromSeconds(2));
            var scrape = await _client.GetStringAsync("/metrics");
            Assert.DoesNotContain($"node_id=\"{after.Id}\"", scrape);

            var beforeSeries = scrape.Split("\n").First(l => l.Contains($"node_id=\"{before.Id}\"") && l.StartsWith("obicon_tests_runs_total", StringComparison.Ordinal));
            Assert.EndsWith(" 1", beforeSeries.TrimEnd());
        }
        finally
        {
            await _client.PutAsJsonAsync("/v1/settings/TestMetricsEnabled", new { value = "true" });
        }
    }
}
