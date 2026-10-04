using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Obicon.Integration.Tests;

/// <summary>
/// Full-stack integration tests: a Dockerized server and node, talking over real
/// WebSocket and HTTP. The stack boots once for the whole collection.
/// </summary>
[Collection(ObiconStackCollectionDefinition.Name)]
public sealed class FullStackTests
{
    private readonly ObiconStackFixture _fixture;

    public FullStackTests(ObiconStackFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task EnrolledNode_Connects_AndReportsItself()
    {
        var nodes = await _fixture.Api.GetFromJsonAsync<JsonElement>("/v1/nodes");
        var node = nodes.EnumerateArray().Single(n => n.GetProperty("id").GetGuid() == _fixture.NodeId);

        Assert.Equal("integration-node", node.GetProperty("name").GetString());
        Assert.Equal("auto-enrollment", node.GetProperty("enrollmentType").GetString());
        Assert.False(string.IsNullOrEmpty(node.GetProperty("version").GetString()));

        var statuses = await _fixture.Api.GetFromJsonAsync<JsonElement>("/v1/nodes/status");
        var status = statuses.EnumerateArray().Single(s => s.GetProperty("id").GetGuid() == _fixture.NodeId);
        Assert.True(status.GetProperty("isConnected").GetBoolean());
    }

    [Fact]
    public async Task HttpTest_AgainstTheServer_HealthEndpoint_Completes()
    {
        // The node reaches the server by its docker DNS name over the shared network;
        // /metrics is auth-exempt and answers 200, so it is a stable HTTP target
        var test = await CreateTestAsync(new
        {
            Name = "server-metrics",
            Type = 2,
            Target = $"http://{_fixture.ServerContainerName}:5000/metrics",
            Frequency = 3600,
            NodeIds = new[] { _fixture.NodeId }
        });

        var job = await RunAndAwaitJobAsync(test);

        Assert.True(job.GetProperty("success").GetBoolean(), job.GetProperty("output").GetString());
        var http = job.GetProperty("details").GetProperty("http");
        Assert.Equal(200, http.GetProperty("statusCode").GetInt32());
        Assert.True(http.GetProperty("ttfbMs").GetDouble() > 0);
    }

    [Fact]
    public async Task RunOnce_PingLoopback_InTheContainer_Completes()
    {
        // Docker's default capability set grants NET_RAW, so the containerized node
        // can send raw ICMP: loopback ping proves the runner works unprivileged-in-docker
        var job = await RunOnceAndAwaitJobAsync(new
        {
            Type = 0,
            Target = "127.0.0.1",
            NodeIds = new[] { _fixture.NodeId }
        });

        Assert.True(job.GetProperty("success").GetBoolean(), job.GetProperty("output").GetString());
        var ping = job.GetProperty("details").GetProperty("ping");
        Assert.True(ping.GetProperty("received").GetInt32() > 0);
        Assert.True(ping.GetProperty("avgRoundtripMs").GetDouble() >= 0);
    }

    [Fact]
    public async Task DnsTest_ResolvesTheServerByNetworkName()
    {
        // The docker embedded DNS resolves the server container's name, so this
        // exercises the node's DNS runner against a real resolver
        var job = await RunOnceAndAwaitJobAsync(new
        {
            Type = 5,
            Target = _fixture.ServerContainerName,
            NodeIds = new[] { _fixture.NodeId }
        });

        Assert.True(job.GetProperty("success").GetBoolean(), job.GetProperty("output").GetString());
        var dns = job.GetProperty("details").GetProperty("dns");
        Assert.True(dns.GetProperty("resolved").GetArrayLength() > 0);
    }

    [Fact]
    public async Task Metrics_ExposeBuildInfoAndTestRuns()
    {
        // One quick run so the test-run metrics are guaranteed to exist, then scrape
        await RunOnceAndAwaitJobAsync(new
        {
            Type = 5,
            Target = "localhost",
            NodeIds = new[] { _fixture.NodeId }
        });

        using var noAuth = new HttpClient { BaseAddress = new Uri($"http://localhost:{_fixture.ServerHostPort}") };
        var metrics = await noAuth.GetStringAsync("/metrics");

        Assert.Contains("obicon_server_build_info", metrics);
        Assert.Contains("obicon_tests_runs", metrics);
    }

    /// <summary>
    /// Creates a test through the API and returns its JSON representation.
    /// </summary>
    private async Task<JsonElement> CreateTestAsync(object request)
    {
        var response = await _fixture.Api.PostAsJsonAsync("/v1/tests", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>
    /// Triggers a saved test and polls its job until it reaches a final status.
    /// Default timeout: 60s.
    /// </summary>
    private async Task<JsonElement> RunAndAwaitJobAsync(JsonElement test)
    {
        var testId = test.GetProperty("id").GetGuid();
        var response = await _fixture.Api.PostAsync($"/v1/tests/{testId}/run", null);
        response.EnsureSuccessStatusCode();
        return await AwaitJobAsync(testId);
    }

    /// <summary>
    /// Runs a test once through the run-once endpoint and polls the job until it
    /// reaches a final status. Default timeout: 60s.
    /// </summary>
    private async Task<JsonElement> RunOnceAndAwaitJobAsync(object request)
    {
        var response = await _fixture.Api.PostAsJsonAsync("/v1/tests/run-once", request);
        response.EnsureSuccessStatusCode();
        var jobs = await response.Content.ReadFromJsonAsync<JsonElement>();
        var jobId = jobs.EnumerateArray().Single().GetProperty("id").GetGuid();

        return await AwaitJobAsync(jobId, exactId: true);
    }

    /// <summary>
    /// Polls the queue until the job is Completed, Failed, or Timed Out. When the
    /// jobs list is searched by test ID, the newest of that test's jobs is returned.
    /// </summary>
    private async Task<JsonElement> AwaitJobAsync(Guid id, bool exactId = false)
    {
        JsonElement job = default;
        await ObiconStackFixture.RetryUntilAsync(async () =>
        {
            var jobs = (await _fixture.Api.GetFromJsonAsync<JsonElement>("/v1/testruns?limit=500")).GetProperty("items");
            var match = jobs.EnumerateArray()
                .FirstOrDefault(j => exactId
                    ? j.GetProperty("id").GetGuid() == id
                    : j.GetProperty("testId").GetGuid() == id);
            if (match.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            job = match;
            return job.GetProperty("status").GetInt32() >= 3;
        }, TimeSpan.FromSeconds(60), $"the job {id} to finish");

        return job;
    }
}
