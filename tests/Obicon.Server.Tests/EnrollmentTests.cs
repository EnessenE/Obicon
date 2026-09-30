using Xunit;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Obicon.Server.Tests;

/// <summary>
/// Integration tests for node self-enrollment, covering the settings gate and token validation.
/// </summary>
public class EnrollmentTests : IClassFixture<ObiconServerFactory>
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _disabledClient;
    private readonly ObiconServerFactory _disabledFactory;
    private ObiconServerFactory? _enabledFactory;

    public EnrollmentTests(ObiconServerFactory disabledFactory)
    {
        _disabledFactory = disabledFactory;
        _disabledClient = CreateClient(_disabledFactory);
    }

    private HttpClient CreateClient(ObiconServerFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("uwu");
        return client;
    }

    private async Task<HttpClient> GetEnabledClientAsync()
    {
        if (_enabledFactory == null)
        {
            _enabledFactory = new EnrollmentEnabledServerFactory();
        }
        return CreateClient(_enabledFactory);
    }

    private async Task<string> CreateEnrollTokenAsync(HttpClient client, object? body = null)
    {
        var response = await client.PostAsJsonAsync("/v1/enroll-tokens", body ?? new { });
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("token").GetString()!;
    }

    [Fact]
    public async Task Enroll_IsRejected_WhenAutoEnrollmentIsDisabled()
    {
        var client = await GetEnabledClientAsync();
        var token = await CreateEnrollTokenAsync(client);

        // The disabled server has the setting forced to false and rejects any enrollment,
        // even with a token issued by a different instance
        var response = await _disabledClient.PostAsJsonAsync("/v1/enroll", new
        {
            EnrollToken = token,
            NodeName = "sneaky-node"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Enroll_IsRejected_WithInvalidToken_WhenEnabled()
    {
        var client = await GetEnabledClientAsync();

        var response = await client.PostAsJsonAsync("/v1/enroll", new
        {
            EnrollToken = "not-a-real-token",
            NodeName = "node-with-bad-token"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Enroll_Succeeds_CreatesAutoEnrolledNode_AndBlocksUserEdits()
    {
        var client = await GetEnabledClientAsync();
        var token = await CreateEnrollTokenAsync(client, new { Name = "integration" });

        var response = await client.PostAsJsonAsync("/v1/enroll", new
        {
            EnrollToken = token,
            NodeName = "test-pi",
            Labels = new[] { "lab", "fast" },
            Pools = new[] { "lab-nodes" }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var enrollResult = await response.Content.ReadFromJsonAsync<JsonElement>();
        var nodeId = enrollResult.GetProperty("id").GetGuid();
        Assert.False(string.IsNullOrEmpty(enrollResult.GetProperty("authToken").GetString()));

        // The node exists, is auto-enrolled, and was put into its pool
        var nodes = await client.GetFromJsonAsync<JsonElement>("/v1/nodes");
        var created = nodes.EnumerateArray().Single(n => n.GetProperty("id").GetGuid() == nodeId);
        Assert.Equal("auto-enrollment", created.GetProperty("enrollmentType").GetString());

        var pools = await client.GetFromJsonAsync<JsonElement>("/v1/pools");
        Assert.Contains(pools.EnumerateArray(), p => p.GetProperty("name").GetString() == "lab-nodes");

        // Users cannot edit an enrolled node
        var put = await client.PutAsJsonAsync($"/v1/nodes/{nodeId}", new
        {
            Name = "hacked",
            Labels = new[] { "user-label" },
            RegenerateToken = false
        });
        Assert.Equal(HttpStatusCode.Conflict, put.StatusCode);
    }

    [Fact]
    public async Task Enroll_IsRejected_AfterTokenRevoked()
    {
        var client = await GetEnabledClientAsync();
        var token = await CreateEnrollTokenAsync(client);
        var tokens = await client.GetFromJsonAsync<JsonElement>("/v1/enroll-tokens");
        var tokenId = tokens.EnumerateArray().First().GetProperty("id").GetGuid();

        var revoke = await client.PostAsync($"/v1/enroll-tokens/{tokenId}/revoke", null);
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        var response = await client.PostAsJsonAsync("/v1/enroll", new
        {
            EnrollToken = token,
            NodeName = "too-late"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Enroll_IsRejected_WithExpiredToken()
    {
        var client = await GetEnabledClientAsync();
        var token = await CreateEnrollTokenAsync(client, new { ExpiresAt = DateTime.UtcNow.AddHours(-1) });

        var response = await client.PostAsJsonAsync("/v1/enroll", new
        {
            EnrollToken = token,
            NodeName = "too-late"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Enroll_TokenIsGenerated_WhenNoNameGiven()
    {
        var client = await GetEnabledClientAsync();

        var response = await client.PostAsJsonAsync("/v1/enroll-tokens", new { });
        var token = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Matches("^enroll-token-\\d{2}-\\d{2}-\\d{4}-\\d{2}-\\d{2}-\\d{2}$", token.GetProperty("name").GetString()!);
    }
}
