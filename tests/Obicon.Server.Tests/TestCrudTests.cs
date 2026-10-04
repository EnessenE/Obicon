using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace Obicon.Server.Tests;

/// <summary>
/// Integration tests for test CRUD: creation, editing all settings, and validation.
/// </summary>
public class TestCrudTests : LoggedTest, IClassFixture<ObiconServerFactory>
{
    private readonly HttpClient _client;

    public TestCrudTests(ITestOutputHelper output, ObiconServerFactory factory)
        : base(output)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new(ObiconServerFactory.AuthHeader);
    }

    private async Task<Guid> CreateNodeAsync()
    {
        var response = await _client.PostAsJsonAsync("/v1/nodes", new { Name = "crud-node" });
        response.EnsureSuccessStatusCode();
        var node = await response.Content.ReadFromJsonAsync<JsonElement>();
        return node.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Test_WithBothIpVersions_SchedulesOneJobPerFamily()
    {
        var nodeId = await CreateNodeAsync();
        var created = await _client.PostAsJsonAsync("/v1/tests", new
        {
            Name = "dual-stack",
            Type = 4,
            Target = "example.com",
            Frequency = 60,
            IpVersion = 3, // Both
            NodeIds = new[] { nodeId }
        });
        created.EnsureSuccessStatusCode();
        var test = await created.Content.ReadFromJsonAsync<JsonElement>();
        var testId = test.GetProperty("id").GetGuid();

        var run = await _client.PostAsync($"/v1/tests/{testId}/run", null);
        run.EnsureSuccessStatusCode();

        var jobs = (await _client.GetFromJsonAsync<JsonElement>("/v1/testruns?limit=500")).GetProperty("items");
        var forTest = jobs.EnumerateArray()
            .Where(j => j.GetProperty("testId").GetGuid() == testId)
            .ToList();

        // One IPv4 job and one IPv6 job for the single targeted node
        Assert.Equal(2, forTest.Count);
        Assert.Equal(1, forTest.Count(j => j.GetProperty("ipVersion").GetInt32() == 1));
        Assert.Equal(1, forTest.Count(j => j.GetProperty("ipVersion").GetInt32() == 2));
    }

    [Fact]
    public async Task Test_CanBeEdited_WithAllSettings()
    {
        var nodeId = await CreateNodeAsync();

        var create = await _client.PostAsJsonAsync("/v1/tests", new
        {
            Name = "original",
            Type = 5,
            Target = "localhost",
            NodeIds = new[] { nodeId },
            Frequency = 60,
            IsActive = true,
            IpVersion = 0,
            TimeoutSeconds = 30
        });
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var testId = created.GetProperty("id").GetGuid();
        Assert.Equal(30, created.GetProperty("timeoutSeconds").GetInt32());

        var update = await _client.PutAsJsonAsync($"/v1/tests/{testId}", new
        {
            Name = "edited",
            Type = 0,
            Target = "example.com",
            NodeIds = new[] { nodeId },
            PoolIds = Array.Empty<Guid>(),
            Frequency = 120,
            IsActive = false,
            ExpectedStatusCodes = "200,301",
            CheckCertificateExpiryDays = 30,
            ExpectedDnsResult = "1.2.3.4",
            IpVersion = 1,
            TimeoutSeconds = 15
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var updated = await update.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("edited", updated.GetProperty("name").GetString());
        Assert.Equal("example.com", updated.GetProperty("target").GetString());
        Assert.Equal(120, updated.GetProperty("frequency").GetInt32());
        Assert.False(updated.GetProperty("isActive").GetBoolean());
        Assert.Equal("200,301", updated.GetProperty("expectedStatusCodes").GetString());
        Assert.Equal(30, updated.GetProperty("checkCertificateExpiryDays").GetInt32());
        Assert.Equal("1.2.3.4", updated.GetProperty("expectedDnsResult").GetString());
        Assert.Equal(1, updated.GetProperty("ipVersion").GetInt32());
        Assert.Equal(15, updated.GetProperty("timeoutSeconds").GetInt32());
    }

    [Fact]
    public async Task Test_RejectsInvalidTimeout()
    {
        var nodeId = await CreateNodeAsync();

        var response = await _client.PostAsJsonAsync("/v1/tests", new
        {
            Name = "bad-timeout",
            Type = 5,
            Target = "localhost",
            NodeIds = new[] { nodeId },
            Frequency = 60,
            TimeoutSeconds = 0
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Test_RejectsFrequencyOutsidePresets()
    {
        var nodeId = await CreateNodeAsync();

        var response = await _client.PostAsJsonAsync("/v1/tests", new
        {
            Name = "bad-frequency",
            Type = 5,
            Target = "localhost",
            NodeIds = new[] { nodeId },
            Frequency = 45,
            TimeoutSeconds = 30
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Test_AcceptsFrequencyAfterPresetChange()
    {
        var original = await _client.PutAsJsonAsync("/v1/settings/FrequencyPresetsSeconds", new { Value = new[] { 15, 45 } });
        original.EnsureSuccessStatusCode();
        try
        {
            var nodeId = await CreateNodeAsync();

            var response = await _client.PostAsJsonAsync("/v1/tests", new
            {
                Name = "custom-frequency",
                Type = 5,
                Target = "localhost",
                NodeIds = new[] { nodeId },
                Frequency = 45,
                TimeoutSeconds = 30
            });

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var created = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(45, created.GetProperty("frequency").GetInt32());
        }
        finally
        {
            await _client.PutAsJsonAsync("/v1/settings/FrequencyPresetsSeconds", new { Value = new[] { 10, 30, 60, 120, 300, 600, 3600 } });
        }
    }

    [Fact]
    public async Task Test_RejectsMissingTargets()
    {
        var response = await _client.PostAsJsonAsync("/v1/tests", new
        {
            Name = "no-targets",
            Type = 5,
            Target = "localhost",
            NodeIds = Array.Empty<Guid>(),
            PoolIds = Array.Empty<Guid>(),
            Frequency = 60
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
