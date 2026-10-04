using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace Obicon.Server.Tests;

/// <summary>
/// Tests list settings forced as native arrays in configuration: the values bind
/// to the setting's type, are reported as forced, and stay read-only.
/// </summary>
public class ForcedListSettingsTests : LoggedTest, IClassFixture<ForcedListSettingsServerFactory>
{
    private readonly HttpClient _client;

    public ForcedListSettingsTests(ITestOutputHelper output, ForcedListSettingsServerFactory factory)
        : base(output)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new(ObiconServerFactory.AuthHeader);
    }

    [Fact]
    public async Task NativeArrays_BindToTheSettingType_AndAreReportedAsForced()
    {
        var settings = await _client.GetFromJsonAsync<JsonElement>("/v1/settings");
        var byKey = settings.EnumerateArray().ToDictionary(s => s.GetProperty("key").GetString()!, s => s);

        var types = byKey["EnabledTestTypes"];
        Assert.Equal("Configuration (forced)", types.GetProperty("source").GetString());
        var expectedTypes = JsonSerializer.SerializeToElement(new[] { "Ping", "Dns" });
        Assert.True(JsonElement.DeepEquals(expectedTypes, types.GetProperty("value")));

        var presets = byKey["FrequencyPresetsSeconds"];
        Assert.Equal("Configuration (forced)", presets.GetProperty("source").GetString());
        var expectedPresets = JsonSerializer.SerializeToElement(new[] { 15, 45 });
        Assert.True(JsonElement.DeepEquals(expectedPresets, presets.GetProperty("value")));

        // The forced presets drive the derived scheduler interval
        Assert.Equal(15, byKey["SchedulerLoopIntervalSeconds"].GetProperty("value").GetInt32());
    }

    [Fact]
    public async Task ForcedListSettings_AreReadOnly()
    {
        var response = await _client.PutAsJsonAsync("/v1/settings/EnabledTestTypes", new { value = new[] { "Http" } });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
