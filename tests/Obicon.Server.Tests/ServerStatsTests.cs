using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace Obicon.Server.Tests;

/// <summary>
/// Tests the server stats endpoint: it must report the server's own version, so the
/// UI can check each node's reported version against the supported range.
/// </summary>
public class ServerStatsTests : LoggedTest, IClassFixture<ObiconServerFactory>
{
    private readonly HttpClient _client;

    public ServerStatsTests(ITestOutputHelper output, ObiconServerFactory factory)
        : base(output)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new(ObiconServerFactory.AuthHeader);
    }

    [Fact]
    public async Task Stats_ReportsServerVersion()
    {
        var stats = await _client.GetFromJsonAsync<JsonElement>("/v1/server/stats");

        var version = stats.GetProperty("version").GetString();
        Assert.False(string.IsNullOrWhiteSpace(version));
        Assert.Matches(@"^\d+\.\d+\.\d+", version!);
    }
}
