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
            name: "enroll_tokens",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "text", nullable: false),
                token_hash = table.Column<string>(type: "text", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                pool_id = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_enroll_tokens", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "node_pools",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "text", nullable: false),
                description = table.Column<string>(type: "text", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_node_pools", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "nodes",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "text", nullable: false),
                auth_token = table.Column<string>(type: "text", nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                last_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                enrollment_type = table.Column<int>(type: "integer", nullable: false),
                version = table.Column<string>(type: "text", nullable: true),
                ip_address = table.Column<string>(type: "text", nullable: true),
                internal_ipv4 = table.Column<string>(type: "text", nullable: true),
                internal_ipv6 = table.Column<string>(type: "text", nullable: true),
                external_ipv4 = table.Column<string>(type: "text", nullable: true),
                external_ipv6 = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_nodes", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "server_setting_list_values",
            columns: table => new
            {
                key = table.Column<string>(type: "text", nullable: false),
                position = table.Column<int>(type: "integer", nullable: false),
                item = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_server_setting_list_values", x => new { x.key, x.position });
            });

        migrationBuilder.CreateTable(
            name: "server_setting_values",
            columns: table => new
            {
                key = table.Column<string>(type: "text", nullable: false),
                value = table.Column<string>(type: "text", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_server_setting_values", x => x.key);
            });

        migrationBuilder.CreateTable(
            name: "test_jobs",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                test_id = table.Column<Guid>(type: "uuid", nullable: false),
                node_id = table.Column<Guid>(type: "uuid", nullable: false),
                test_type = table.Column<int>(type: "integer", nullable: false),
                target = table.Column<string>(type: "text", nullable: false),
                timeout_seconds = table.Column<int>(type: "integer", nullable: false),
                expected_status_codes = table.Column<string>(type: "text", nullable: false),
                check_certificate_expiry_days = table.Column<int>(type: "integer", nullable: true),
                expected_dns_result = table.Column<string>(type: "text", nullable: true),
                ip_version = table.Column<int>(type: "integer", nullable: false),
                expected_body_pattern = table.Column<string>(type: "text", nullable: true),
                proxy_url = table.Column<string>(type: "text", nullable: true),
                cache_bust = table.Column<bool>(type: "boolean", nullable: false),
                traceroute_max_hops = table.Column<int>(type: "integer", nullable: true),
                traceroute_queries_per_hop = table.Column<int>(type: "integer", nullable: true),
                traceroute_query_timeout_ms = table.Column<int>(type: "integer", nullable: true),
                traceroute_resolve_hostnames = table.Column<bool>(type: "boolean", nullable: true),
                ping_count = table.Column<int>(type: "integer", nullable: true),
                ping_timeout_ms = table.Column<int>(type: "integer", nullable: true),
                ping_interval_ms = table.Column<int>(type: "integer", nullable: true),
                http_method = table.Column<string>(type: "text", nullable: true),
                follow_redirects = table.Column<bool>(type: "boolean", nullable: true),
                dns_nameserver = table.Column<string>(type: "text", nullable: true),
                dns_query_type = table.Column<string>(type: "text", nullable: true),
                status = table.Column<int>(type: "integer", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                acknowledged_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                success = table.Column<bool>(type: "boolean", nullable: true),
                duration_ms = table.Column<long>(type: "bigint", nullable: true),
                output = table.Column<string>(type: "text", nullable: true),
                error_message = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_test_jobs", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "tests",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "text", nullable: false),
                type = table.Column<int>(type: "integer", nullable: false),
                target = table.Column<string>(type: "text", nullable: false),
                frequency = table.Column<int>(type: "integer", nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                expected_status_codes = table.Column<string>(type: "text", nullable: false),
                check_certificate_expiry_days = table.Column<int>(type: "integer", nullable: true),
                expected_dns_result = table.Column<string>(type: "text", nullable: true),
                ip_version = table.Column<int>(type: "integer", nullable: false),
                expected_body_pattern = table.Column<string>(type: "text", nullable: true),
                proxy_url = table.Column<string>(type: "text", nullable: true),
                cache_bust = table.Column<bool>(type: "boolean", nullable: false),
                traceroute_max_hops = table.Column<int>(type: "integer", nullable: true),
                traceroute_queries_per_hop = table.Column<int>(type: "integer", nullable: true),
                traceroute_query_timeout_ms = table.Column<int>(type: "integer", nullable: true),
                traceroute_resolve_hostnames = table.Column<bool>(type: "boolean", nullable: true),
                ping_count = table.Column<int>(type: "integer", nullable: true),
                ping_timeout_ms = table.Column<int>(type: "integer", nullable: true),
                ping_interval_ms = table.Column<int>(type: "integer", nullable: true),
                http_method = table.Column<string>(type: "text", nullable: true),
                follow_redirects = table.Column<bool>(type: "boolean", nullable: true),
                dns_nameserver = table.Column<string>(type: "text", nullable: true),
                dns_query_type = table.Column<string>(type: "text", nullable: true),
                timeout_seconds = table.Column<int>(type: "integer", nullable: false),
                last_scheduled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tests", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "node_labels",
            columns: table => new
            {
                node_id = table.Column<Guid>(type: "uuid", nullable: false),
                label = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_node_labels", x => new { x.node_id, x.label });
                table.ForeignKey(
                    name: "FK_node_labels_nodes_node_id",
                    column: x => x.node_id,
                    principalTable: "nodes",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "node_reported_settings",
            columns: table => new
            {
                node_id = table.Column<Guid>(type: "uuid", nullable: false),
                key = table.Column<string>(type: "text", nullable: false),
                value = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_node_reported_settings", x => new { x.node_id, x.key });
                table.ForeignKey(
                    name: "FK_node_reported_settings_nodes_node_id",
                    column: x => x.node_id,
                    principalTable: "nodes",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "pool_members",
            columns: table => new
            {
                pool_id = table.Column<Guid>(type: "uuid", nullable: false),
                node_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_pool_members", x => new { x.pool_id, x.node_id });
                table.ForeignKey(
                    name: "FK_pool_members_node_pools_pool_id",
                    column: x => x.pool_id,
                    principalTable: "node_pools",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_pool_members_nodes_node_id",
                    column: x => x.node_id,
                    principalTable: "nodes",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "test_job_certificates",
            columns: table => new
            {
                job_id = table.Column<Guid>(type: "uuid", nullable: false),
                subject = table.Column<string>(type: "text", nullable: false),
                issuer = table.Column<string>(type: "text", nullable: false),
                not_before = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                not_after = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                days_remaining = table.Column<double>(type: "double precision", nullable: true),
                subject_alternative_names = table.Column<List<string>>(type: "text[]", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_test_job_certificates", x => x.job_id);
                table.ForeignKey(
                    name: "FK_test_job_certificates_test_jobs_job_id",
                    column: x => x.job_id,
                    principalTable: "test_jobs",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "test_job_dns_details",
            columns: table => new
            {
                job_id = table.Column<Guid>(type: "uuid", nullable: false),
                host = table.Column<string>(type: "text", nullable: false),
                nameservers_queried = table.Column<List<string>>(type: "text[]", nullable: false),
                answering_nameserver = table.Column<string>(type: "text", nullable: true),
                nameserver_rtt_ms = table.Column<double>(type: "double precision", nullable: true),
                resolved = table.Column<List<string>>(type: "text[]", nullable: false),
                via = table.Column<string>(type: "text", nullable: false),
                response_status = table.Column<string>(type: "text", nullable: true),
                query_type = table.Column<string>(type: "text", nullable: false),
                expected_address = table.Column<string>(type: "text", nullable: true),
                expected_matched = table.Column<bool>(type: "boolean", nullable: true),
                error = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_test_job_dns_details", x => x.job_id);
                table.ForeignKey(
                    name: "FK_test_job_dns_details_test_jobs_job_id",
                    column: x => x.job_id,
                    principalTable: "test_jobs",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "test_job_headers",
            columns: table => new
            {
                job_id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "text", nullable: false),
                value = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_test_job_headers", x => new { x.job_id, x.name });
                table.ForeignKey(
                    name: "FK_test_job_headers_test_jobs_job_id",
                    column: x => x.job_id,
                    principalTable: "test_jobs",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "test_job_http_details",
            columns: table => new
            {
                job_id = table.Column<Guid>(type: "uuid", nullable: false),
                url = table.Column<string>(type: "text", nullable: false),
                method = table.Column<string>(type: "text", nullable: false),
                final_url = table.Column<string>(type: "text", nullable: true),
                status_code = table.Column<int>(type: "integer", nullable: true),
                reason_phrase = table.Column<string>(type: "text", nullable: true),
                resolved_address = table.Column<string>(type: "text", nullable: true),
                dns_ms = table.Column<double>(type: "double precision", nullable: true),
                connect_ms = table.Column<double>(type: "double precision", nullable: true),
                tls_ms = table.Column<double>(type: "double precision", nullable: true),
                tls_protocol = table.Column<string>(type: "text", nullable: true),
                tls_cipher = table.Column<string>(type: "text", nullable: true),
                ttfb_ms = table.Column<double>(type: "double precision", nullable: true),
                transfer_ms = table.Column<double>(type: "double precision", nullable: true),
                bytes_read = table.Column<long>(type: "bigint", nullable: true),
                bytes_truncated = table.Column<bool>(type: "boolean", nullable: true),
                proxy_url = table.Column<string>(type: "text", nullable: true),
                body_matched = table.Column<bool>(type: "boolean", nullable: true),
                error = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_test_job_http_details", x => x.job_id);
                table.ForeignKey(
                    name: "FK_test_job_http_details_test_jobs_job_id",
                    column: x => x.job_id,
                    principalTable: "test_jobs",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "test_job_ping_details",
            columns: table => new
            {
                job_id = table.Column<Guid>(type: "uuid", nullable: false),
                target = table.Column<string>(type: "text", nullable: false),
                resolved_address = table.Column<string>(type: "text", nullable: true),
                dns_ms = table.Column<double>(type: "double precision", nullable: true),
                reply_address = table.Column<string>(type: "text", nullable: true),
                reply_status = table.Column<string>(type: "text", nullable: false),
                roundtrip_ms = table.Column<double>(type: "double precision", nullable: true),
                ttl = table.Column<int>(type: "integer", nullable: true),
                wallclock_ms = table.Column<double>(type: "double precision", nullable: true),
                sent = table.Column<int>(type: "integer", nullable: false),
                received = table.Column<int>(type: "integer", nullable: false),
                loss_percent = table.Column<double>(type: "double precision", nullable: false),
                min_roundtrip_ms = table.Column<double>(type: "double precision", nullable: true),
                avg_roundtrip_ms = table.Column<double>(type: "double precision", nullable: true),
                max_roundtrip_ms = table.Column<double>(type: "double precision", nullable: true),
                error = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_test_job_ping_details", x => x.job_id);
                table.ForeignKey(
                    name: "FK_test_job_ping_details_test_jobs_job_id",
                    column: x => x.job_id,
                    principalTable: "test_jobs",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "test_job_tcp_details",
            columns: table => new
            {
                job_id = table.Column<Guid>(type: "uuid", nullable: false),
                host = table.Column<string>(type: "text", nullable: false),
                port = table.Column<int>(type: "integer", nullable: false),
                resolved_address = table.Column<string>(type: "text", nullable: true),
                family = table.Column<string>(type: "text", nullable: true),
                dns_ms = table.Column<double>(type: "double precision", nullable: true),
                connect_ms = table.Column<double>(type: "double precision", nullable: true),
                error = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_test_job_tcp_details", x => x.job_id);
                table.ForeignKey(
                    name: "FK_test_job_tcp_details_test_jobs_job_id",
                    column: x => x.job_id,
                    principalTable: "test_jobs",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "test_job_tls_details",
            columns: table => new
            {
                job_id = table.Column<Guid>(type: "uuid", nullable: false),
                host = table.Column<string>(type: "text", nullable: false),
                port = table.Column<int>(type: "integer", nullable: false),
                resolved_address = table.Column<string>(type: "text", nullable: true),
                family = table.Column<string>(type: "text", nullable: true),
                dns_ms = table.Column<double>(type: "double precision", nullable: true),
                connect_ms = table.Column<double>(type: "double precision", nullable: true),
                handshake_ms = table.Column<double>(type: "double precision", nullable: true),
                protocol = table.Column<string>(type: "text", nullable: true),
                cipher = table.Column<string>(type: "text", nullable: true),
                error = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_test_job_tls_details", x => x.job_id);
                table.ForeignKey(
                    name: "FK_test_job_tls_details_test_jobs_job_id",
                    column: x => x.job_id,
                    principalTable: "test_jobs",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "test_job_traceroute_details",
            columns: table => new
            {
                job_id = table.Column<Guid>(type: "uuid", nullable: false),
                resolved_address = table.Column<string>(type: "text", nullable: true),
                target_reached = table.Column<bool>(type: "boolean", nullable: false),
                hop_count = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_test_job_traceroute_details", x => x.job_id);
                table.ForeignKey(
                    name: "FK_test_job_traceroute_details_test_jobs_job_id",
                    column: x => x.job_id,
                    principalTable: "test_jobs",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "test_headers",
            columns: table => new
            {
                test_id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "text", nullable: false),
                value = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_test_headers", x => new { x.test_id, x.name });
                table.ForeignKey(
                    name: "FK_test_headers_tests_test_id",
                    column: x => x.test_id,
                    principalTable: "tests",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "test_target_nodes",
            columns: table => new
            {
                test_id = table.Column<Guid>(type: "uuid", nullable: false),
                node_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_test_target_nodes", x => new { x.test_id, x.node_id });
                table.ForeignKey(
                    name: "FK_test_target_nodes_nodes_node_id",
                    column: x => x.node_id,
                    principalTable: "nodes",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_test_target_nodes_tests_test_id",
                    column: x => x.test_id,
                    principalTable: "tests",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "test_target_pools",
            columns: table => new
            {
                test_id = table.Column<Guid>(type: "uuid", nullable: false),
                pool_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_test_target_pools", x => new { x.test_id, x.pool_id });
                table.ForeignKey(
                    name: "FK_test_target_pools_node_pools_pool_id",
                    column: x => x.pool_id,
                    principalTable: "node_pools",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_test_target_pools_tests_test_id",
                    column: x => x.test_id,
                    principalTable: "tests",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "test_job_dns_records",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                job_id = table.Column<Guid>(type: "uuid", nullable: false),
                ordinal = table.Column<int>(type: "integer", nullable: false),
                record_type = table.Column<string>(type: "text", nullable: false),
                value = table.Column<string>(type: "text", nullable: false),
                ttl_seconds = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_test_job_dns_records", x => x.id);
                table.ForeignKey(
                    name: "FK_test_job_dns_records_test_job_dns_details_job_id",
                    column: x => x.job_id,
                    principalTable: "test_job_dns_details",
                    principalColumn: "job_id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "test_job_ping_replies",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                job_id = table.Column<Guid>(type: "uuid", nullable: false),
                ordinal = table.Column<int>(type: "integer", nullable: false),
                reply_address = table.Column<string>(type: "text", nullable: true),
                reply_status = table.Column<string>(type: "text", nullable: false),
                roundtrip_ms = table.Column<double>(type: "double precision", nullable: true),
                ttl = table.Column<int>(type: "integer", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_test_job_ping_replies", x => x.id);
                table.ForeignKey(
                    name: "FK_test_job_ping_replies_test_job_ping_details_job_id",
                    column: x => x.job_id,
                    principalTable: "test_job_ping_details",
                    principalColumn: "job_id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "test_job_traceroute_hops",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                job_id = table.Column<Guid>(type: "uuid", nullable: false),
                hop = table.Column<int>(type: "integer", nullable: false),
                address = table.Column<string>(type: "text", nullable: true),
                hostname = table.Column<string>(type: "text", nullable: true),
                status = table.Column<string>(type: "text", nullable: false),
                roundtrip_ms = table.Column<double>(type: "double precision", nullable: true),
                error = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_test_job_traceroute_hops", x => x.id);
                table.ForeignKey(
                    name: "FK_test_job_traceroute_hops_test_job_traceroute_details_job_id",
                    column: x => x.job_id,
                    principalTable: "test_job_traceroute_details",
                    principalColumn: "job_id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "test_job_traceroute_probes",
            columns: table => new
            {
                hop_id = table.Column<Guid>(type: "uuid", nullable: false),
                ordinal = table.Column<int>(type: "integer", nullable: false),
                status = table.Column<string>(type: "text", nullable: false),
                roundtrip_ms = table.Column<double>(type: "double precision", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_test_job_traceroute_probes", x => new { x.hop_id, x.ordinal });
                table.ForeignKey(
                    name: "FK_test_job_traceroute_probes_test_job_traceroute_hops_hop_id",
                    column: x => x.hop_id,
                    principalTable: "test_job_traceroute_hops",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_enroll_tokens_token_hash",
            table: "enroll_tokens",
            column: "token_hash");

        migrationBuilder.CreateIndex(
            name: "IX_node_pools_name",
            table: "node_pools",
            column: "name");

        migrationBuilder.CreateIndex(
            name: "IX_nodes_auth_token",
            table: "nodes",
            column: "auth_token");

        migrationBuilder.CreateIndex(
            name: "IX_pool_members_node_id",
            table: "pool_members",
            column: "node_id");

        migrationBuilder.CreateIndex(
            name: "IX_test_job_dns_records_job_id",
            table: "test_job_dns_records",
            column: "job_id");

        migrationBuilder.CreateIndex(
            name: "IX_test_job_ping_replies_job_id",
            table: "test_job_ping_replies",
            column: "job_id");

        migrationBuilder.CreateIndex(
            name: "IX_test_job_traceroute_hops_job_id",
            table: "test_job_traceroute_hops",
            column: "job_id");

        migrationBuilder.CreateIndex(
            name: "IX_test_jobs_created_at",
            table: "test_jobs",
            column: "created_at");

        migrationBuilder.CreateIndex(
            name: "IX_test_jobs_node_id_status",
            table: "test_jobs",
            columns: new[] { "node_id", "status" });

        migrationBuilder.CreateIndex(
            name: "IX_test_jobs_status_completed_at",
            table: "test_jobs",
            columns: new[] { "status", "completed_at" });

        migrationBuilder.CreateIndex(
            name: "IX_test_jobs_test_id_completed_at",
            table: "test_jobs",
            columns: new[] { "test_id", "completed_at" });

        migrationBuilder.CreateIndex(
            name: "IX_test_target_nodes_node_id",
            table: "test_target_nodes",
            column: "node_id");

        migrationBuilder.CreateIndex(
            name: "IX_test_target_pools_pool_id",
            table: "test_target_pools",
            column: "pool_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "enroll_tokens");

        migrationBuilder.DropTable(
            name: "node_labels");

        migrationBuilder.DropTable(
            name: "node_reported_settings");

        migrationBuilder.DropTable(
            name: "pool_members");

        migrationBuilder.DropTable(
            name: "server_setting_list_values");

        migrationBuilder.DropTable(
            name: "server_setting_values");

        migrationBuilder.DropTable(
            name: "test_headers");

        migrationBuilder.DropTable(
            name: "test_job_certificates");

        migrationBuilder.DropTable(
            name: "test_job_dns_records");

        migrationBuilder.DropTable(
            name: "test_job_headers");

        migrationBuilder.DropTable(
            name: "test_job_http_details");

        migrationBuilder.DropTable(
            name: "test_job_ping_replies");

        migrationBuilder.DropTable(
            name: "test_job_tcp_details");

        migrationBuilder.DropTable(
            name: "test_job_tls_details");

        migrationBuilder.DropTable(
            name: "test_job_traceroute_probes");

        migrationBuilder.DropTable(
            name: "test_target_nodes");

        migrationBuilder.DropTable(
            name: "test_target_pools");

        migrationBuilder.DropTable(
            name: "test_job_dns_details");

        migrationBuilder.DropTable(
            name: "test_job_ping_details");

        migrationBuilder.DropTable(
            name: "test_job_traceroute_hops");

        migrationBuilder.DropTable(
            name: "nodes");

        migrationBuilder.DropTable(
            name: "node_pools");

        migrationBuilder.DropTable(
            name: "tests");

        migrationBuilder.DropTable(
            name: "test_job_traceroute_details");

        migrationBuilder.DropTable(
            name: "test_jobs");
    }
}
