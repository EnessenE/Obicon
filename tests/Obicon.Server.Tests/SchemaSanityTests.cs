using System.Net;
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
/// Sanity checks for the relational schema itself: every identifier is lowercase
/// snake_case (no quoted identifiers anywhere), no column is JSON, the join tables
/// carry cascading foreign keys, and deletes leave no stale rows behind.
/// </summary>
public class SchemaSanityTests : LoggedTest, IClassFixture<ObiconServerFactory>
{
    private readonly ObiconServerFactory _factory;
    private readonly HttpClient _client;
    private readonly ITestQueueService _queue;

    public SchemaSanityTests(ITestOutputHelper output, ObiconServerFactory factory)
        : base(output)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new(ObiconServerFactory.AuthHeader);
        _queue = factory.Services.GetRequiredService<ITestQueueService>();
    }

    /// <summary>
    /// Opens a direct connection to the fixture database, for row-level assertions.
    /// </summary>
    private NpgsqlConnection OpenConnection()
    {
        var connectionString = _factory.Services.GetRequiredService<IConfiguration>()
            .GetConnectionString("Default");
        var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        return connection;
    }

    /// <summary>
    /// Counts rows with raw SQL; unquoted snake_case identifiers only.
    /// </summary>
    private static long CountRows(NpgsqlConnection connection, string table, string where, params (string Name, object Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"select count(*) from {table} where {where}";
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }
        return (long)command.ExecuteScalar()!;
    }

    private async Task<Guid> CreateNodeAsync(string name)
    {
        var created = await _client.PostAsJsonAsync("/v1/nodes", new { name });
        created.EnsureSuccessStatusCode();
        return (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreatePoolAsync(string name)
    {
        var created = await _client.PostAsJsonAsync("/v1/pools", new { name });
        created.EnsureSuccessStatusCode();
        return (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    /// <summary>
    /// Creates an HTTP test through the API, targeting the given nodes and/or pools.
    /// </summary>
    private async Task<Guid> CreateTestAsync(Guid[] nodeIds, Guid[] poolIds, Dictionary<string, string>? headers = null)
    {
        var payload = new Dictionary<string, object?>
        {
            ["name"] = $"sanity-{Guid.NewGuid():N}",
            ["type"] = 2,
            ["target"] = "https://example.com",
            ["nodeIds"] = nodeIds.Select(id => id.ToString()).ToArray(),
            ["poolIds"] = poolIds.Select(id => id.ToString()).ToArray(),
            ["frequency"] = 3600,
            ["isActive"] = true,
            ["timeoutSeconds"] = 30,
            ["headers"] = headers ?? new Dictionary<string, string>()
        };

        var created = await _client.PostAsJsonAsync("/v1/tests", payload);
        created.EnsureSuccessStatusCode();
        return (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task SetStorageModeAsync(string mode)
    {
        var response = await _client.PutAsJsonAsync("/v1/settings/TestResultStorageMode", new { value = mode });
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Schema_NoCapitalizedIdentifiers()
    {
        await using var connection = OpenConnection();

        using (var command = connection.CreateCommand())
        {
            // Every table name is lowercase snake_case; the EF migrations history
            // table is EF's own bookkeeping and the one accepted exception
            command.CommandText = """
                select table_name from information_schema.tables
                where table_schema = 'public'
                  and table_name <> lower(table_name)
                  and table_name <> '__EFMigrationsHistory'
                """;
            Assert.Null(await command.ExecuteScalarAsync());
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select table_name, column_name from information_schema.columns
                where table_schema = 'public'
                  and column_name <> lower(column_name)
                  and table_name <> '__EFMigrationsHistory'
                """;
            using var reader = await command.ExecuteReaderAsync();
            Assert.False(reader.Read(), "no column name may contain uppercase letters");
        }
    }

    [Fact]
    public async Task Schema_NoJsonColumns()
    {
        await using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            select count(*) from information_schema.columns
            where table_schema = 'public' and data_type in ('json', 'jsonb')
            """;
        Assert.Equal(0L, (long)command.ExecuteScalar()!);
    }

    /// <summary>
    /// The junction and detail tables must have a cascading foreign key to every
    /// entity they reference, so a delete can never leave a stale row behind.
    /// </summary>
    [Theory]
    [InlineData("pool_members", 2)]
    [InlineData("test_target_nodes", 2)]
    [InlineData("test_target_pools", 2)]
    [InlineData("node_labels", 1)]
    [InlineData("node_reported_settings", 1)]
    [InlineData("test_headers", 1)]
    [InlineData("test_job_headers", 1)]
    [InlineData("test_job_traceroute_hops", 1)]
    [InlineData("test_job_traceroute_probes", 1)]
    [InlineData("test_job_ping_replies", 1)]
    [InlineData("test_job_dns_records", 1)]
    [InlineData("test_job_certificates", 1)]
    public async Task Schema_JoinTables_HaveCascadingForeignKeys(string table, int expectedCount)
    {
        await using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            select count(*) from pg_constraint
            where contype = 'f' and confdeltype = 'c' and conrelid = @table::regclass
            """;
        command.Parameters.AddWithValue("table", table);
        Assert.Equal(expectedCount, (int)(long)command.ExecuteScalar()!);
    }

    /// <summary>
    /// The hot paths must have their indexes: queue listing and the retention sweep
    /// (created_at), per-test latest-result sampling (test_id, completed_at), enroll
    /// token validation (token_hash), and pool-by-name resolution (name).
    /// </summary>
    [Theory]
    [InlineData("test_jobs", "test_id, completed_at")]
    [InlineData("test_jobs", "created_at")]
    [InlineData("enroll_tokens", "token_hash")]
    [InlineData("node_pools", "name")]
    public async Task Schema_HotPathsHaveTheirIndexes(string table, string columns)
    {
        await using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            select count(*) from pg_indexes
            where schemaname = 'public' and tablename = @table and indexdef like @def
            """;
        command.Parameters.AddWithValue("table", table);
        command.Parameters.AddWithValue("def", $"%({columns})%");
        Assert.Equal(1L, (long)command.ExecuteScalar()!);
    }

    [Fact]
    public async Task DeleteNode_RemovesItsJoinRows()
    {
        var nodeId = await CreateNodeAsync("cascade-node");
        var update = await _client.PutAsJsonAsync($"/v1/nodes/{nodeId}", new
        {
            name = "cascade-node",
            labels = new[] { "edge", "home" }
        });
        update.EnsureSuccessStatusCode();

        var poolId = await CreatePoolAsync("cascade-pool");
        var members = await _client.PutAsJsonAsync($"/v1/pools/{poolId}/nodes", new { nodeIds = new[] { nodeId.ToString() } });
        members.EnsureSuccessStatusCode();

        var testId = await CreateTestAsync([nodeId], []);

        var delete = await _client.DeleteAsync($"/v1/nodes/{nodeId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        await using var connection = OpenConnection();
        Assert.Equal(0L, CountRows(connection, "node_labels", "node_id = @id", ("id", nodeId)));
        Assert.Equal(0L, CountRows(connection, "node_reported_settings", "node_id = @id", ("id", nodeId)));
        Assert.Equal(0L, CountRows(connection, "pool_members", "node_id = @id", ("id", nodeId)));
        Assert.Equal(0L, CountRows(connection, "test_target_nodes", "node_id = @id", ("id", nodeId)));

        // The pool survives, now without members; the test survives, now untargeted
        Assert.Equal(0L, CountRows(connection, "pool_members", "pool_id = @id", ("id", poolId)));
        var test = await _client.GetFromJsonAsync<JsonElement>($"/v1/tests/{testId}");
        Assert.Empty(test.GetProperty("nodeIds").EnumerateArray());
    }

    [Fact]
    public async Task DeletePool_RemovesMembershipAndPoolTargetRows()
    {
        var nodeId = await CreateNodeAsync("pool-cascade-node");
        var poolId = await CreatePoolAsync("pool-cascade");
        var members = await _client.PutAsJsonAsync($"/v1/pools/{poolId}/nodes", new { nodeIds = new[] { nodeId.ToString() } });
        members.EnsureSuccessStatusCode();

        var testId = await CreateTestAsync([nodeId], [poolId]);

        var delete = await _client.DeleteAsync($"/v1/pools/{poolId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        await using var connection = OpenConnection();
        Assert.Equal(0L, CountRows(connection, "pool_members", "pool_id = @id", ("id", poolId)));
        Assert.Equal(0L, CountRows(connection, "test_target_pools", "pool_id = @id", ("id", poolId)));

        // The targeted test survives: the pool target is gone, the direct one stays
        var test = await _client.GetFromJsonAsync<JsonElement>($"/v1/tests/{testId}");
        Assert.Empty(test.GetProperty("poolIds").EnumerateArray());
        Assert.Equal(nodeId, test.GetProperty("nodeIds")[0].GetGuid());
    }

    [Fact]
    public async Task DeleteTest_RemovesTargetAndHeaderRows()
    {
        var nodeId = await CreateNodeAsync("test-cascade-node");
        var testId = await CreateTestAsync([nodeId], [], new Dictionary<string, string> { ["X-Token"] = "secret" });

        await using (var connection = OpenConnection())
        {
            Assert.Equal(1L, CountRows(connection, "test_headers", "test_id = @id", ("id", testId)));
            Assert.Equal(1L, CountRows(connection, "test_target_nodes", "test_id = @id", ("id", testId)));
        }

        var delete = await _client.DeleteAsync($"/v1/tests/{testId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        await using (var connection = OpenConnection())
        {
            Assert.Equal(0L, CountRows(connection, "test_headers", "test_id = @id", ("id", testId)));
            Assert.Equal(0L, CountRows(connection, "test_target_nodes", "test_id = @id", ("id", testId)));
            Assert.Equal(0L, CountRows(connection, "test_target_pools", "test_id = @id", ("id", testId)));
        }
    }

    [Fact]
    public async Task StorageNone_DeletesHeaderAndDetailRows()
    {
        await SetStorageModeAsync("None");
        try
        {
            var job = await _queue.EnqueueJobAsync(new TestJob
            {
                TestId = Guid.NewGuid(),
                NodeId = Guid.NewGuid(),
                Target = "example.com",
                TestType = TestType.Ping,
                Headers = [new TestJobHeader { Name = "X-Trace", Value = "abc" }]
            });

            var completed = await _queue.UpdateJobStatusAsync(job.Id, TestJobStatus.Completed, new TestResult
            {
                Success = true,
                DurationMs = 10,
                Output = "out",
                Details = new()
                {
                    Ping = new()
                    {
                        Target = "example.com",
                        Sent = 1,
                        Received = 1,
                        Replies = [new() { ReplyAddress = "1.2.3.4", ReplyStatus = "Success", RoundtripMs = 5 }]
                    }
                }
            });
            Assert.NotNull(completed);

            // The row and every child row went together through the cascade
            await using var connection = OpenConnection();
            Assert.Equal(0L, CountRows(connection, "test_jobs", "id = @id", ("id", job.Id)));
            Assert.Equal(0L, CountRows(connection, "test_job_headers", "job_id = @id", ("id", job.Id)));
            Assert.Equal(0L, CountRows(connection, "test_job_ping_details", "job_id = @id", ("id", job.Id)));
            Assert.Equal(0L, CountRows(connection, "test_job_ping_replies", "job_id = @id", ("id", job.Id)));
        }
        finally
        {
            await SetStorageModeAsync("Full");
        }
    }

    [Fact]
    public async Task MetadataOnly_StripsDetailRows_KeepsScalarOutcome()
    {
        await SetStorageModeAsync("MetadataOnly");
        try
        {
            var job = await _queue.EnqueueJobAsync(new TestJob
            {
                TestId = Guid.NewGuid(),
                NodeId = Guid.NewGuid(),
                Target = "example.com",
                TestType = TestType.Dns
            });

            var completed = await _queue.UpdateJobStatusAsync(job.Id, TestJobStatus.Completed, new TestResult
            {
                Success = true,
                DurationMs = 77,
                Output = "no answer",
                Details = new()
                {
                    Dns = new()
                    {
                        Host = "example.com",
                        Records = [new() { RecordType = "A", Value = "93.184.216.34", TtlSeconds = 300 }]
                    }
                }
            });
            Assert.NotNull(completed);

            await using var connection = OpenConnection();
            // The row skeleton keeps the scalar outcome; no detail rows were written
            Assert.Equal(1L, CountRows(connection, "test_jobs", "id = @id", ("id", job.Id)));
            Assert.Equal(0L, CountRows(connection, "test_job_dns_details", "job_id = @id", ("id", job.Id)));
            Assert.Equal(0L, CountRows(connection, "test_job_dns_records", "job_id = @id", ("id", job.Id)));

            using var command = connection.CreateCommand();
            command.CommandText = "select success, duration_ms, output from test_jobs where id = @id";
            command.Parameters.AddWithValue("id", job.Id);
            using var reader = command.ExecuteReader();
            Assert.True(reader.Read());
            Assert.True(reader.GetBoolean(0));
            Assert.Equal(77L, reader.GetInt64(1));
            Assert.Equal("no answer", reader.GetString(2));
        }
        finally
        {
            await SetStorageModeAsync("Full");
        }
    }

    [Fact]
    public async Task TestHeaders_AreFrozenOntoTheJobAtEnqueueTime()
    {
        var nodeId = await CreateNodeAsync("header-freeze-node");
        var testId = await CreateTestAsync([nodeId], [], new Dictionary<string, string> { ["X-Original"] = "first" });

        var firstRun = await _client.PostAsync($"/v1/tests/{testId}/run", content: null);
        Assert.True(firstRun.IsSuccessStatusCode);

        var firstJob = (await _queue.GetRunsAsync(new(50, 0, null, nodeId, null, null))).Items.First();
        Assert.Equal("first", firstJob.Headers.Single(h => h.Name == "X-Original").Value);

        // Editing the test must never reach the already-enqueued job
        var payload = new Dictionary<string, object?>
        {
            ["name"] = $"sanity-{Guid.NewGuid():N}",
            ["type"] = 2,
            ["target"] = "https://example.com",
            ["nodeIds"] = new[] { nodeId.ToString() },
            ["poolIds"] = Array.Empty<string>(),
            ["frequency"] = 3600,
            ["isActive"] = true,
            ["timeoutSeconds"] = 30,
            ["headers"] = new Dictionary<string, string> { ["X-Original"] = "second" }
        };
        var update = await _client.PutAsJsonAsync($"/v1/tests/{testId}", payload);
        update.EnsureSuccessStatusCode();

        var secondRun = await _client.PostAsync($"/v1/tests/{testId}/run", content: null);
        Assert.True(secondRun.IsSuccessStatusCode);

        var jobs = (await _queue.GetRunsAsync(new(500, 0, null, nodeId, null, null)))
            .Items.OrderBy(j => j.CreatedAt).ToList();
        Assert.Equal(2, jobs.Count);
        Assert.Equal("first", jobs[0].Headers.Single(h => h.Name == "X-Original").Value);
        Assert.Equal("second", jobs[1].Headers.Single(h => h.Name == "X-Original").Value);
    }
}
