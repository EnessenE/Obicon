using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Obicon.Server.Tests;

/// <summary>
/// Integration tests for the HTTP test enhancements: body regex, headers, proxy,
/// and cache busting, including their validation.
/// </summary>
public class HttpExpectationTests : IClassFixture<ObiconServerFactory>
{
    private readonly HttpClient _client;

    public HttpExpectationTests(ObiconServerFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new(ObiconServerFactory.AuthHeader);
    }

    private async Task<Guid> CreateNodeAsync()
    {
        var response = await _client.PostAsJsonAsync("/v1/nodes", new { Name = "http-node" });
        response.EnsureSuccessStatusCode();
        var node = await response.Content.ReadFromJsonAsync<JsonElement>();
        return node.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task HttpTest_StoresAllNewFields_AndEchoesThem()
    {
        var nodeId = await CreateNodeAsync();

        var create = await _client.PostAsJsonAsync("/v1/tests", new
        {
            Name = "http-with-extras",
            Type = 2,
            Target = "http://example.com/health",
            NodeIds = new[] { nodeId },
            Frequency = 60,
            IsActive = true,
            TimeoutSeconds = 30,
            ExpectedBodyPattern = "\"status\"\\s*:\\s*\"up\"",
            Headers = new Dictionary<string, string>
            {
                ["X-Api-Key"] = "secret",
                ["Accept"] = "application/json"
            },
            ProxyUrl = "http://proxy.internal:8080",
            CacheBust = true
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("\"status\"\\s*:\\s*\"up\"", created.GetProperty("expectedBodyPattern").GetString());
        Assert.True(created.GetProperty("cacheBust").GetBoolean());
        Assert.Equal("http://proxy.internal:8080", created.GetProperty("proxyUrl").GetString());

        var headers = created.GetProperty("headers");
        Assert.Equal(2, headers.EnumerateObject().Count());
        Assert.Equal("secret", headers.GetProperty("X-Api-Key").GetString());

        // The fields survive a round trip through GET
        var fetched = await _client.GetFromJsonAsync<JsonElement>($"/v1/tests/{created.GetProperty("id").GetGuid()}");
        Assert.True(fetched.GetProperty("cacheBust").GetBoolean());
        Assert.Equal("http://proxy.internal:8080", fetched.GetProperty("proxyUrl").GetString());
    }

    [Fact]
    public async Task HttpTest_RejectsInvalidBodyRegex_With400()
    {
        var nodeId = await CreateNodeAsync();

        var create = await _client.PostAsJsonAsync("/v1/tests", new
        {
            Name = "bad-regex",
            Type = 3,
            Target = "https://example.com",
            NodeIds = new[] { nodeId },
            Frequency = 60,
            TimeoutSeconds = 30,
            ExpectedBodyPattern = "[unclosed"
        });

        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
    }

    [Fact]
    public async Task HttpTest_RejectsInvalidProxy_With400()
    {
        var nodeId = await CreateNodeAsync();

        var create = await _client.PostAsJsonAsync("/v1/tests", new
        {
            Name = "bad-proxy",
            Type = 2,
            Target = "http://example.com",
            NodeIds = new[] { nodeId },
            Frequency = 60,
            TimeoutSeconds = 30,
            ProxyUrl = "definitely not a url"
        });

        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
    }

    [Fact]
    public async Task HttpTest_RejectsMalformedHeaderName_With400()
    {
        var nodeId = await CreateNodeAsync();

        var create = await _client.PostAsJsonAsync("/v1/tests", new
        {
            Name = "bad-header",
            Type = 2,
            Target = "http://example.com",
            NodeIds = new[] { nodeId },
            Frequency = 60,
            TimeoutSeconds = 30,
            Headers = new Dictionary<string, string>
            {
                ["Not A: Header"] = "value"
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
    }

    [Fact]
    public async Task RunOnce_RejectsInvalidBodyRegex_With400()
    {
        var nodeId = await CreateNodeAsync();

        // The regex validation runs before connectivity, so this 400 is about the pattern
        var run = await _client.PostAsJsonAsync("/v1/tests/run-once", new
        {
            Type = 2,
            Target = "http://example.com",
            NodeIds = new[] { nodeId },
            ExpectedBodyPattern = "(unclosed"
        });

        Assert.Equal(HttpStatusCode.BadRequest, run.StatusCode);
        var error = await run.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("regular expression", error.GetProperty("message").GetString());
    }

    [Fact]
    public async Task RunOnce_RejectsUnknownNode_With400()
    {
        var run = await _client.PostAsJsonAsync("/v1/tests/run-once", new
        {
            Type = 2,
            Target = "http://example.com",
            NodeIds = new[] { Guid.NewGuid() }
        });

        Assert.Equal(HttpStatusCode.BadRequest, run.StatusCode);
        var error = await run.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("Unknown node ID", error.GetProperty("message").GetString());
    }

    [Fact]
    public async Task RunOnce_RejectsEmptyNodeList_With400()
    {
        var run = await _client.PostAsJsonAsync("/v1/tests/run-once", new
        {
            Type = 2,
            Target = "http://example.com",
            NodeIds = Array.Empty<Guid>()
        });

        Assert.Equal(HttpStatusCode.BadRequest, run.StatusCode);
    }

    [Fact]
    public async Task RunOnce_RejectsWhenNoNodeIsConnected_With400()
    {
        var first = await CreateNodeAsync();
        var second = await CreateNodeAsync();

        var run = await _client.PostAsJsonAsync("/v1/tests/run-once", new
        {
            Type = 2,
            Target = "http://example.com",
            NodeIds = new[] { first, second }
        });

        Assert.Equal(HttpStatusCode.BadRequest, run.StatusCode);
        var error = await run.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("None of the selected nodes are connected", error.GetProperty("message").GetString());
    }

    [Fact]
    public async Task RunOnce_RejectsEmptyRequest_With400()
    {
        var run = await _client.PostAsJsonAsync("/v1/tests/run-once", new
        {
            Type = 2,
            Target = "http://example.com",
            NodeIds = Array.Empty<Guid>(),
            PoolIds = Array.Empty<Guid>()
        });

        Assert.Equal(HttpStatusCode.BadRequest, run.StatusCode);
        var error = await run.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("At least one node ID or pool ID", error.GetProperty("message").GetString());
    }

    [Fact]
    public async Task RunOnce_RejectsUnknownPool_With400()
    {
        var run = await _client.PostAsJsonAsync("/v1/tests/run-once", new
        {
            Type = 2,
            Target = "http://example.com",
            PoolIds = new[] { Guid.NewGuid() }
        });

        Assert.Equal(HttpStatusCode.BadRequest, run.StatusCode);
        var error = await run.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("Unknown pool ID", error.GetProperty("message").GetString());
    }

    [Fact]
    public async Task RunOnce_ResolvesPoolMembers_ButRejectsWhenNoneIsConnected()
    {
        // A pool with two members; without connections the pool path must resolve
        // the members and then fail with the connected message
        var first = await CreateNodeAsync();
        var second = await CreateNodeAsync();

        var pool = await _client.PostAsJsonAsync("/v1/pools", new { Name = "run-once-pool" });
        pool.EnsureSuccessStatusCode();
        var poolId = (await pool.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var members = await _client.PutAsJsonAsync($"/v1/pools/{poolId}/nodes", new
        {
            NodeIds = new[] { first, second }
        });
        members.EnsureSuccessStatusCode();

        var run = await _client.PostAsJsonAsync("/v1/tests/run-once", new
        {
            Type = 2,
            Target = "http://example.com",
            PoolIds = new[] { poolId }
        });

        Assert.Equal(HttpStatusCode.BadRequest, run.StatusCode);
        var error = await run.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("None of the selected nodes are connected", error.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Pool_DescriptionRoundTrips()
    {
        var create = await _client.PostAsJsonAsync("/v1/pools", new
        {
            Name = "edge-fleet",
            Description = "Nodes sitting at the network edge"
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Nodes sitting at the network edge", created.GetProperty("description").GetString());

        var update = await _client.PutAsJsonAsync($"/v1/pools/{created.GetProperty("id").GetGuid()}", new
        {
            Name = "edge-fleet",
            Description = "Renamed but same purpose"
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Renamed but same purpose", updated.GetProperty("description").GetString());
    }
}
