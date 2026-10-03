# Server

The Obicon server is the central piece: it stores nodes, tests, and jobs; schedules test runs; dispatches them to connected nodes over WebSockets; and serves the REST API, health checks, and Prometheus metrics.

## Running

```bash
dotnet run --project src/Obicon.Server
```

| What | Where |
|------|-------|
| REST API | http://localhost:5000 (all routes under `/v1`) |
| Swagger | http://localhost:5000/swagger |
| Health check | `GET /v1/health` |
| Prometheus metrics | `GET /metrics` (no auth) |
| WebSocket hub | `ws://localhost:5000/ws/nodes` (nodes connect here) |

In Docker, the published image is `ghcr.io/<owner>/<repo>/server` (port 5000).

## Storage

All data lives in a SQLite file (`obicon.db`, in the working directory by default). The schema is created and migrated automatically on startup, including adding missing tables/columns and one-time data conversions — after an upgrade you keep your existing database. Only changing an existing column's type or name requires manual migration.

## Authentication

API requests must carry the auth header (default: `Authorization: uwu`). `/ws`, `/metrics`, and `/swagger` are exempt; the WebSocket endpoint instead authenticates nodes by their token, and `/v1/enroll` authenticates by enroll token.

The header value and the WebSocket path are configurable (see below). The frontend's `js/api.js` already sends the header for every call.

## Configuration

Server configuration lives in `appsettings.json` (override with `ServerSettings__*` environment variables):

| Key | Default | Purpose |
|-----|---------|---------|
| `ConnectionStrings:Default` | `Data Source=obicon.db` | SQLite connection string |
| `ServerSettings:AuthHeader` | `uwu` | Required API auth header value |
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

The settings UI groups settings into sections; the **Observability** section controls node log handling:

| Setting | Default | Meaning |
|---------|---------|---------|
| `NodeLogShippingEnabled` | `false` | Nodes may ship their log entries to the server; while off, shipped entries are dropped |
| `NodeLocalLoggingEnabled` | `true` | The server's default policy for the node's **test-related** console output (assignments, execution, monitoring); a node's own configuration takes precedence |
| `ShipNodeLogsToConsole` | `false` | Log entries received from nodes are written to the server's own console, tagged with the node's identity |
| `TestMetricsEnabled` | `true` | Finished test runs are exported on `/metrics` as `obicon.tests.runs` and `obicon.tests.duration_ms`; when off, new runs are not recorded (already exported series persist until restart) |
| `TestMetricsIncludeNodeLabels` | `true` | The executing node's labels are attached to the exported test metrics as the comma-separated `node_labels` label; changing it starts new series for subsequent runs |

The policy is announced to every node in the server hello message. Changing `NodeLogShippingEnabled` or `NodeLocalLoggingEnabled` through the API or UI pushes a `ServerPolicyUpdate` to all connected nodes immediately — they apply it on the fly, without reconnecting. A node's `Node:LocalLoggingEnabled` override always wins over the server's local logging default.

## Version compatibility

The server announces its version to every node when it connects, and nodes report theirs. A node is supported when its version shares the server's major and minor version (e.g. node 0.2.x with server 0.2.y). Unsupported nodes are disconnected with a policy-violation close — enable the `AllowUnsupportedNodeVersions` setting to accept them with a warning instead. The node has its own flag for the same purpose: `Node:AllowUnsupportedServerVersion`.

## Scheduling and the queue

Active tests are enqueued by the scheduler each time their frequency elapses. After server downtime an overdue test runs once and resynchronizes — no missed-run catch-up. The queue processor dispatches each job to its node; jobs whose node never acknowledges, never starts, or never comes online are marked `NoRun` after a grace window.

Details, including the full endpoint list and job status values, are in the [API specification](api-spec.md).
