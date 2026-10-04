using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Obicon.Server.Models;
using Obicon.Server.Services;
using Obicon.Shared.Models.Enums;
using Xunit;
using Xunit.Abstractions;

namespace Obicon.Server.Tests;

/// <summary>
/// Tests the TestResultStorageMode settings and the strict job retention: Full keeps
/// the payload, MetadataOnly strips it, None deletes the row on completion (always
/// returning the job first so metrics are never lost), and the prune sweep deletes
/// only terminal or long-stuck rows, never live ones.
/// </summary>
public class JobStorageTests : LoggedTest, IClassFixture<ObiconServerFactory>
{
    private readonly ObiconServerFactory _factory;
    private readonly HttpClient _client;
    private readonly ITestQueueService _queue;

    public JobStorageTests(ITestOutputHelper output, ObiconServerFactory factory)
        : base(output)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new(ObiconServerFactory.AuthHeader);
        _queue = factory.Services.GetRequiredService<ITestQueueService>();
    }

    private async Task<TestJob> CreateCompletedJobAsync()
    {
        var job = await _queue.EnqueueJobAsync(new TestJob
        {
            TestId = Guid.NewGuid(),
            NodeId = Guid.NewGuid(),
            Target = "example.com",
            TestType = TestType.Ping
        });

        return (await _queue.UpdateJobStatusAsync(job.Id, TestJobStatus.Completed, new TestResult
        {
            Success = true,
            DurationMs = 42,
            Output = "probe output",
            Details = new()
            {
                Ping = new()
                {
                    Target = "example.com",
                    Sent = 4,
                    Received = 3,
                    LossPercent = 25,
                    AvgRoundtripMs = 12.5,
                    Replies =
                    [
                        new() { ReplyAddress = "93.184.216.34", ReplyStatus = "Success", RoundtripMs = 12, Ttl = 56 },
                        new() { ReplyStatus = "TimedOut" }
                    ]
                }
            }
        }))!;
    }

    private async Task SetModeAsync(string mode)
    {
        var response = await _client.PutAsJsonAsync("/v1/settings/TestResultStorageMode", new { value = mode });
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Full_KeepsTheResultPayload()
    {
        var job = await CreateCompletedJobAsync();

        var stored = await _queue.GetJobAsync(job.Id);

        Assert.NotNull(stored);
        Assert.Equal(TestJobStatus.Completed, stored.Status);
        Assert.Equal(true, stored.Success);
        Assert.Equal(42, stored.DurationMs);
        Assert.Equal("probe output", stored.Output);
        Assert.NotNull(stored.CompletedAt);

        // The structured details round-trip through the normalized tables, order included
        // (the API serves details through the mapper, which restores the probe order)
        Assert.NotNull(stored.Ping);
        var details = TestResultDetailsMapper.ToShared(stored);
        Assert.NotNull(details);
        Assert.NotNull(details.Ping);
        Assert.Equal(4, details.Ping.Sent);
        Assert.Equal(25, details.Ping.LossPercent);
        Assert.Equal(2, details.Ping.Replies.Count);
        Assert.Equal("93.184.216.34", details.Ping.Replies[0].ReplyAddress);
        Assert.Equal(56, details.Ping.Replies[0].Ttl);
        Assert.Equal("TimedOut", details.Ping.Replies[1].ReplyStatus);
    }

    [Fact]
    public async Task MetadataOnly_StripsThePayloadButKeepsTheRow()
    {
        await SetModeAsync("MetadataOnly");
        try
        {
            var job = await CreateCompletedJobAsync();

            var stored = await _queue.GetJobAsync(job.Id);

            Assert.NotNull(stored);
            Assert.Equal(TestJobStatus.Completed, stored.Status);
            // The scalar outcome stays on the row; the structured details are stripped
            Assert.Equal(true, stored.Success);
            Assert.Equal(42, stored.DurationMs);
            Assert.Null(stored.Ping);
            Assert.Null(TestResultDetailsMapper.ToShared(stored));
            Assert.NotNull(stored.CompletedAt);
        }
        finally
        {
            await SetModeAsync("Full");
        }
    }

    [Fact]
    public async Task None_DeletesTheRowButReturnsTheJobForMetrics()
    {
        await SetModeAsync("None");
        try
        {
            var job = await CreateCompletedJobAsync();

            // The returned job is the metrics-before-deletion contract: the caller
            // still has the data to emit, even though the row is gone
            Assert.Equal(TestJobStatus.Completed, job.Status);
            Assert.NotNull(job.CompletedAt);

            Assert.Null(await _queue.GetJobAsync(job.Id));
        }
        finally
        {
            await SetModeAsync("Full");
        }
    }

    [Fact]
    public async Task None_DoesNotTouchLiveJobs()
    {
        await SetModeAsync("None");
        try
        {
            var job = await _queue.EnqueueJobAsync(new TestJob
            {
                TestId = Guid.NewGuid(),
                NodeId = Guid.NewGuid(),
                Target = "example.com",
                TestType = TestType.Ping
            });

            // A non-terminal transition must not apply the storage policy
            await _queue.UpdateJobStatusAsync(job.Id, TestJobStatus.Running);

            Assert.NotNull(await _queue.GetJobAsync(job.Id));
        }
        finally
        {
            await SetModeAsync("Full");
        }
    }

    [Fact]
    public async Task UnknownMode_FallsBackToFull()
    {
        // An invalid stored value must never silently delete results
        await _client.PutAsJsonAsync("/v1/settings/TestResultStorageMode", new { value = "RecycleBin" });

        var job = await CreateCompletedJobAsync();

        var stored = await _queue.GetJobAsync(job.Id);
        Assert.NotNull(stored);
        Assert.Equal(true, stored.Success);
        Assert.NotNull(stored.Ping);

        await SetModeAsync("Full");
    }

    [Fact]
    public async Task Prune_DeletesOnlyTerminalJobsOlderThanTheCutoff()
    {
        var old = await CreateCompletedJobAsync();
        var fresh = await CreateCompletedJobAsync();

        Backdate(old.Id, completed: true);

        await _queue.PruneJobsAsync(
            finishedBefore: DateTime.UtcNow - TimeSpan.FromDays(1),
            stuckBefore: DateTime.UtcNow - TimeSpan.FromDays(7));

        Assert.Null(await _queue.GetJobAsync(old.Id));
        Assert.NotNull(await _queue.GetJobAsync(fresh.Id));
    }

    [Fact]
    public async Task Prune_DeletesStuckLiveJobsPastTheSafetyCap()
    {
        var stuck = await _queue.EnqueueJobAsync(new TestJob
        {
            TestId = Guid.NewGuid(),
            NodeId = Guid.NewGuid(),
            Target = "example.com",
            TestType = TestType.Ping
        });
        var live = await _queue.EnqueueJobAsync(new TestJob
        {
            TestId = Guid.NewGuid(),
            NodeId = Guid.NewGuid(),
            Target = "example.com",
            TestType = TestType.Ping
        });

        Backdate(stuck.Id, completed: false);

        await _queue.PruneJobsAsync(
            finishedBefore: DateTime.UtcNow - TimeSpan.FromDays(30),
            stuckBefore: DateTime.UtcNow - TimeSpan.FromDays(7));

        Assert.Null(await _queue.GetJobAsync(stuck.Id));
        Assert.NotNull(await _queue.GetJobAsync(live.Id));
    }

    [Fact]
    public async Task RetentionSettings_AreExposedWithDefaults()
    {
        var settings = await _client.GetFromJsonAsync<JsonElement>("/v1/settings");
        var byKey = settings.EnumerateArray().ToDictionary(s => s.GetProperty("key").GetString()!, s => s);

        Assert.Equal("Full", byKey["TestResultStorageMode"].GetProperty("value").GetString());
        Assert.Equal("30", byKey["JobRetentionDays"].GetProperty("value").GetString());
    }

    /// <summary>
    /// Moves a job's timestamps into the past with raw SQL, simulating a row that has
    /// been sitting in the database longer than the retention window. Unquoted snake_case
    /// identifiers: the schema never needs quoting.
    /// </summary>
    private void Backdate(Guid jobId, bool completed)
    {
        var connectionString = _factory.Services.GetRequiredService<IConfiguration>()
            .GetConnectionString("Default");
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        var timestamp = completed ? "completed_at" : "created_at";
        command.CommandText = $"UPDATE test_jobs SET {timestamp} = @old WHERE id = @id";
        command.Parameters.AddWithValue("old", DateTime.UtcNow - TimeSpan.FromDays(10));
        command.Parameters.AddWithValue("id", jobId);
        command.ExecuteNonQuery();
    }
}
