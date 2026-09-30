using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Obicon.Server.Models;

namespace Obicon.Server.Data;

/// <summary>
/// SQLite persistence for nodes, tests, and test jobs.
/// </summary>
public class ObiconDbContext : DbContext
{
    /// <summary>
    /// Registered nodes. Default: empty.
    /// </summary>
    public DbSet<Node> Nodes => Set<Node>();

    /// <summary>
    /// User-defined node pools. Default: empty.
    /// </summary>
    public DbSet<NodePool> NodePools => Set<NodePool>();

    /// <summary>
    /// Configured tests. Default: empty.
    /// </summary>
    public DbSet<Test> Tests => Set<Test>();

    /// <summary>
    /// Test jobs in the execution queue. Default: empty.
    /// </summary>
    public DbSet<TestJob> TestJobs => Set<TestJob>();

    /// <summary>
    /// Runtime overrides of server settings. Default: empty.
    /// </summary>
    public DbSet<ServerSettingValue> ServerSettingValues => Set<ServerSettingValue>();

    /// <summary>
    /// Enroll tokens for node self-registration, stored as hashes. Default: empty.
    /// </summary>
    public DbSet<EnrollToken> EnrollTokens => Set<EnrollToken>();

    public ObiconDbContext(DbContextOptions<ObiconDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Rows created before the Headers column existed may hold a JSON array default
    /// (e.g. '[]' from an earlier schema migration); fall back to an empty dictionary.
    /// </summary>
    private static Dictionary<string, string> DeserializeHeaders(string v)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, string>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>();
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var guidListConverter = new ValueConverter<List<Guid>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<List<Guid>>(v, (JsonSerializerOptions?)null) ?? new List<Guid>());

        var stringListConverter = new ValueConverter<List<string>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

        var stringDictionaryConverter = new ValueConverter<Dictionary<string, string>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => DeserializeHeaders(v));

        var testResultConverter = new ValueConverter<TestResult?, string>(
            v => v == null ? string.Empty : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => string.IsNullOrEmpty(v) ? null : JsonSerializer.Deserialize<TestResult>(v, (JsonSerializerOptions?)null));

        // SQLite stores datetimes as TEXT without timezone info; re-mark them as UTC on read
        var utcConverter = new ValueConverter<DateTime, DateTime>(
            v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        var nullableUtcConverter = new ValueConverter<DateTime?, DateTime?>(
            v => v, v => v == null ? v : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc));

        modelBuilder.Entity<Node>()
            .HasIndex(n => n.AuthToken);

        modelBuilder.Entity<ServerSettingValue>()
            .HasKey(s => s.Key);

        modelBuilder.Entity<Node>()
            .Property(n => n.Labels)
            .HasConversion(stringListConverter);

        modelBuilder.Entity<NodePool>()
            .Property(p => p.NodeIds)
            .HasConversion(guidListConverter);

        modelBuilder.Entity<Test>()
            .Property(t => t.NodeIds)
            .HasConversion(guidListConverter);

        modelBuilder.Entity<Test>()
            .Property(t => t.PoolIds)
            .HasConversion(guidListConverter);

        modelBuilder.Entity<Test>()
            .Property(t => t.Headers)
            .HasConversion(stringDictionaryConverter);

        modelBuilder.Entity<TestJob>()
            .Property(j => j.Headers)
            .HasConversion(stringDictionaryConverter);

        modelBuilder.Entity<TestJob>()
            .Property(j => j.Result)
            .HasConversion(testResultConverter);

        modelBuilder.Entity<TestJob>()
            .HasIndex(j => new { j.NodeId, j.Status });

        modelBuilder.Entity<Node>()
            .Property(n => n.Settings)
            .HasConversion(stringDictionaryConverter);

        modelBuilder.Entity<Node>().Property(n => n.CreatedAt).HasConversion(utcConverter);
        modelBuilder.Entity<Node>().Property(n => n.LastSeenAt).HasConversion(nullableUtcConverter);

        modelBuilder.Entity<Test>().Property(t => t.CreatedAt).HasConversion(utcConverter);
        modelBuilder.Entity<Test>().Property(t => t.UpdatedAt).HasConversion(nullableUtcConverter);

        modelBuilder.Entity<TestJob>().Property(j => j.CreatedAt).HasConversion(utcConverter);
        modelBuilder.Entity<TestJob>().Property(j => j.StartedAt).HasConversion(nullableUtcConverter);
        modelBuilder.Entity<TestJob>().Property(j => j.AcknowledgedAt).HasConversion(nullableUtcConverter);
        modelBuilder.Entity<TestJob>().Property(j => j.CompletedAt).HasConversion(nullableUtcConverter);
    }
}
