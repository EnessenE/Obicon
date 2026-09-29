using Xunit;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Obicon.Server.Tests;

/// <summary>
/// Integration tests for the settings API: forced settings are read-only,
/// unforced settings can be changed and take effect.
/// </summary>
public class SettingsTests : IClassFixture<ObiconServerFactory>
{
    private readonly HttpClient _client;

    public SettingsTests(ObiconServerFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new("uwu");
    }

    [Fact]
    public async Task GetAll_ListsEveryKnownSetting_WithDescription()
    {
        var settings = await _client.GetFromJsonAsync<JsonElement>("/v1/settings");

        var keys = settings.EnumerateArray().Select(s => s.GetProperty("key").GetString()).ToList();
        Assert.Contains("AuthHeader", keys);
        Assert.Contains("MaxTestTimeoutSeconds", keys);
        Assert.Contains("NodeAutoEnrollmentEnabled", keys);
        Assert.Contains("NoRunGraceFactor", keys);
        Assert.True(settings.EnumerateArray().All(s => !string.IsNullOrEmpty(s.GetProperty("description").GetString())));
    }

    [Fact]
    public async Task Set_ReturnsConflict_ForForcedSetting()
    {
        // MaxTestTimeoutSeconds is present in appsettings.json, so it is forced
        var response = await _client.PutAsJsonAsync("/v1/settings/MaxTestTimeoutSeconds", new { Value = "5" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Set_ReturnsBadRequest_ForUnknownSetting()
    {
        var response = await _client.PutAsJsonAsync("/v1/settings/NoSuchSetting", new { Value = "1" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Set_StoresOverride_ForUnforcedSetting()
    {
        // NoRunGraceFactor is not in appsettings.json, so it can be overridden
        var response = await _client.PutAsJsonAsync("/v1/settings/NoRunGraceFactor", new { Value = "3" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var setting = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("3", setting.GetProperty("value").GetString());
        Assert.Equal("Database", setting.GetProperty("source").GetString());
    }

    [Fact]
    public async Task Set_ReturnsBadRequest_ForInvalidValue()
    {
        var response = await _client.PutAsJsonAsync("/v1/settings/NoRunGraceFactor", new { Value = "not-a-number" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Api_RequiresAuthorizationHeader()
    {
        using var factory = new ObiconServerFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/v1/nodes");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
