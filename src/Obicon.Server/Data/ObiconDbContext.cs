using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Obicon.Server.Models;

namespace Obicon.Server.Data;

/// <summary>
/// PostgreSQL persistence for nodes, tests, and test jobs.
/// </summary>
public class ObiconDbContext : DbContext
{
    /// <summary>
    /// Registered nodes. Default: empty.
    /// </summary>
    public DbSet<Node> Nodes => Set<Node>();

    /// <summary>
    /// Labels attached to nodes, one row per label. Default: empty.
    /// </summary>
    public DbSet<NodeLabel> NodeLabels => Set<NodeLabel>();

    /// <summary>
    /// Operating settings nodes reported about themselves, one row per key. Default: empty.
    /// </summary>
    public DbSet<NodeReportedSetting> NodeReportedSettings => Set<NodeReportedSetting>();

    /// <summary>
    /// User-defined node pools. Default: empty.
    /// </summary>
    public DbSet<NodePool> NodePools => Set<NodePool>();

    /// <summary>
    /// Pool membership, one row per member node. Default: empty.
    /// </summary>
    public DbSet<PoolMember> PoolMembers => Set<PoolMember>();

    /// <summary>
    /// Configured tests. Default: empty.
    /// </summary>
    public DbSet<Test> Tests => Set<Test>();

    /// <summary>
    /// Nodes directly targeted by tests, one row per targeted node. Default: empty.
    /// </summary>
    public DbSet<TestTargetNode> TestTargetNodes => Set<TestTargetNode>();

    /// <summary>
    /// Pools targeted by tests, one row per targeted pool. Default: empty.
    /// </summary>
    public DbSet<TestTargetPool> TestTargetPools => Set<TestTargetPool>();

    /// <summary>
    /// Custom HTTP headers attached to tests, one row per header. Default: empty.
    /// </summary>
    public DbSet<TestHeader> TestHeaders => Set<TestHeader>();

    /// <summary>
    /// Test jobs in the execution queue. Default: empty.
    /// </summary>
    public DbSet<TestJob> TestJobs => Set<TestJob>();

    /// <summary>
    /// Custom HTTP headers copied onto jobs at enqueue time, one row per header. Default: empty.
    /// </summary>
    public DbSet<TestJobHeader> TestJobHeaders => Set<TestJobHeader>();

    /// <summary>
    /// Traceroute details of finished runs, one row per run. Default: empty.
    /// </summary>
    public DbSet<TestJobTracerouteDetails> TestJobTracerouteDetails => Set<TestJobTracerouteDetails>();

    /// <summary>
    /// Hops of traceroute runs, one row per hop. Default: empty.
    /// </summary>
    public DbSet<TestJobTracerouteHop> TestJobTracerouteHops => Set<TestJobTracerouteHop>();

    /// <summary>
    /// Probes of traceroute hops, one row per probe. Default: empty.
    /// </summary>
    public DbSet<TestJobTracerouteProbe> TestJobTracerouteProbes => Set<TestJobTracerouteProbe>();

    /// <summary>
    /// Ping details of finished runs, one row per run. Default: empty.
    /// </summary>
    public DbSet<TestJobPingDetails> TestJobPingDetails => Set<TestJobPingDetails>();

    /// <summary>
    /// Replies of ping runs, one row per reply. Default: empty.
    /// </summary>
    public DbSet<TestJobPingReply> TestJobPingReplies => Set<TestJobPingReply>();

    /// <summary>
    /// TCP details of finished runs, one row per run. Default: empty.
    /// </summary>
    public DbSet<TestJobTcpDetails> TestJobTcpDetails => Set<TestJobTcpDetails>();

    /// <summary>
    /// HTTP(S) details of finished runs, one row per run. Default: empty.
    /// </summary>
    public DbSet<TestJobHttpDetails> TestJobHttpDetails => Set<TestJobHttpDetails>();

    /// <summary>
    /// DNS details of finished runs, one row per run. Default: empty.
    /// </summary>
    public DbSet<TestJobDnsDetails> TestJobDnsDetails => Set<TestJobDnsDetails>();

    /// <summary>
    /// Records returned by DNS runs, one row per record. Default: empty.
    /// </summary>
    public DbSet<TestJobDnsRecord> TestJobDnsRecords => Set<TestJobDnsRecord>();

    /// <summary>
    /// TLS details of finished runs, one row per run. Default: empty.
    /// </summary>
    public DbSet<TestJobTlsDetails> TestJobTlsDetails => Set<TestJobTlsDetails>();

    /// <summary>
    /// Certificates reported by finished HTTP(S)/TLS runs, one row per run. Default: empty.
    /// </summary>
    public DbSet<TestJobCertificate> TestJobCertificates => Set<TestJobCertificate>();

    /// <summary>
    /// Runtime overrides of server settings. Default: empty.
    /// </summary>
    public DbSet<ServerSettingValue> ServerSettingValues => Set<ServerSettingValue>();
    public DbSet<ServerSettingListValue> ServerSettingListValues => Set<ServerSettingListValue>();

    /// <summary>
    /// Enroll tokens for node self-registration, stored as hashes. Default: empty.
    /// </summary>
    public DbSet<EnrollToken> EnrollTokens => Set<EnrollToken>();

    public ObiconDbContext(DbContextOptions<ObiconDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Npgsql maps DateTime to timestamp with time zone, which only accepts UTC values:
        // mark every persisted datetime as UTC on both write and read
        var utcConverter = new ValueConverter<DateTime, DateTime>(
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc), v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        var nullableUtcConverter = new ValueConverter<DateTime?, DateTime?>(
            v => v == null ? v : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc), v => v == null ? v : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc));

        modelBuilder.Entity<Node>()
            .HasIndex(n => n.AuthToken);

        // Indexes for the hot paths: auth and enrollment token lookups, pool-by-name
        // resolution at enrollment, and the queue's newest-first listing, retention
        // sweep, and per-test latest-result sampling
        modelBuilder.Entity<EnrollToken>()
            .HasIndex(t => t.TokenHash);

        modelBuilder.Entity<NodePool>()
            .HasIndex(p => p.Name);

        modelBuilder.Entity<ServerSettingValue>()
            .HasKey(s => s.Key);

        // List-typed setting overrides: one ordered row per item, never JSON text
        modelBuilder.Entity<ServerSettingListValue>()
            .HasKey(v => new { v.Key, v.Position });

        // Relationship and dictionary tables: no ID lists or key-value maps are ever
        // serialized into a column; each reference is a row with a cascading foreign key
        modelBuilder.Entity<NodeLabel>()
            .HasKey(l => new { l.NodeId, l.Label });
        modelBuilder.Entity<NodeLabel>()
            .HasOne<Node>()
            .WithMany(n => n.Labels)
            .HasForeignKey(l => l.NodeId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<NodeReportedSetting>()
            .HasKey(s => new { s.NodeId, s.Key });
        modelBuilder.Entity<NodeReportedSetting>()
            .HasOne<Node>()
            .WithMany(n => n.Settings)
            .HasForeignKey(s => s.NodeId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PoolMember>()
            .HasKey(m => new { m.PoolId, m.NodeId });
        modelBuilder.Entity<PoolMember>()
            .HasOne<NodePool>()
            .WithMany(p => p.Members)
            .HasForeignKey(m => m.PoolId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<PoolMember>()
            .HasOne<Node>()
            .WithMany()
            .HasForeignKey(m => m.NodeId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TestTargetNode>()
            .HasKey(t => new { t.TestId, t.NodeId });
        modelBuilder.Entity<TestTargetNode>()
            .HasOne<Test>()
            .WithMany(t => t.NodeTargets)
            .HasForeignKey(t => t.TestId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<TestTargetNode>()
            .HasOne<Node>()
            .WithMany()
            .HasForeignKey(t => t.NodeId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TestTargetPool>()
            .HasKey(t => new { t.TestId, t.PoolId });
        modelBuilder.Entity<TestTargetPool>()
            .HasOne<Test>()
            .WithMany(t => t.PoolTargets)
            .HasForeignKey(t => t.TestId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<TestTargetPool>()
            .HasOne<NodePool>()
            .WithMany()
            .HasForeignKey(t => t.PoolId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TestHeader>()
            .HasKey(h => new { h.TestId, h.Name });
        modelBuilder.Entity<TestHeader>()
            .HasOne<Test>()
            .WithMany(t => t.Headers)
            .HasForeignKey(h => h.TestId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TestJobHeader>()
            .HasKey(h => new { h.JobId, h.Name });
        modelBuilder.Entity<TestJobHeader>()
            .HasOne<TestJob>()
            .WithMany(j => j.Headers)
            .HasForeignKey(h => h.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        // Result details: one table per test-type section, 1:1 with the job, the
        // array payloads as child tables - all rows cascade on job deletion
        modelBuilder.Entity<TestJobTracerouteDetails>()
            .HasKey(d => d.JobId);
        modelBuilder.Entity<TestJobTracerouteDetails>()
            .HasOne<TestJob>()
            .WithOne(j => j.Traceroute)
            .HasForeignKey<TestJobTracerouteDetails>(d => d.JobId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<TestJobTracerouteDetails>()
            .HasMany(d => d.Hops)
            .WithOne()
            .HasForeignKey(h => h.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TestJobTracerouteHop>()
            .HasKey(h => h.Id);
        modelBuilder.Entity<TestJobTracerouteHop>()
            .HasMany(h => h.Probes)
            .WithOne()
            .HasForeignKey(p => p.HopId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TestJobTracerouteProbe>()
            .HasKey(p => new { p.HopId, p.Ordinal });

        modelBuilder.Entity<TestJobPingDetails>()
            .HasKey(d => d.JobId);
        modelBuilder.Entity<TestJobPingDetails>()
            .HasOne<TestJob>()
            .WithOne(j => j.Ping)
            .HasForeignKey<TestJobPingDetails>(d => d.JobId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<TestJobPingDetails>()
            .HasMany(d => d.Replies)
            .WithOne()
            .HasForeignKey(r => r.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TestJobPingReply>()
            .HasKey(r => r.Id);

        modelBuilder.Entity<TestJobTcpDetails>()
            .HasKey(d => d.JobId);
        modelBuilder.Entity<TestJobTcpDetails>()
            .HasOne<TestJob>()
            .WithOne(j => j.Tcp)
            .HasForeignKey<TestJobTcpDetails>(d => d.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TestJobHttpDetails>()
            .HasKey(d => d.JobId);
        modelBuilder.Entity<TestJobHttpDetails>()
            .HasOne<TestJob>()
            .WithOne(j => j.Http)
            .HasForeignKey<TestJobHttpDetails>(d => d.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TestJobDnsDetails>()
            .HasKey(d => d.JobId);
        modelBuilder.Entity<TestJobDnsDetails>()
            .HasOne<TestJob>()
            .WithOne(j => j.Dns)
            .HasForeignKey<TestJobDnsDetails>(d => d.JobId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<TestJobDnsDetails>()
            .HasMany(d => d.Records)
            .WithOne()
            .HasForeignKey(r => r.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TestJobDnsRecord>()
            .HasKey(r => r.Id);

        modelBuilder.Entity<TestJobTlsDetails>()
            .HasKey(d => d.JobId);
        modelBuilder.Entity<TestJobTlsDetails>()
            .HasOne<TestJob>()
            .WithOne(j => j.Tls)
            .HasForeignKey<TestJobTlsDetails>(d => d.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TestJobCertificate>()
            .HasKey(c => c.JobId);
        modelBuilder.Entity<TestJobCertificate>()
            .HasOne<TestJob>()
            .WithOne(j => j.Certificate)
            .HasForeignKey<TestJobCertificate>(c => c.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TestJob>()
            .HasIndex(j => new { j.NodeId, j.Status });
        modelBuilder.Entity<TestJob>()
            .HasIndex(j => new { j.Status, j.CompletedAt });
        modelBuilder.Entity<TestJob>()
            .HasIndex(j => j.CreatedAt);
        modelBuilder.Entity<TestJob>()
            .HasIndex(j => new { j.TestId, j.CompletedAt });

        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetProperties()))
        {
            var type = property.ClrType;
            if (type == typeof(DateTime))
            {
                property.SetValueConverter(utcConverter);
            }
            else if (type == typeof(DateTime?))
            {
                property.SetValueConverter(nullableUtcConverter);
            }
        }

        // PostgreSQL naming: every table, column, and constraint name is snake_case, so
        // queries never need quoted identifiers (select * from tests, not "Tests").
        // Keys, foreign keys, and indexes derive their default names from the table
        // and column names, so they follow automatically.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            entityType.SetTableName(ToSnakeCase(entityType.GetTableName() ?? entityType.DisplayName()));
            foreach (var property in entityType.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));
            }
        }
    }

    /// <summary>
    /// Converts a PascalCase identifier to snake_case: AuthToken becomes auth_token.
    /// </summary>
    private static string ToSnakeCase(string name)
    {
        var result = new StringBuilder(name.Length + 8);
        foreach (var c in name)
        {
            if (char.IsUpper(c))
            {
                if (result.Length > 0)
                {
                    result.Append('_');
                }
                result.Append(char.ToLowerInvariant(c));
            }
            else
            {
                result.Append(c);
            }
        }
        return result.ToString();
    }
}
