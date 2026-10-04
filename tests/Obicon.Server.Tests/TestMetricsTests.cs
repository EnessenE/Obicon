using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Obicon.Server.Metrics;
using Obicon.Server.Models.Responses;
using Obicon.Shared.Models.Enums;
using Xunit;
using Xunit.Abstractions;

namespace Obicon.Server.Tests;

/// <summary>
/// Tests the test metrics settings: TestMetricsEnabled gates whether finished runs are
/// exported on /metrics, and TestMetricsLabels selects which labels ride along - with
/// test_id and the counter's status as a forced floor. Settings changed here are
/// restored, because the fixture database is shared by every test in the class.
/// </summary>
public class TestMetricsTests : LoggedTest, IClassFixture<ObiconServerFactory>
{
    private readonly ObiconServerFactory _factory;
    private readonly HttpClient _client;
    private readonly ITestMetricsEmitter _emitter;

    public TestMetricsTests(ITestOutputHelper output, ObiconServerFactory factory)
        : base(output)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new(ObiconServerFactory.AuthHeader);
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
        throw new Xunit.Sdk.XunitException($"Expected series never appeared on /metrics. Body:\n{string.Join("\n", last.Split("\n").Where(l => l.StartsWith("obicon", StringComparison.Ordinal)).Take(40))}\n");
    }

    /// <summary>
    /// Scrapes the runs counter series for a node identified by its name - the default
    /// label set carries node_name, not node_id.
    /// </summary>
    private async Task<string> RunSeriesForAsync(string nodeName)
    {
        var scrape = await ScrapeUntilAsync(b => b.Contains($"node_name=\"{nodeName}\"") && b.Contains("obicon_tests_runs_total"));
        return scrape.Split("\n").First(l => l.Contains($"node_name=\"{nodeName}\"") && l.StartsWith("obicon_tests_runs_total", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Defaults_AreEnabledWithTheDefaultLabelSet()
    {
        var settings = await _client.GetFromJsonAsync<JsonElement>("/v1/settings");
        var byKey = settings.EnumerateArray().ToDictionary(s => s.GetProperty("key").GetString()!, s => s);
        Assert.True(byKey["TestMetricsEnabled"].GetProperty("value").GetBoolean());
        var expected = JsonSerializer.SerializeToElement(new[] { "test_type", "test_name", "node_name", "node_labels" });
        Assert.True(JsonElement.DeepEquals(expected, byKey["TestMetricsLabels"].GetProperty("value")));
    }

    [Fact]
    public async Task Emit_DefaultSet_IncludesEveryDefaultLabel()
    {
        var node = await CreateNodeAsync("labeled-node", "home", "edge");

        await _emitter.EmitAsync(job: null, test: null, node, TestJobStatus.Completed, 123);

        var series = await RunSeriesForAsync("labeled-node");
        Assert.Contains($"test_id=\"unknown\"", series);
        Assert.Contains("node_name=\"labeled-node\"", series);
        Assert.Contains("node_labels=\"edge,home\"", series);
        Assert.Contains("test_id=\"unknown\"", series);
        Assert.Contains("status=\"Completed\"", series);
        Assert.Contains("test_type=\"unknown\"", series);
    }

    [Fact]
    public async Task Emit_SelectedLabels_RideAlong_UnselectedDoNot()
    {
        await _client.PutAsJsonAsync("/v1/settings/TestMetricsLabels", new { value = new[] { "node_id", "node_labels" } });
        try
        {
            var node = await CreateNodeAsync("selection-node", "edge");

            await _emitter.EmitAsync(job: null, test: null, node, TestJobStatus.Completed, 123);

            // This selection deliberately excludes node_name, so the series is found by node_id
            var series = await ScrapeUntilAsync(
                b => b.Contains($"node_id=\"{node.Id}\"") && b.Contains("obicon_tests_runs_total"));
            var line = series.Split("\n").First(l => l.Contains($"node_id=\"{node.Id}\"") && l.StartsWith("obicon_tests_runs_total", StringComparison.Ordinal));
            Assert.Contains($"node_id=\"{node.Id}\"", line);
            Assert.Contains("node_labels=\"edge\"", line);
            Assert.DoesNotContain("node_name=", line);
            Assert.DoesNotContain("test_type=", line);
            Assert.DoesNotContain("test_name=", line);

            // The forced floor survives every selection
            Assert.Contains("test_id=\"unknown\"", line);
            Assert.Contains("status=\"Completed\"", line);
        }
        finally
        {
            await _client.PutAsJsonAsync("/v1/settings/TestMetricsLabels", new { value = new[] { "test_type", "test_name", "node_name", "node_labels" } });
        }
    }

    [Fact]
    public async Task Emit_EmptySelection_StillCarriesTheForcedFloor()
    {
        await _client.PutAsJsonAsync("/v1/settings/TestMetricsLabels", new { value = Array.Empty<string>() });
        try
        {
            var node = await CreateNodeAsync("floor-node");

            await _emitter.EmitAsync(job: null, test: null, node, TestJobStatus.Failed, 5);

            var scrape = await ScrapeUntilAsync(b => b.Contains("obicon_tests_runs_total"));
            var series = scrape.Split("\n").First(l => l.Contains("status=\"Failed\"") && l.StartsWith("obicon_tests_runs_total", StringComparison.Ordinal));
            Assert.Contains("test_id=\"unknown\"", series);
            Assert.DoesNotContain("node_id=", series);
            Assert.DoesNotContain("node_name=", series);
        }
        finally
        {
            await _client.PutAsJsonAsync("/v1/settings/TestMetricsLabels", new { value = new[] { "test_type", "test_name", "node_name", "node_labels" } });
        }
    }

    [Fact]
    public async Task Emit_InvalidLabelJson_FallsBackToDefaults()
    {
        await _client.PutAsJsonAsync("/v1/settings/TestMetricsLabels", new { value = "not json at all" });
        try
        {
            var node = await CreateNodeAsync("fallback-node", "edge");

            await _emitter.EmitAsync(job: null, test: null, node, TestJobStatus.Completed, 123);

            var series = await RunSeriesForAsync("fallback-node");
            Assert.Contains("node_labels=\"edge\"", series);
            Assert.Contains("node_name=\"fallback-node\"", series);
        }
        finally
        {
            await _client.PutAsJsonAsync("/v1/settings/TestMetricsLabels", new { value = new[] { "test_type", "test_name", "node_name", "node_labels" } });
        }
    }

    [Fact]
    public async Task QueueGauge_ExportsEveryStatus_EvenWithoutJobs()
    {
        // The gauge zero-fills every status, so the family is exported from the very
        // first boot - before any job ever existed - instead of being absent
        var scrape = await ScrapeUntilAsync(b =>
            Enum.GetValues<TestJobStatus>().Select(s => s.ToString())
                .All(status => b.Contains($"obicon_tests_queue_jobs{{otel_scope_name=\"Obicon.Tests\",status=\"{status}\"}}")));

        foreach (var status in Enum.GetValues<TestJobStatus>().Select(s => s.ToString()))
        {
            Assert.Contains($"obicon_tests_queue_jobs{{otel_scope_name=\"Obicon.Tests\",status=\"{status}\"}}", scrape);
        }
    }

    [Fact]
    public async Task Emit_IsNotExported_WhenMetricsDisabled()
    {
        var before = await CreateNodeAsync("before-disable-node");
        await _emitter.EmitAsync(job: null, test: null, before, TestJobStatus.Completed, 123);
        await ScrapeUntilAsync(b => b.Contains("node_name=\"before-disable-node\"") && b.Contains("obicon_tests_runs_total"));

        await _client.PutAsJsonAsync("/v1/settings/TestMetricsEnabled", new { value = "false" });
        try
        {
            var after = await CreateNodeAsync("after-disable-node");
            await _emitter.EmitAsync(job: null, test: null, after, TestJobStatus.Completed, 123);

            // The disabled run is never recorded; give the exporter a moment, then the
            // series must be absent and the previously exported one frozen at 1
            await Task.Delay(TimeSpan.FromSeconds(2));
            var scrape = await _client.GetStringAsync("/metrics");
            Assert.DoesNotContain("node_name=\"after-disable-node\"", scrape);

            var beforeSeries = scrape.Split("\n").First(l => l.Contains("node_name=\"before-disable-node\"") && l.StartsWith("obicon_tests_runs_total", StringComparison.Ordinal));
            Assert.EndsWith(" 1", beforeSeries.TrimEnd());
        }
        finally
        {
            await _client.PutAsJsonAsync("/v1/settings/TestMetricsEnabled", new { value = "true" });
        }
    }
}
