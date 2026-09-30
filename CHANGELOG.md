# Changelog

All notable changes to Obicon are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com), and each component uses
[semantic versioning](https://semver.org) independently.

Versions are tracked per component: `## [Server x.y.z]` and `## [Node x.y.z]`.
The CI pipeline publishes `ghcr.io/<owner>/<repo>/server:<server version>` and
`.../node:<node version>`, and creates the tags/releases `server-vx.y.z` and
`node-vx.y.z` with the matching section below as notes. The frontend has no
separate version; its changes are listed under the server release.

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

- Reports its version on registration; logs the server version on startup and whenever it changes; disconnects from servers outside its supported range (same major.minor) unless `Node:AllowUnsupportedServerVersion` is enabled
- Reports its operating settings (max concurrent tests, heartbeat interval, default and max test timeouts, reconnect delay) on registration, so the server can show what each node can do
- Log shipping: a capture sink in the Serilog pipeline queues every log event and a background shipper sends the entries to the server as `NodeLog` messages with full metadata (node name, version, timestamp, level, message, exception, structured properties). Controlled by the server's observability settings, with node-side overrides (`Node:LogShippingEnabled`, `Node:LogShippingMinLevel`, `Node:LocalLoggingEnabled`); a server-side local-logging policy mutes only test-related output on the node's console (lifecycle logs stay visible) while shipping continues; every policy change is logged before it takes effect
- Policy changes propagate live: `ServerPolicyUpdate` messages from the server apply new shipping and local-logging settings on the fly, without reconnecting; the node's local override still wins
- Address reporting: the node resolves its internal (LAN) IPv4 and IPv6 addresses and its external (public) IPv4 and IPv6 addresses via configurable check services on an interval, logs changes, and pushes them to the server as `NodeInfoUpdate` messages; unavailable families are reported as such and current values also travel with every registration
- HTTP/HTTPS test enhancements: body regex checks (with a 1-second match timeout), custom request headers, proxy support, and cache busting via a unique query parameter

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
