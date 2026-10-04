# Changelog

All notable changes to Obicon are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com), and each component uses
[semantic versioning](https://semver.org) independently.

Versions are tracked per component: `## [Server x.y.z]` and `## [Node x.y.z]`.
The CI pipeline publishes `ghcr.io/enessene/obicon/server:<server version>` and
`.../node:<node version>`, and creates the tags/releases `server-vx.y.z` and
`node-vx.y.z` with the matching section below as notes. The frontend has no
separate version; its changes are listed under the server release.

## [Server 0.5.0] - Unreleased

### Server
- **Breaking: PostgreSQL replaces SQLite as the server database.** The server now requires a PostgreSQL instance (connection string in `ConnectionStrings:Default`, e.g. `Host=localhost;Database=obicon;Username=postgres;Password=postgres`) and applies EF Core migrations on startup instead of the in-place SQLite schema patcher. The single-writer write queue is gone - PostgreSQL allows concurrent writers, so every unit of work runs directly against its own context. There is **no upgrade path from an existing `obicon.db`** (the project is below 1.0): back up the old file if you need its history; per the new retention contract, long-term history belongs in your metric store anyway. A `docker-compose.yml` ships in the repo root (server + PostgreSQL)
- **Breaking: the schema is fully relational and snake_case.** Every table, column, and constraint name is snake_case (`tests`, `node_id`, `created_at`), so queries never need quoted identifiers - `select * from tests`, not `select * from "Tests"`. No column is ever `jsonb`: node IDs, pool members, and test targets are junction tables (`pool_members`, `test_target_nodes`, `test_target_pools`), labels and reported settings are key-value rows (`node_labels`, `node_reported_settings`), headers are per-header rows (`test_headers`, `test_job_headers`), and a finished run's structured details are normalized into one table per test-type section (`test_job_traceroute_details` with `test_job_traceroute_hops` and `test_job_traceroute_probes`, `test_job_ping_details` with `test_job_ping_replies`, `test_job_tcp_details`, `test_job_http_details`, `test_job_dns_details` with `test_job_dns_records`, `test_job_tls_details`, `test_job_certificates`); only native `text[]` columns hold plain string lists (DNS nameservers/resolved addresses, certificate SANs). `MetadataOnly` storage now keeps the scalar outcome (success, duration, output) and strips only the structured details, matching its documented contract. Existing 0.5.0-beta databases need a reset (drop the schema or the volume)
- New `TestResultStorageMode` setting (default `Full`): `Full` keeps finished jobs with their complete result payload, `MetadataOnly` keeps the row skeleton but drops the payload on completion, `None` deletes the row the moment the run completes. Every terminal status respects the mode, and run metrics are always emitted before deletion
- New `JobRetentionDays` setting (default 30): a background sweep deletes finished test jobs older than the window - no archive, no exceptions; anything older exists only in your metric store. Jobs stuck in a live status for more than 7 days are swept as a safety cap. The settings UI renders the mode as a select
- New `TestMetricsLabels` setting (replaces `TestMetricsIncludeNodeLabels`, JSON array): chooses which labels ride along on `obicon.tests.runs` and `obicon.tests.duration_ms`. `test_id` and the counter's `status` are a forced floor; the default set is `test_type`, `test_name`, `node_name`, `node_labels` (node_name is the human-readable node dimension, node_id is the rename-stable opt-in), and an invalid value falls back to the defaults. The settings page renders one switch per label plus a permanently disabled `job_id` switch documenting why it can never be a metric label (high cardinality: one series per run)
- OTLP egress through OpenTelemetry: set `Otlp:Endpoint` in appsettings (env `Otlp__Endpoint`) to push metrics **and** node logs to your observability backend - Prometheus 3.x's OTLP receiver, a collector, Mimir, VictoriaMetrics, or a vendor. Absent or empty keeps today's scrape-only behavior; the `/metrics` Prometheus endpoint stays up either way. Retention of that history is your backend's flag, not ours
- Deleting a node now also removes it from every test that directly targets it, mirroring the pool cleanup - previously the stale node ID kept queueing jobs (marked `NoRun`) on every due interval of an active test
- `obicon.tests.queue_jobs` now exports every status from the very first boot (0 for statuses with no jobs), so the metric family is visible on a fresh install and dashboards show 0 instead of empty; the per-run series (`obicon.tests.runs`, `obicon.tests.duration_ms`) still first appear after the first finished run, as with any counter
- Node log funnel: log entries accepted from nodes (while `NodeLogShippingEnabled` is on) are now forwarded into the server's OTel logging pipeline and egress via the OTLP logs exporter, with the node's identity and the entry's properties as first-class fields: `node_id`, `node_name`, `node_version`, `source_context`, and every shipped property (`job_id`, `test_id`) - the join keys into the metrics. `ShipNodeLogsToConsole` keeps its separate console echo; the structured content is no longer dropped on the floor
- Tests: the server suite runs against a shared Testcontainers PostgreSQL with one database per test factory (Docker required, as for the integration suite), and the integration stack boots a PostgreSQL container next to the server. New coverage: storage modes, the metrics-before-deletion invariant, prune behavior, the label selection with its forced floor, and the log funnel's attribute contract
- Docs: `docs/getting-started.md` is now purely the published-image user path, driving the REST API via Swagger — it no longer tells users to self-host the web UI (which is not part of the published images); build-from-source moved to the new `docs/contributing.md`, the architecture diagram renders as a Mermaid diagram, and a standalone database compose ships in `test/` for running the server from source with the default connection string
- Secondary indexes on the hot paths: `test_jobs (test_id, completed_at)` for the per-test latest-result sampling, `test_jobs (created_at)` for the queue's newest-first listing and the stuck-job sweep, `enroll_tokens (token_hash)` for enrollment validation, and `node_pools (name)` for pool-by-name resolution

## [Node 0.5.0] - Unreleased

- **Breaking: the `Node:AllowUnsupportedServerVersion` setting is removed.** The node now always closes the connection to a server outside its supported version range; tolerating version mismatches is exclusively the server's decision (`AllowUnsupportedNodeVersions` on the server)
- Version bump only otherwise, so nodes stay within the server's supported version range (same major.minor as the server they connect to - server 0.5.0 disconnects 0.4.x nodes unless `AllowUnsupportedNodeVersions` is enabled)

## [Server 0.4.0] - Unreleased

### Server
- Tests can run over both IP families: the `ipVersion` field gains a `Both` value that schedules one job pinned to IPv4 and one pinned to IPv6 per targeted node (create, edit, run-once), so dual-stack coverage needs a single test; the queue API now reports each job's `ipVersion` and the queue page badges family-pinned runs
- HTTP(S) tests accept all standard request methods (GET, HEAD, POST, PUT, DELETE, PATCH, OPTIONS, TRACE); headers stay a plain dictionary end to end, and the web UI edits them as key/value rows instead of a free-text blob
- New TLS test type: handshake against a host:port, reporting the negotiated protocol and cipher plus the full certificate (subject, issuer, validity window, SANs), with the existing certificate-expiry threshold applying
- DNS tests can query specific record types: A, AAAA, CNAME, TXT, MX, or CAA (default remains both address families), with every returned record reported as a typed value with its TTL
- New `EnabledTestTypes` setting (default empty = all types): a JSON array of the test type names this server offers, e.g. `["Ping","Http","Dns"]`; creating, editing, or dry-running a disabled type is rejected with 400, the web UI hides disabled types from the dropdowns, and a new `GET /v1/tests/types` endpoint reports every type's enabled state — the settings page renders one toggle per type instead of the raw JSON
- New `NodeExternalIpResolvingEnabled` setting (default off): while disabled, nodes do not resolve their external (public) addresses via check services and report them as unavailable; enabling it propagates live to connected nodes and takes effect within a second
- Test results now carry structured details instead of the old flat stringly metrics dictionary: nodes report one typed section per test type (traceroute hops with address/status/RTT each, HTTP phase timings with certificate, DNS record lists), the server persists them on the job, and the queue API exposes them as `details`. Breaking: results stored by earlier versions keep their text output but no longer show their metrics
- New integration test suite (`tests/Obicon.Integration.Tests`, Testcontainers): builds the server and node images from the repo Dockerfiles, boots them on a shared docker network, and verifies the whole stack end to end — auto-enrollment over the WebSocket, HTTP/ping/DNS tests run by the node against the live server, and the metrics endpoint. It runs in CI as its own `integration-tests` job and needs a Docker engine locally
- Test logs land in the test console: server tests derive from `LoggedTest` and show the in-memory app's log trail per test (for failed tests and with detailed console verbosity), the node test suite gets a `TestOutputLogger` that writes a service's `ILogger` entries to the test output, and a failed integration stack start carries the server and node container logs in its failure message

- Per-test settings beyond the traceroute ones: ping probes per run (default 4, range 1-100), per-probe timeout (default 2000 ms), and interval between probes (default 0); HTTP(S) request method (GET or HEAD) and a follow-redirects toggle (default on); and a DNS nameserver override to query a specific resolver instead of the system's — all settable on create, edit, and dry runs with server-side validation
- Traceroute tests are configurable per test: maximum hops (default 30), probes per hop (default 3), per-probe timeout in ms (default 2000), and reverse hostname resolution per hop (default on) — settable on create, edit, and dry runs, validated server-side (1-64 hops, 1-10 probes, 100-60000 ms)

### Frontend
- Settings fields on test create, edit, and dry runs for every test type: traceroute hops/probes/hostnames, ping probes/timeout/interval (the ping result renders a per-reply table with loss and min/avg/max), HTTP method and redirects, and the DNS nameserver override
- Queue page and dry-run results render the structured details per test type — a hop table for traceroutes, phase timings and the certificate for HTTP(S), record lists for DNS — older results show only the text output

## [Node 0.4.0] - Unreleased

- New TLS runner: connects, handshakes, and reports the certificate chain details (including SANs), protocol, cipher, and per-phase timings; supports the certificate expiry threshold
- The DNS runner queries the requested record type (A, AAAA, CNAME, TXT, MX, CAA) and reports typed records with TTLs; the raw DNS client gained name decompression and the new record parsers
- External (public) address resolving is now gated by the server's `NodeExternalIpResolvingEnabled` policy (announced in the server hello and pushed live): while disabled the node contacts no check service and reports the addresses as unavailable, and a runtime change refreshes the addresses promptly
- Traceroute runs honor the new per-test settings — hop limit, probes per hop, per-probe timeout, and best-effort reverse hostname resolution per hop — and each hop's details now carry every probe's round trip and the resolved hostname
- Ping runs honor the new per-test settings (probe count, per-probe timeout, interval) and report every reply plus loss and min/avg/max round trip statistics; HTTP(S) runs support HEAD requests and disabling redirect following; DNS runs can target a specific nameserver
- DNS tests log and report more data: the nameserver answer now includes each record's TTL and the DNS response status (e.g. `NXDOMAIN`), logged per nameserver and carried in the result details
- Test runners report structured result details with every run — traceroute sends one record per hop (address, status, roundtrip, error) instead of only a text rendering, ping/TCP send their resolution and timing fields, HTTP(S) send phase timings, TLS certificate, and check outcomes, and DNS sends the queried nameservers and the returned A/AAAA records — in a `Details` section on the TestResult message, replacing the flat `Metrics` dictionary

## [Server 0.3.1] - 2026-10-03

### Server
- Coding standard: the repo now adheres to the C# Coding Guidelines (csharpcodingguidelines.com) — enforced by a root `.editorconfig` (naming and style rules, warnings in CLI builds) and a new `coding-guidelines` CI job that verifies formatting (`dotnet format`) and builds with warnings-as-errors. The full codebase was cleaned up to pass it: every log call is a source-generated `[LoggerMessage]` partial method (145 sites), culture-sensitive conversions specify `CultureInfo.InvariantCulture`, shared state classes expose properties instead of public fields, and `SqliteWriteQueue` disposes correctly
- The server's console sink moved from code to `appsettings.json` (`Serilog:WriteTo`, invariant culture), mirroring the node; when the section defines no sinks, the previous built-in default (plain console, invariant culture) takes over
- The API auth key (`ServerSettings:AuthHeader`, now defaulting to `secureobiconkey` instead of the placeholder `uwu`) is no longer hardcoded outside its defaults: the Swagger auth description shows the configured value, and the web UI keeps its key in localStorage (prompted on the first 401) so a deployment with a changed key can still use the UI. A deployment that sets `AuthHeader` in its configuration is unaffected; one relying on the built-in default moves to the new key on upgrade

## [Node 0.3.1] - 2026-10-03

- Same guidelines cleanup as the server: source-generated `[LoggerMessage]` logging throughout, invariant culture on all conversions, the node's shared state classes (`NodeStatistics`, `NodeAddressState`, `NodeLoggingState`) encapsulated behind properties with thread-safe mutators, `TestExecutor` disposes its semaphore, and the node identity file reads use `nameof`
- The `tls_cipher` metric now reports the negotiated TLS cipher suite (e.g. `Tls13Aes128GcmSha256`) instead of the legacy `SslStream.CipherAlgorithm` value, which is obsolete and returns `None` on TLS 1.3
- The node's console log format moved from code to `appsettings.json`: the `Serilog:ConsoleSink` section defines the sink's level, output template, and invariant culture, read through `Serilog.Settings.Configuration`; the mute wrapper stays in code because the local-logging policy is runtime state, and a missing or empty section falls back to the built-in default (same template, level, and culture)

## [Server 0.3.0] - 2026-10-01

### Server
- New `obicon.tests.queue_jobs` gauge on `/metrics`: current test job count per status, so the queue state can be tracked in Prometheus over time
- New `obicon.tests.current_result` gauge on `/metrics`: the latest job status of every created test (one series per test, `-1` when it never ran), and `obicon.server.build_info` exposes the server version as a label
- Grafana dashboards ship in `observability/`: one for node health, one for tests, and one for the server, each with filter variables (node, test type, test, log level)
- Deleting a node now closes its live WebSocket connection: a deleted node no longer keeps heartbeating as a connected ghost that is absent from the node list; a background connection watcher also sweeps every 30 seconds for connections whose node record no longer exists
- New settings controlling the test metrics: `TestMetricsEnabled` (default on) gates whether finished runs are exported on `/metrics`, and `TestMetricsIncludeNodeLabels` (default on) attaches the executing node's labels as the comma-separated `node_labels` label on `obicon.tests.runs` and `obicon.tests.duration_ms`
- Grafana tests dashboard: a `Tests` table listing every created test with its latest result, 24h run count, success rate, and average duration — clicking a test filters the whole dashboard to it — plus a per-node runs table and a node-labels filter variable
- `GET /v1/server/stats` now reports the server's own `version`, shown on the Server page as a link to the matching GitHub release
- The nodes API reports `versionSupported` per node: the server's own verdict (same major.minor, as checked on the node's connection) on the reported version; null when the node never connected
- Deleting a node now also removes it from every pool it belongs to, so a pool no longer carries a stale node ID that would produce jobs for a missing node

### Frontend
- Nodes page: decluttered rows (name, version, state, one representative IP, actions) with everything else — addresses per family, capacity, enrollment, labels, pools, reported settings — in a new read-only details modal
- A warning icon marks nodes the server reports as version-mismatched (`versionSupported: false`), with the explanation as its tooltip and in the details modal
- Server page: the current server version links to its GitHub release

## [Node 0.3.0] - 2026-10-01

- No functional changes; version bump only, so nodes stay within the server's supported version range (same major.minor as the server they connect to — server 0.3.0 disconnects 0.2.x nodes unless `AllowUnsupportedNodeVersions` is enabled)

## [Server 0.2.0] - 2026-09-30

### Server
- Version compatibility gate: the server announces its version to connecting nodes and disconnects nodes outside the supported range (same major.minor), unless the new `AllowUnsupportedNodeVersions` setting is enabled
- Nodes report their software version on registration and the server stores their connection IP; nodes also self-report their internal (LAN) and external (public) addresses, refreshed on an interval — all exposed by the nodes API and shown on the nodes page
- Nodes also report their operating settings (max concurrent tests, heartbeat interval, default and max test timeouts, reconnect delay); the nodes API exposes them and the UI shows a capacity column plus the full settings in the node edit modal
- Node pools have a description, settable at creation and editable from the UI
- Enroll tokens can be scoped to a pool: nodes enrolling with a scoped token are always added to that pool; unscoped tokens keep letting nodes choose their own pools
- HTTP/HTTPS test enhancements: response body regex expectations, custom headers, proxy support, and cache busting — validated server-side, executed by the node
- Observability settings section with node log controls: `NodeLogShippingEnabled` (accept shipped node log entries), `NodeLocalLoggingEnabled` (default policy for node-side logging, overridable per node), and `ShipNodeLogsToConsole` (forward received entries to the server console); the policy is announced to nodes in the server hello and changes are pushed live to connected nodes as `ServerPolicyUpdate` messages; shipped entries arrive over a new `NodeLog` WebSocket message, with full structured metadata (source context, scope properties, exceptions)
- Run-once (`POST /v1/tests/run-once`) accepts nodes and/or pools: explicit nodes always run, and each pool contributes its top 3 connected members, least busy first; validation errors precede connectivity checks
- SQLite writes are serialized through a write queue in the data layer: mutating operations are enqueued as read-modify-write units and executed one by one by a single background consumer, while reads stay direct; failures still propagate to the API
- Every API endpoint documents its response types and status codes in Swagger (`ProducesResponseType`), so the UI shows what to expect

### Frontend
- Nodes page: version and IP columns
- Pools: description field on creation and an edit modal for name and description
- Tests: body regex, headers, proxy, and cache busting fields on create, edit, and dry runs
- Dry runs execute on all selected nodes at once (random single-node fallback when nothing is selected), with per-node results
- Settings: pool scope selector when creating enroll tokens, plus a scope column in the token table

## [Node 0.2.0] - 2026-09-30

- Reports its version on registration; logs the server version on every connection or reconnection, plus a notice whenever it changes; disconnects from servers outside its supported range (same major.minor) unless `Node:AllowUnsupportedServerVersion` is enabled
- Reports its operating settings (max concurrent tests, heartbeat interval, default and max test timeouts, reconnect delay) on registration, so the server can show what each node can do
- Log shipping: a capture sink in the Serilog pipeline queues every log event and a background shipper sends the entries to the server as `NodeLog` messages with full metadata (node name, version, timestamp, level, message, exception, structured properties). Controlled by the server's observability settings, with node-side overrides (`Node:LogShippingEnabled`, `Node:LogShippingMinLevel`, `Node:LocalLoggingEnabled`); a server-side local-logging policy mutes only test-related output on the node's console (lifecycle logs stay visible) while shipping continues; every policy change is logged before it takes effect
- Policy changes propagate live: `ServerPolicyUpdate` messages from the server apply new shipping and local-logging settings on the fly, without reconnecting; the node's local override still wins
- Address reporting: the node resolves its internal (LAN) IPv4 and IPv6 addresses and its external (public) IPv4 and IPv6 addresses via configurable check services on an interval, logs changes, and pushes them to the server as `NodeInfoUpdate` messages; unavailable families are reported as such and current values also travel with every registration
- HTTP/HTTPS test enhancements: body regex checks (with a 1-second match timeout), custom request headers, proxy support, and cache busting via a unique query parameter
- TLS required by default (`Node:RequireTls`): plain `ws://` connections are refused unless the host is loopback or the setting is overridden

## [Server 0.1.0] - 2026-09-29

Initial release.

### Server
- ASP.NET Core API (`/v1`) with Swagger and Serilog logging
- SQLite persistence (EF Core) with automatic schema and data migration on upgrade; no need to delete `obicon.db`
- Node management: create and edit nodes, labels, auth tokens regenerated on demand (tokens stored as SHA-256 hashes)
- Node pools; tests target nodes and/or pools, membership visible from both sides
- Node activity view: connected, active, and inactive nodes
- Tests: Ping, Traceroute, HTTP, HTTPS, TCP, DNS with per-test timeout, IP version selection, expected status codes, TLS certificate expiry checks, and DNS result expectations; enable/disable without deleting
- Configurable frequency presets (`FrequencyPresetsSeconds`); the scheduler loop runs at the lowest preset, reported by the read-only `SchedulerLoopIntervalSeconds` setting
- Test queue with NoRun detection (never acknowledged, never started, node offline) and timeout reaping
- Server settings: forced-by-configuration, database overrides, and read-only derived settings, each with a description
- Node auto-enrollment via expiring enroll tokens (hashed at rest), manageable from the settings page
- OpenTelemetry Prometheus metrics on `/metrics` (per-test results with node labels, server actions, NoRuns, received node log entries with level and source); native ASP.NET Core health checks
- WebSocket hub for nodes on `/ws/nodes` with heartbeat-based connection tracking

### Frontend
- Static Bootstrap 5 UI: dashboard, nodes, pools, tests (create/edit with live check estimates and dry runs), queue, server stats, settings

## [Node 0.1.0] - 2026-09-29

Initial release.

- Console node connecting over WebSocket; runs all test types with verbose per-run results (DNS resolver details, HTTP/TLS timing breakdowns)
- Configurable Prometheus metrics endpoint via `Node:MetricsHost` (default `localhost`, `+` exposes outside the machine) and `Node:MetricsPort` (default 9464), plus a health endpoint
- Configuration through appsettings or `Node__*` environment variables
