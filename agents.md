# Agent Guidelines for Obicon Project

## Project References

- **Goal & architecture:** See `/Project/Obicon.md` for what the app is and how the parts fit together
- **Todo list:** See `/Project/todo.md` for current task tracking — keep it up to date as work lands

Note: the `/Project` folder is **local-only** (gitignored). In a fresh clone it won't exist — recreate it with a `todo.md` if you need task tracking.
- **API spec:** See `/docs/api-spec.md` — update it whenever an endpoint, DTO, or message changes

## User Documentation

`/docs` hosts the user-facing documentation and will eventually be served as a docs page:

- `index.md` (overview), `getting-started.md` (published-image user path), `contributing.md` (build and tests), `server.md`, `node.md`, `metrics.md`, and the full `api-spec.md`
- Keep these pages **light and basic** — setup, configuration, and pointers; deep internals belong in `/Project` docs or the API spec, not duplicated there
- Update them whenever user-facing behavior changes: ports, settings, env vars, endpoints, Docker usage. Like the API spec, a docs page that lags the code is a bug
- The docs are plain Markdown with relative links between pages, so any static site generator (e.g. MkDocs) can host them without changes

## Build and Run

- **Build everything:** `dotnet build Obicon.slnx`
- **Tests:** `dotnet test tests/Obicon.Server.Tests` (xUnit with `WebApplicationFactory<Program>` integration tests — each factory instance gets its own database on a shared Testcontainers PostgreSQL, so a running Docker engine is required — plus unit tests) and `dotnet test tests/Obicon.Node.Tests` (unit tests for the node: log capture sink, logging policy, identity store). Add a test for every security-relevant behavior (e.g. enrollment disabled, forced settings, token expiry)
- **Logs in the test console:** server test classes boot the app through `ObiconServerFactory`, so derive them from `LoggedTest` (`XunitLogging.cs`) — the in-memory server's logs then go to the running test's output, shown for failed tests and with `--logger "console;verbosity=detailed"`. Node tests pass `TestOutputLogger` to any service taking an `ILogger` to get its logs in the test output. A failed integration stack start includes the server and node container logs in the failure message
- **Integration tests:** `dotnet test tests/Obicon.Integration.Tests` — the full stack in Docker via Testcontainers: it builds the server and node images from the repo Dockerfiles, boots the server and an auto-enrolled node on a shared docker network, and drives them through the real API and WebSocket (HTTP/ping/DNS tests against the server container, metrics scrape). Needs a running Docker engine (Docker Desktop works with no extra configuration); CI runs it as its own `integration-tests` job
- **Server:** `dotnet run --project src/Obicon.Server` → http://localhost:5000, Swagger at `/swagger`
  - API auth: header `Authorization: <ServerSettings:AuthHeader>` (default `secureobiconkey`; the Swagger description shows the configured value and the web UI stores its key in localStorage, prompted on the first 401). `/ws`, `/metrics`, and `/swagger` are exempt (WebSocket authenticates with the node token instead)
  - Data: PostgreSQL (connection string `ConnectionStrings:Default`, overridable as `ConnectionStrings__Default`); a local stack is `docker compose up -d`. EF Core migrations run on startup (`Data/Migrations`, design-time factory in `Data/DesignTimeDbContextFactory.cs`, generate new ones with `dotnet ef migrations add <Name> --project src/Obicon.Server`). Finished test results respect the `TestResultStorageMode` setting and the `JobRetentionDays` retention window — long-term history belongs to the user's metric store, not the database
- **Node:** `Node__Token="<token>" dotnet run --project src/Obicon.Node`
  - Every setting in `appsettings.json` (`Node` section) can be overridden by env vars: `Node__ServerUrl`, `Node__MaxConcurrentTests`, etc.
  - Health endpoint: `http://localhost:8080/health` (HttpListener, not Kestrel)
  - On Linux, ping/traceroute need raw-socket privileges (`cap_net_raw`) — without them these tests fail with a clear error, which is expected
- **Client (frontend):** `dotnet run --project src/Obicon.Client --urls http://localhost:5003` — plain static files from `wwwroot`, no build step; a browser refresh picks up changes
  - Shared chrome lives in `js/layout.js`: every page has empty `<div id="appNavbar"></div>` and `<div id="appFooter"></div>` placeholders that it fills (nav links + active state). Do not copy the navbar into pages
  - Styling is Bootstrap 5.3.8 via CDN (there is no 5.4.8 release) plus Bootstrap Icons and `css/styles.css`; page JS references elements by `id`, so keep ids stable when editing markup

## Project Layout

- `src/Obicon.Shared` — the wire contract shared by server and node: `WebSocketMessage`, message types (`TestAssignmentMessage`, `TestResultMessage`, ...), `TestType`, `TestJobStatus`. Anything both sides serialize belongs here, not in the server
- `src/Obicon.Server` — ASP.NET Core API (`/v1`), WebSocket host (`/ws/nodes`), PostgreSQL persistence (EF Core, `Data/ObiconDbContext.cs`), and `TestQueueProcessor` which dispatches queued jobs to connected nodes
- `src/Obicon.Node` — .NET console app (generic host): `ServerConnection` (dedicated comm task: register, heartbeat, reconnect), `TestExecutor` (max concurrency, `[timeout]+5s` hard kill), test runners, `HealthService`, `MonitoringService`
- `src/Obicon.Client` — static frontend; `js/api.js` is the shared API helper (already handles 204 and the auth header)

## CI, Releases, and Docker

- **Pipelines:** `.github/workflows/ci.yml` (pull requests to `main`: build + test, default read-only permissions) and `.github/workflows/release.yml` (pushes to `main`: build + test, then publish the server and node images to the GitHub Container Registry, `ghcr.io/enessene/obicon/server` and `/node` — each as `<version>` and `latest`, versions derived from the CHANGELOG headings — then tag and release). The frontend is not published as an image (run it with `dotnet run --project src/Obicon.Client`)
- **Versions are per component:** `CHANGELOG.md` tracks `## [Server x.y.z]` and `## [Node x.y.z]` headings independently — bump only the component that changed. The pipeline publishes each image with its own version and creates `server-vx.y.z` / `node-vx.y.z` tags and releases, with the matching changelog section as notes. Frontend changes are listed under the server release. The publish job reuses images for an existing tag but skips re-releasing
- **Dockerfiles:** `src/Obicon.Server/Dockerfile` (aspnet:10.0, port 5000, configured via `ConnectionStrings__Default` to point at a PostgreSQL instance) and `src/Obicon.Node/Dockerfile` (runtime:10.0, configured via `Node__*` env vars; needs `--cap-add=NET_RAW` for ping/traceroute, and `Node__MetricsHost=+` to expose metrics). Both build from the repo root as context with `.dockerignore` keeping it small

## Server Settings Standard

Settings live in three layers, resolved in this order:

1. **Forced** — present in the `ServerSettings` section of appsettings or as `ServerSettings__*` environment variables (`Configure<ServerSettings>` binds them). Read-only; the API returns 409 and the UI shows a "Forced by configuration" badge
2. **Database overrides** — set via `PUT /v1/settings/{key}`, stored in the `ServerSettingValues` table
3. **Defaults** — from `ServerSettingDefinitions.All`

Rules:

- **Every setting must have a description** in `Configuration/ServerSettingDefinitions.cs` — it is shown in the settings UI and API. A setting without a description is incomplete work
- **Setting values are typed, never serialized strings:** a setting holding multiple values is a `List<string>`/`List<int>` (`ValueType` + typed `Default`), read through `GetAsync<List<T>>`, and travels as a native JSON array in the API and UI — never a JSON-encoded string property. Scalar settings are typed too: `bool` and `int` defaults are native values and arrive as native JSON (PUT also accepts plain strings for scalars). The settings service canonicalizes and validates per type; a new setting gets its type here, not ad-hoc parsing at the consumer
- When adding a setting: add it to `ServerSettings` (config binding + property doc comment) AND to `ServerSettingDefinitions.All` (description, type, default). Consumers read values through `IServerSettingsService.GetAsync<T>(key)`, never directly from `IOptions`, so database overrides take effect
- Read-only (derived) settings set `IsReadOnly = true` in `ServerSettingDefinitions.All` and get their value computed in `ServerSettingsService.ComputeReadOnly` — they are NOT in `ServerSettings`, cannot be stored or forced, and `PUT` returns 409. Example: `SchedulerLoopIntervalSeconds`, the `TestScheduler` loop interval, equals the lowest `FrequencyPresetsSeconds` preset (the loop sleeps between scans, so the CPU cost is one short DB query per wake)
- The settings service caches effective values; the cache is invalidated on change
- **Test frequencies are plain seconds** (`Test.Frequency`, no enum). The allowed values come from the `FrequencyPresetsSeconds` setting (default `10,30,60,120,300,600,3600`); `TestService` validates create/update against it and the tests page loads its dropdowns from `/v1/settings`. The scheduler treats the value directly as the interval in seconds
- Enroll tokens are stored as SHA-256 hashes only; the plain value is returned exactly once at creation. **Node auth tokens follow the same rule**: `Node.AuthToken` holds a hash, lookups hash the presented token (`TokenHasher`), and the plain value only appears in the response of creation, regeneration, or enrollment — never in list endpoints. Node identity (`node-identity.json`) contains the node auth token and is gitignored

## Logging Standard

Logging calls are written as `[LoggerMessage]` partial methods (CA1848): make the containing class `partial`, add the private partial method with `[LoggerMessage(Level = ..., Message = "...")]` at the bottom of the class, and call it instead of the `ILogger` extension. Top-level statements log through the project's `ProgramLog` class

The node's console sink is configured in `appsettings.json` (`Serilog:ConsoleSink`: level, template, invariant culture) rather than in code — change it there, not in `Program.cs`. The sink is attached through a `WriteTo.Conditional` wrapper in `Program.cs` because the local-logging mute policy is runtime state, and a missing or empty section falls back to the built-in default mirrored in code; the capture sink (`NodeLogSink`) always sees the full Debug pipeline

**Every basic action gets an `LogInformation` entry in the service that performs it** — created/updated/deleted for nodes, pools, and tests; token regeneration; test runs triggered; jobs enqueued, dispatched, and finished; connections opened and closed. Someone tailing the log should see the full lifecycle without debug logging enabled.

- Log **what** and **identify it**: `"Created pool {PoolId} with name {PoolName}"`, `"Job {JobId} finished on node {NodeId}: success={Success} duration={DurationMs}ms"`
- Put lifecycle logs in **services**, not controllers; HTTP request lifecycle lines come from ASP.NET already
- Failure paths log at **Warning** (expected: not found, bad input) or **Error** (unexpected exceptions)
- High-frequency chatter (heartbeats, queue polls) logs at **Debug**, not Information
- Cross-reference: meaningful server actions also increment the `Obicon.Server` metrics counter (`ServerMetrics.Action("created_pool")`), finished test runs go to `ServerMetrics.TestRun(...)`

## Conventions

- **C# Coding Guidelines:** we adhere to [csharpcodingguidelines.com](https://csharpcodingguidelines.com/). Enforcement lives in the root `.editorconfig` (naming, style, and formatting rules) plus the recommended .NET analyzer rules (`Directory.Build.props`); every rule fires as a build warning. The `coding-guidelines` CI job builds with `-warnaserror`, so any violation fails the pipeline. The codebase is clean: keep it that way — when writing or touching C# code, do not add new violations. Suppressions (`#pragma warning disable` or `[SuppressMessage]`) require a justification naming why the rule does not apply. Test method names keep the xUnit underscore convention (CA1707 is scoped off under `tests/`); private fields are `_camelCase`

- **Database writes run as units of work** (`Data/ObiconDbContextFactoryExtensions`): every mutating operation is a read-modify-write unit executed against its own short-lived context — PostgreSQL allows concurrent writers, so there is no queue. Rules:
  - New write paths go through `_dbFactory.ExecuteAsync(async db => ...)` and must do their whole read-modify-write inside the unit — entities never cross the context boundary
  - Code that already holds a context calls helpers that take the unit's `db` instead of opening another (e.g. `TestQueueService.CreateJobAsync(db, job)`)
  - Failures propagate to the caller, so controllers keep their 400/404 behavior
- **One class per file:** a file holds at most one top-level class (or record), named after it - `PoolMember.cs` contains `PoolMember`. Nested private classes are fine; grouping several related types into one file is not. Request and response DTOs live in `Models/Requests/` and `Models/Responses/`, not beside the controller
- **JSON casing differs by channel, on purpose:** HTTP API responses are camelCase (ASP.NET default); WebSocket payloads are PascalCase (`System.Text.Json` defaults + explicit `[JsonPropertyName]`). Keep both as they are — the node and frontend depend on them
- **Validation:** all request DTOs use DataAnnotations → automatic 400 ProblemDetails naming the field. Add attributes for every new required/range-checked field
- **Responses:** never return EF entities directly; map through DTOs in `Models/Responses/` (`TestJobResponse.From(job)` pattern)
- **Frontend cache busting:** bump `?v=N` on `<script>` tags whenever a JS file changes — browsers cache them
- **Metrics:** two server meters (`Obicon.Server` for lifecycle actions, `Obicon.Tests` for run counts/durations) exported at `GET /metrics` via the OpenTelemetry Prometheus exporter and, when `Otlp:Endpoint` is configured, via OTLP (metrics and the node-log funnel share that endpoint); the node exports its `Obicon.Node` meter on `http://localhost:9464/metrics`. Add new actions to the existing counters, don't create new meters. The per-run labels come from the `TestMetricsLabels` setting (`test_id` and `status` are a forced floor) — `job_id` must never become a metric label; it is the join key into the logs
- **Timestamps:** all persistence uses UTC; `ObiconDbContext` marks every persisted datetime as UTC on write and read so they serialize with `Z`
- **DI cycle warning:** `ServerConnection` and `TestExecutor` mutually reference each other; the executor resolves `IServerConnection` lazily. Keep it that way when touching constructors

## Database Standard

The schema is plain, fully relational PostgreSQL, designed to be queried by hand:

- **Snake_case everywhere.** Every table, column, and constraint name is snake_case (`tests`, `node_id`, `created_at`, `FK_pool_members_nodes_node_id`). `ObiconDbContext.OnModelCreating` applies the conversion to every entity at the end of model building — new entities get it for free; never hand-name a table or column in PascalCase, and never add a quoted identifier to a query. The goal: `select * from tests`, never `select * from "Tests"`
- **Never use `jsonb` (or JSON-in-text) for any column.** Relationships are junction tables (`test_target_nodes`, `test_target_pools`, `pool_members`), dictionaries are key-value rows (`node_labels`, `node_reported_settings`, `test_headers`, `test_job_headers`), and a finished run's structured details are normalized into one table per test-type section (`test_job_traceroute_details` + `test_job_traceroute_hops` + `test_job_traceroute_probes`, `test_job_ping_details` + `test_job_ping_replies`, `test_job_tcp_details`, `test_job_http_details`, `test_job_dns_details` + `test_job_dns_records`, `test_job_tls_details`, `test_job_certificates`). The only list-typed columns are native `text[]` for plain string lists (DNS nameservers/resolved addresses, certificate SANs). New structured data gets its own tables, mapped through `TestResultDetailsMapper` or a sibling mapper
- **Join rows cascade.** Every junction and child row has a cascading foreign key to its owner, so deleting a node/pool/test/job leaves no stale references (this is what keeps deleted nodes from queueing jobs)
- **Index the hot paths:** every column a recurring query filters or sorts on gets an index in `ObiconDbContext.OnModelCreating` - token lookups (`nodes.auth_token`, `enroll_tokens.token_hash`), queue listing and sweeps (`test_jobs.created_at`, `(status, completed_at)`), per-test latest result (`test_jobs.(test_id, completed_at)`), pool-by-name resolution (`node_pools.name`). Join-table foreign keys get theirs from EF automatically
- **While a release is unreleased, the schema lives in ONE migration:** every model change regenerates `InitialCreate` instead of stacking increments - delete `src/Obicon.Server/Data/Migrations`, then `dotnet ef migrations add InitialCreate --project src/Obicon.Server --output-dir Data/Migrations` (and drop any `using` the scaffold adds that the file does not need). Unreleased databases are dev databases; they get reset, not migrated. Incremental migrations start with the first released schema, and from then on never regenerate - add instead
- **Shape queries for PostgreSQL, not for EF:** loading more than one collection navigation runs as `AsSplitQuery()` — a single-query include over many joined tables produces a plan cost in the millions (cartesian row estimates), which crosses PostgreSQL's JIT threshold and costs hundreds of milliseconds of compilation per execution. Read-only loads use `AsNoTracking()`; write paths (`DequeueTestAsync`-style transitions) load only what they change and keep tracking. List endpoints are paginated and filtered server-side (see `GetRunsAsync`) — never load an unbounded window into memory
- **Timestamps are UTC everywhere:** the context marks every persisted `DateTime` as UTC on write and read; never store or pass local time
- **API casing is unaffected:** HTTP responses stay camelCase and WebSocket payloads stay PascalCase — the snake_case rule is strictly database identifiers

## Model Documentation Standard

**Every model field must have a documentation comment** explaining its purpose in max 2 sentences. 

For complex objects (nested types, collections, enums), also include a default value where applicable.

### Example Format

```csharp
/// <summary>
/// Unique identifier for the node. Generated automatically on creation.
/// </summary>
public Guid Id { get; set; }

/// <summary>
/// Human-readable name of the node. Default: empty string.
/// </summary>
public string Name { get; set; } = string.Empty;

/// <summary>
/// Authentication token for WebSocket connections. 
/// Generated as GUID on node creation. Default: empty string.
/// </summary>
public string AuthToken { get; set; } = string.Empty;
```

### Documentation Rules

1. **All public properties** must have `<summary>` documentation
2. **Max 2 sentences** per field - be concise
3. **Complex types** (collections, custom objects, enums): Include default value if applicable
4. **Enums**: Document each value with `<summary>`
5. **DTOs**: Same rules apply - describe what each field represents

### Where This Applies

- All model classes in `Obicon.Server/Models/`
- All DTO classes in `Obicon.Server/Models/Requests/` and `Obicon.Server/Models/Responses/`
- All shared models in `Obicon.Shared/Models/`
- All node models and configuration classes in `Obicon.Node/Models/` and `Obicon.Node/Configuration/`

### Additional Notes

- Use `/// <summary>` XML documentation format
- Place comment **above** the property/field
- No need to document private fields
- For enums: document the enum type and each value
