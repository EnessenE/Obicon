using System.Net;
using Xunit;
using Xunit.Abstractions;

namespace Obicon.Server.Tests;

/// <summary>
/// Tests for the health endpoint's auth exemption: orchestrator probes
/// (Kubernetes, Docker, load balancers) cannot send the API key, so
/// /v1/health must answer without the Authorization header while every
/// other API route keeps rejecting unauthenticated requests.
/// </summary>
public class HealthEndpointTests : LoggedTest, IClassFixture<ObiconServerFactory>
{
    private readonly ObiconServerFactory _factory;
    private readonly HttpClient _client;

    public HealthEndpointTests(ITestOutputHelper output, ObiconServerFactory factory)
        : base(output)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    protected override void DisposeManagedResources()
    {
        _client.Dispose();
    }

    [Fact]
    public async Task Health_WithoutAuthHeader_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/v1/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", (await response.Content.ReadAsStringAsync()).Trim());
    }

    [Fact]
    public async Task OtherEndpoints_WithoutAuthHeader_StayUnauthorized()
    {
        var nodes = await _client.GetAsync("/v1/nodes");
        var settings = await _client.GetAsync("/v1/settings");

        Assert.Equal(HttpStatusCode.Unauthorized, nodes.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, settings.StatusCode);
    }

    [Fact]
    public async Task Health_WithAuthHeader_ReturnsHealthy()
    {
        using var authorized = _factory.CreateClient();
        authorized.DefaultRequestHeaders.Authorization = new(ObiconServerFactory.AuthHeader);

        var response = await authorized.GetAsync("/v1/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
