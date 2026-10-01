using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Obicon.Server.Configuration;
using Xunit;

namespace Obicon.Server.Tests;

/// <summary>
/// Integration tests for the settings API: forced settings are read-only,
/// unforced settings can be changed and take effect. Every setting defined in
/// ServerSettingDefinitions is covered: exposure, default, and where possible its behavior.
/// </summary>
public class SettingsTests : IClassFixture<ObiconServerFactory>
{
    private readonly HttpClient _client;

    public SettingsTests(ObiconServerFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new("uwu");
    }

    private async Task<Dictionary<string, JsonElement>> GetSettingsMapAsync()
    {
        var settings = await _client.GetFromJsonAsync<JsonElement>("/v1/settings");
        return settings.EnumerateArray().ToDictionary(s => s.GetProperty("key").GetString()!, s => s);
    }

    [Fact]
    public async Task EveryDefinedSetting_IsExposed_WithDescription()
    {
        var settings = await _client.GetFromJsonAsync<JsonElement>("/v1/settings");
        var keys = settings.EnumerateArray().Select(s => s.GetProperty("key").GetString()).ToList();

        // The API must expose every setting the server knows, and nothing else
        Assert.Equal(
            ServerSettingDefinitions.All.Select(d => d.Key).OrderBy(k => k),
            keys.OrderBy(k => k));

        Assert.All(settings.EnumerateArray(), s =>
            Assert.False(string.IsNullOrEmpty(s.GetProperty("description").GetString())));
    }

    [Fact]
    public async Task EveryNonForcedNonDerivedSetting_ReturnsItsDefault()
    {
        var settings = await GetSettingsMapAsync();

        foreach (var definition in ServerSettingDefinitions.All)
        {
            if (definition.Key == "SchedulerLoopIntervalSeconds")
            {
                continue; // derived, not the raw default
            }

            var setting = settings[definition.Key];
            var source = setting.GetProperty("source").GetString();
            if (source == "Default")
            {
                Assert.Equal(definition.Default, setting.GetProperty("value").GetString());
            }
        }
    }

    [Fact]
    public async Task Set_ReturnsConflict_ForForcedSetting()
    {
        // MaxTestTimeoutSeconds is present in appsettings.json, so it is forced
        var response = await _client.PutAsJsonAsync("/v1/settings/MaxTestTimeoutSeconds", new { Value = "5" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task EveryConfiguredSetting_IsReportedAsForced()
    {
        // These come from appsettings.json in the server project, so all of them are forced
        var settings = await GetSettingsMapAsync();

        Assert.Equal("Configuration (forced)", settings["AuthHeader"].GetProperty("source").GetString());
        Assert.Equal("Configuration (forced)", settings["MaxTestTimeoutSeconds"].GetProperty("source").GetString());
        Assert.Equal("Configuration (forced)", settings["NodeConnectionTimeoutSeconds"].GetProperty("source").GetString());
        Assert.Equal("Configuration (forced)", settings["WebSocketPath"].GetProperty("source").GetString());
    }

    [Fact]
    public async Task Set_ReturnsConflict_ForWebSocketPathAndConnectionTimeout()
    {
        var webSocketPath = await _client.PutAsJsonAsync("/v1/settings/WebSocketPath", new { Value = "/ws/other" });
        var connectionTimeout = await _client.PutAsJsonAsync("/v1/settings/NodeConnectionTimeoutSeconds", new { Value = "99" });

        Assert.Equal(HttpStatusCode.Conflict, webSocketPath.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, connectionTimeout.StatusCode);
    }

    [Fact]
    public async Task AuthHeader_WrongValueIsRejected()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/nodes");
        request.Headers.Authorization = new("definitely-not-uwu");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MaxTestTimeoutSeconds_ClampsEnqueuedJobTimeouts()
    {
        // The setting is forced to 60 in the test server's appsettings: a test created
        // with a larger timeout must have its queued jobs clamped to it
        var node = await _client.PostAsJsonAsync("/v1/nodes", new { Name = "clamp-node" });
        var nodeId = (await node.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var create = await _client.PostAsJsonAsync("/v1/tests", new
        {
            Name = "clamp-test",
            Type = 4,
            Target = "localhost:80",
            NodeIds = new[] { nodeId },
            Frequency = 60,
            TimeoutSeconds = 3600
        });
        create.EnsureSuccessStatusCode();
        var testId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var run = await _client.PostAsync($"/v1/tests/{testId}/run", null);
        run.EnsureSuccessStatusCode();

        var queue = await _client.GetFromJsonAsync<JsonElement>("/v1/queue");
        var job = queue.EnumerateArray().First(j => j.GetProperty("testId").GetGuid() == testId);
        Assert.Equal(60, job.GetProperty("timeoutSeconds").GetInt32());
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

        // Restore the default so other tests see a clean state
        await _client.PutAsJsonAsync("/v1/settings/NoRunGraceFactor", new { Value = "2" });
    }

    [Fact]
    public async Task Set_ReturnsBadRequest_ForInvalidValue()
    {
        var response = await _client.PutAsJsonAsync("/v1/settings/NoRunGraceFactor", new { Value = "not-a-number" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AllowUnsupportedNodeVersions_CanBeToggled()
    {
        var settings = await GetSettingsMapAsync();
        Assert.False(settings["AllowUnsupportedNodeVersions"].GetProperty("isForced").GetBoolean());
        Assert.Equal("false", settings["AllowUnsupportedNodeVersions"].GetProperty("value").GetString());

        var enable = await _client.PutAsJsonAsync("/v1/settings/AllowUnsupportedNodeVersions", new { Value = "true" });
        Assert.Equal(HttpStatusCode.OK, enable.StatusCode);
        var enabled = await enable.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("true", enabled.GetProperty("value").GetString());

        var invalid = await _client.PutAsJsonAsync("/v1/settings/AllowUnsupportedNodeVersions", new { Value = "maybe" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        // Restore the default
        var disable = await _client.PutAsJsonAsync("/v1/settings/AllowUnsupportedNodeVersions", new { Value = "false" });
        Assert.Equal(HttpStatusCode.OK, disable.StatusCode);
    }

    [Fact]
    public async Task ObservabilitySettings_BelongToTheirGroup_WithCorrectDefaults()
    {
        var settings = await GetSettingsMapAsync();

        // Every setting belongs to a named section
        Assert.All(settings.Values, s =>
            Assert.False(string.IsNullOrEmpty(s.GetProperty("group").GetString())));

        Assert.Equal("Observability", settings["NodeLogShippingEnabled"].GetProperty("group").GetString());
        Assert.Equal("Observability", settings["NodeLocalLoggingEnabled"].GetProperty("group").GetString());
        Assert.Equal("Observability", settings["ShipNodeLogsToConsole"].GetProperty("group").GetString());

        // Shipping is off and console forwarding is off by default; local node logging is on
        Assert.Equal("false", settings["NodeLogShippingEnabled"].GetProperty("value").GetString());
        Assert.Equal("true", settings["NodeLocalLoggingEnabled"].GetProperty("value").GetString());
        Assert.Equal("false", settings["ShipNodeLogsToConsole"].GetProperty("value").GetString());
    }

    [Fact]
    public async Task ObservabilitySettings_CanBeToggled()
    {
        foreach (var key in new[] { "NodeLogShippingEnabled", "NodeLocalLoggingEnabled", "ShipNodeLogsToConsole" })
        {
            var toggle = await _client.PutAsJsonAsync($"/v1/settings/{key}", new { Value = "true" });
            Assert.Equal(HttpStatusCode.OK, toggle.StatusCode);
            var changed = await toggle.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Observability", changed.GetProperty("group").GetString());

            // Restore the defaults
            var restore = await _client.PutAsJsonAsync($"/v1/settings/{key}",
                new { Value = key == "NodeLocalLoggingEnabled" ? "true" : "false" });
            Assert.Equal(HttpStatusCode.OK, restore.StatusCode);
        }
    }

    [Fact]
    public async Task NodeAutoEnrollmentEnabled_SettingControlsEnrollment()
    {
        // The setting is unforced in this factory, so a database override must take effect
        var enable = await _client.PutAsJsonAsync("/v1/settings/NodeAutoEnrollmentEnabled", new { Value = "true" });
        Assert.Equal(HttpStatusCode.OK, enable.StatusCode);

        try
        {
            var tokenResponse = await _client.PostAsJsonAsync("/v1/enroll-tokens", new { Name = "settings-test" });
            tokenResponse.EnsureSuccessStatusCode();
            var token = (await tokenResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;

            var enroll = await _client.PostAsJsonAsync("/v1/enroll", new { EnrollToken = token, NodeName = "settings-pi" });
            Assert.Equal(HttpStatusCode.OK, enroll.StatusCode);
        }
        finally
        {
            await _client.PutAsJsonAsync("/v1/settings/NodeAutoEnrollmentEnabled", new { Value = "false" });
        }

        // With the override restored to false, enrollment is rejected again
        var token2 = (await (await _client.PostAsJsonAsync("/v1/enroll-tokens", new { Name = "settings-test-2" }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        var rejected = await _client.PostAsJsonAsync("/v1/enroll", new { EnrollToken = token2, NodeName = "late-pi" });
        Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);
    }

    [Fact]
    public async Task FrequencyPresetsSeconds_GateTestCreation()
    {
        var change = await _client.PutAsJsonAsync("/v1/settings/FrequencyPresetsSeconds", new { Value = "15,45" });
        change.EnsureSuccessStatusCode();

        try
        {
            var node = await _client.PostAsJsonAsync("/v1/nodes", new { Name = "preset-node" });
            var nodeId = (await node.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

            var allowed = await _client.PostAsJsonAsync("/v1/tests", new
            {
                Name = "preset-test",
                Type = 4,
                Target = "localhost:80",
                NodeIds = new[] { nodeId },
                Frequency = 45,
                TimeoutSeconds = 30
            });
            Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);

            var disallowed = await _client.PostAsJsonAsync("/v1/tests", new
            {
                Name = "preset-test-2",
                Type = 4,
                Target = "localhost:80",
                NodeIds = new[] { nodeId },
                Frequency = 60,
                TimeoutSeconds = 30
            });
            Assert.Equal(HttpStatusCode.BadRequest, disallowed.StatusCode);
        }
        finally
        {
            await _client.PutAsJsonAsync("/v1/settings/FrequencyPresetsSeconds", new { Value = "10,30,60,120,300,600,3600" });
        }
    }

    [Fact]
    public async Task SchedulerInterval_IsDerivedFromLowestPreset()
    {
        var settings = await _client.GetFromJsonAsync<JsonElement>("/v1/settings");
        var interval = settings.EnumerateArray().Single(s => s.GetProperty("key").GetString() == "SchedulerLoopIntervalSeconds");

        Assert.True(interval.GetProperty("isReadOnly").GetBoolean());
        Assert.Equal("Derived", interval.GetProperty("source").GetString());
        Assert.Equal("10", interval.GetProperty("value").GetString());
    }

    [Fact]
    public async Task Set_ReturnsConflict_ForReadOnlySetting()
    {
        var response = await _client.PutAsJsonAsync("/v1/settings/SchedulerLoopIntervalSeconds", new { Value = "5" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task SchedulerInterval_FollowsPresetOverride()
    {
        var change = await _client.PutAsJsonAsync("/v1/settings/FrequencyPresetsSeconds", new { Value = "15,45,300" });
        change.EnsureSuccessStatusCode();
        try
        {
            var settings = await _client.GetFromJsonAsync<JsonElement>("/v1/settings");
            var interval = settings.EnumerateArray().Single(s => s.GetProperty("key").GetString() == "SchedulerLoopIntervalSeconds");
            Assert.Equal("15", interval.GetProperty("value").GetString());
        }
        finally
        {
            await _client.PutAsJsonAsync("/v1/settings/FrequencyPresetsSeconds", new { Value = "10,30,60,120,300,600,3600" });
        }
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
