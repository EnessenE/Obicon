using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Obicon.Server.Data.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "EnrollTokens",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "text", nullable: false),
                TokenHash = table.Column<string>(type: "text", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                PoolId = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EnrollTokens", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "NodePools",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "text", nullable: false),
                Description = table.Column<string>(type: "text", nullable: false),
                NodeIds = table.Column<string>(type: "jsonb", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_NodePools", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Nodes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "text", nullable: false),
                AuthToken = table.Column<string>(type: "text", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                Labels = table.Column<string>(type: "jsonb", nullable: false),
                EnrollmentType = table.Column<int>(type: "integer", nullable: false),
                Version = table.Column<string>(type: "text", nullable: true),
                IpAddress = table.Column<string>(type: "text", nullable: true),
                InternalIpv4 = table.Column<string>(type: "text", nullable: true),
                InternalIpv6 = table.Column<string>(type: "text", nullable: true),
                ExternalIpv4 = table.Column<string>(type: "text", nullable: true),
                ExternalIpv6 = table.Column<string>(type: "text", nullable: true),
                Settings = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Nodes", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ServerSettingValues",
            columns: table => new
            {
                Key = table.Column<string>(type: "text", nullable: false),
                Value = table.Column<string>(type: "text", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ServerSettingValues", x => x.Key);
            });

        migrationBuilder.CreateTable(
            name: "TestJobs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TestId = table.Column<Guid>(type: "uuid", nullable: false),
                NodeId = table.Column<Guid>(type: "uuid", nullable: false),
                TestType = table.Column<int>(type: "integer", nullable: false),
                Target = table.Column<string>(type: "text", nullable: false),
                TimeoutSeconds = table.Column<int>(type: "integer", nullable: false),
                ExpectedStatusCodes = table.Column<string>(type: "text", nullable: false),
                CheckCertificateExpiryDays = table.Column<int>(type: "integer", nullable: true),
                ExpectedDnsResult = table.Column<string>(type: "text", nullable: true),
                IpVersion = table.Column<int>(type: "integer", nullable: false),
                ExpectedBodyPattern = table.Column<string>(type: "text", nullable: true),
                Headers = table.Column<string>(type: "jsonb", nullable: false),
                ProxyUrl = table.Column<string>(type: "text", nullable: true),
                CacheBust = table.Column<bool>(type: "boolean", nullable: false),
                TracerouteMaxHops = table.Column<int>(type: "integer", nullable: true),
                TracerouteQueriesPerHop = table.Column<int>(type: "integer", nullable: true),
                TracerouteQueryTimeoutMs = table.Column<int>(type: "integer", nullable: true),
                TracerouteResolveHostnames = table.Column<bool>(type: "boolean", nullable: true),
                PingCount = table.Column<int>(type: "integer", nullable: true),
                PingTimeoutMs = table.Column<int>(type: "integer", nullable: true),
                PingIntervalMs = table.Column<int>(type: "integer", nullable: true),
                HttpMethod = table.Column<string>(type: "text", nullable: true),
                FollowRedirects = table.Column<bool>(type: "boolean", nullable: true),
                DnsNameserver = table.Column<string>(type: "text", nullable: true),
                DnsQueryType = table.Column<string>(type: "text", nullable: true),
                Status = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                AcknowledgedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                Result = table.Column<string>(type: "jsonb", nullable: true),
                ErrorMessage = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TestJobs", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Tests",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "text", nullable: false),
                Type = table.Column<int>(type: "integer", nullable: false),
                Target = table.Column<string>(type: "text", nullable: false),
                NodeIds = table.Column<string>(type: "jsonb", nullable: false),
                PoolIds = table.Column<string>(type: "jsonb", nullable: false),
                Frequency = table.Column<int>(type: "integer", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                ExpectedStatusCodes = table.Column<string>(type: "text", nullable: false),
                CheckCertificateExpiryDays = table.Column<int>(type: "integer", nullable: true),
                ExpectedDnsResult = table.Column<string>(type: "text", nullable: true),
                IpVersion = table.Column<int>(type: "integer", nullable: false),
                ExpectedBodyPattern = table.Column<string>(type: "text", nullable: true),
                Headers = table.Column<string>(type: "jsonb", nullable: false),
                ProxyUrl = table.Column<string>(type: "text", nullable: true),
                CacheBust = table.Column<bool>(type: "boolean", nullable: false),
                TracerouteMaxHops = table.Column<int>(type: "integer", nullable: true),
                TracerouteQueriesPerHop = table.Column<int>(type: "integer", nullable: true),
                TracerouteQueryTimeoutMs = table.Column<int>(type: "integer", nullable: true),
                TracerouteResolveHostnames = table.Column<bool>(type: "boolean", nullable: true),
                PingCount = table.Column<int>(type: "integer", nullable: true),
                PingTimeoutMs = table.Column<int>(type: "integer", nullable: true),
                PingIntervalMs = table.Column<int>(type: "integer", nullable: true),
                HttpMethod = table.Column<string>(type: "text", nullable: true),
                FollowRedirects = table.Column<bool>(type: "boolean", nullable: true),
                DnsNameserver = table.Column<string>(type: "text", nullable: true),
                DnsQueryType = table.Column<string>(type: "text", nullable: true),
                TimeoutSeconds = table.Column<int>(type: "integer", nullable: false),
                LastScheduledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Tests", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Nodes_AuthToken",
            table: "Nodes",
            column: "AuthToken");

        migrationBuilder.CreateIndex(
            name: "IX_TestJobs_NodeId_Status",
            table: "TestJobs",
            columns: new[] { "NodeId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_TestJobs_Status_CompletedAt",
            table: "TestJobs",
            columns: new[] { "Status", "CompletedAt" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "EnrollTokens");

        migrationBuilder.DropTable(
            name: "NodePools");

        migrationBuilder.DropTable(
            name: "Nodes");

        migrationBuilder.DropTable(
            name: "ServerSettingValues");

        migrationBuilder.DropTable(
            name: "TestJobs");

        migrationBuilder.DropTable(
            name: "Tests");
    }
}
