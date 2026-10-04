# Server

The Obicon server is the central piece: it stores nodes, tests, and jobs; schedules test runs; dispatches them to connected nodes over WebSockets; and serves the REST API, health checks, and Prometheus metrics.

## Running

```bash
docker compose up -d   # server + PostgreSQL, from the repo root
```

or from source (needs a reachable PostgreSQL — the standalone database compose in [`test/`](../test/) provides one matching the default connection string):

```bash
docker compose -f test/docker-compose.yml up -d
dotnet run --project src/Obicon.Server
```

| What | Where |
|------|-------|
| REST API | http://localhost:5000 (all routes under `/v1`) |
| Swagger | http://localhost:5000/swagger |
| Health check | `GET /v1/health` |
| Prometheus metrics | `GET /metrics` (no auth) |
| WebSocket hub | `ws://localhost:5000/ws/nodes` (nodes connect here) |

In Docker, the published image is `ghcr.io/enessene/obicon/server` (port 5000; tags `latest` and the per-release version, e.g. `0.4.0`). The repo root's `docker-compose.yml` runs the server next to a PostgreSQL container.

## Storage

All data lives in PostgreSQL (default connection string `Host=localhost;Port=5432;Database=obicon;Username=obicon;Password=obicon`, override with `ConnectionStrings__Default`; the compose in [`test/`](../test/) starts a database with exactly those credentials). EF Core migrations run automatically on startup - a fresh database is created, and an existing one is brought up to the current schema.

The database is the system of record for configuration (nodes, pools, tests, settings, enroll tokens) and for a **bounded window of finished test results**:

- The `TestResultStorageMode` setting decides what a finished job leaves behind: `Full` (default, complete payload), `MetadataOnly` (row without the payload), or `None` (row deleted on completion).
- The `JobRetentionDays` setting (default 30) bounds even that: a sweep deletes finished jobs older than the window, no exceptions. There is no archive table and no soft delete.

The contract is deliberate: **the database is for operating the system and debugging the last weeks; history lives in your observability backend** (see [Metrics](metrics.md)). Since 0.5.0 the server uses PostgreSQL instead of SQLite, with no upgrade path from an existing `obicon.db` - back the old file up before switching, or export what you need.

## Authentication

API requests must carry the auth header (default: `Authorization: secureobiconkey`). `/ws`, `/metrics`, and `/swagger` are exempt; the WebSocket endpoint instead authenticates nodes by their token, and `/v1/enroll` authenticates by enroll token.

The header value and the WebSocket path are configurable (see below). The frontend's `js/api.js` already sends the header for every call.

## Configuration

Server configuration lives in `appsettings.json` (override with environment variables):

| Key | Default | Purpose |
|-----|---------|---------|
| `ConnectionStrings:Default` | `Host=localhost;Port=5432;Database=obicon;Username=obicon;Password=obicon` | PostgreSQL connection string |
| `Otlp:Endpoint` | *(empty)* | OTLP endpoint (e.g. `http://collector:4317`); set it to push metrics **and** node logs to your observability backend. Empty keeps the scrape-only behavior |
| `ServerSettings:AuthHeader` | `secureobiconkey` | Required API auth header value |
| `ServerSettings:MaxTestTimeoutSeconds` | `60` | Upper bound for test timeouts |
| `ServerSettings:NodeConnectionTimeoutSeconds` | `30` | Node WebSocket connection timeout |
| `ServerSettings:WebSocketPath` | `/ws/nodes` | Path nodes connect to |

The `Serilog` section configures logging: `MinimumLevel` (per-source overrides) and the `WriteTo` console sink, which uses the invariant culture for all value formatting. When `WriteTo` defines no sinks, a built-in default console sink (same culture, standard template) takes over.

## Server settings (runtime, editable)

Separate from the static config above, the server has runtime settings editable from the UI's Settings page or `PUT /v1/settings/{key}`. They resolve in three layers:

1. **Forced** — set via `ServerSettings__*` env vars or appsettings; read-only, the UI shows a "Forced by configuration" badge
2. **Database override** — set via the API/UI, stored in the database
3. **Default** — built-in defaults

Useful settings include `FrequencyPresetsSeconds` (the intervals tests can choose, default `10,30,60,120,300,600,3600`), `NodeAutoEnrollmentEnabled` (lets nodes register themselves with enroll tokens), `NoRunGraceFactor`, and `AllowUnsupportedNodeVersions` (accept nodes outside the supported version range instead of disconnecting them). `SchedulerLoopIntervalSeconds` is read-only and derived from the lowest frequency preset.

Two settings in the **General** group control what test results the database keeps:

| Setting | Default | Meaning |
|---------|---------|---------|
| `TestResultStorageMode` | `Full` | What a finished job leaves behind: `Full` (complete payload), `MetadataOnly` (row without the payload), or `None` (row deleted on completion). Metrics are always emitted before deletion |
| `JobRetentionDays` | `30` | Days a finished job stays in the database before the sweep deletes it. Want history? Check your metric store |

The settings UI groups settings into sections; the **Observability** section controls node log handling:

| Setting | Default | Meaning |
|---------|---------|---------|
| `NodeLogShippingEnabled` | `false` | Nodes may ship their log entries to the server; while off, shipped entries are dropped |
| `NodeLocalLoggingEnabled` | `true` | The server's default policy for the node's **test-related** console output (assignments, execution, monitoring); a node's own configuration takes precedence |
| `NodeExternalIpResolvingEnabled` | `false` | Nodes may resolve their external (public) addresses via the configured check services; while off, nodes report them as unavailable and contact no check service |
| `ShipNodeLogsToConsole` | `false` | Log entries received from nodes are written to the server's own console, tagged with the node's identity |
| `TestMetricsEnabled` | `true` | Finished test runs are exported on `/metrics` (and via OTLP) as `obicon.tests.runs` and `obicon.tests.duration_ms`; when off, new runs are not recorded (already exported series persist until restart) |
| `TestMetricsLabels` | `["test_type","test_name","node_name","node_labels"]` | JSON array choosing which labels ride along on the test metrics. `test_id` and the counter's `status` are always attached; `node_name` is the human-readable node dimension, `node_id` the rename-stable opt-in, `node_labels` the churniest. Changing the set starts new series for subsequent runs |

The policy is announced to every node in the server hello message. Changing `NodeLogShippingEnabled` or `NodeLocalLoggingEnabled` through the API or UI pushes a `ServerPolicyUpdate` to all connected nodes immediately — they apply it on the fly, without reconnecting. A node's `Node:LocalLoggingEnabled` override always wins over the server's local logging default.

Accepted node log entries are also forwarded into the server's OpenTelemetry logging pipeline: with `Otlp:Endpoint` configured, each entry egresses through the OTLP logs exporter carrying the node's identity and the entry's properties (`node_id`, `node_name`, `node_version`, `source_context`, `job_id`, `test_id`) as first-class fields — the same keys as the test metrics, so logs and metrics can be joined in one Grafana dashboard. `ShipNodeLogsToConsole` remains the separate console echo.

## Version compatibility

The server announces its version to every node when it connects, and nodes report theirs. A node is supported when its version shares the server's major and minor version (e.g. node 0.2.x with server 0.2.y). Unsupported nodes are disconnected with a policy-violation close — enable the `AllowUnsupportedNodeVersions` setting to accept them with a warning instead. The node has its own flag for the same purpose: `Node:AllowUnsupportedServerVersion`.

## Scheduling and the queue

Active tests are enqueued by the scheduler each time their frequency elapses. After server downtime an overdue test runs once and resynchronizes — no missed-run catch-up. The queue processor dispatches each job to its node; jobs whose node never acknowledges, never starts, or never comes online are marked `NoRun` after a grace window.

Details, including the full endpoint list and job status values, are in the [API specification](api-spec.md).
