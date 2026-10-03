# Changelog

All notable changes to Obicon are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com), and each component uses
[semantic versioning](https://semver.org) independently.

Versions are tracked per component: `## [Server x.y.z]` and `## [Node x.y.z]`.
The CI pipeline publishes `ghcr.io/<owner>/<repo>/server:<server version>` and
`.../node:<node version>`, and creates the tags/releases `server-vx.y.z` and
`node-vx.y.z` with the matching section below as notes. The frontend has no
separate version; its changes are listed under the server release.

## [Server 0.4.0] - Unreleased

### Server
- Test results now carry structured details instead of the old flat stringly metrics dictionary: nodes report one typed section per test type (traceroute hops with address/status/RTT each, HTTP phase timings with certificate, DNS record lists), the server persists them on the job, and the queue API exposes them as `details`. Breaking: results stored by earlier versions keep their text output but no longer show their metrics

- Traceroute tests are configurable per test: maximum hops (default 30), probes per hop (default 3), per-probe timeout in ms (default 2000), and reverse hostname resolution per hop (default on) — settable on create, edit, and dry runs, validated server-side (1-64 hops, 1-10 probes, 100-60000 ms)

### Frontend
- Traceroute settings fields on test create, edit, and dry runs; the hop table shows each probe's round trip and the resolved hostname
- Queue page and dry-run results render the structured details per test type — a hop table for traceroutes, phase timings and the certificate for HTTP(S), record lists for DNS — older results show only the text output

## [Node 0.4.0] - Unreleased

- Traceroute runs honor the new per-test settings — hop limit, probes per hop, per-probe timeout, and best-effort reverse hostname resolution per hop — and each hop's details now carry every probe's round trip and the resolved hostname
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
