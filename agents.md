# Agent Guidelines for Obicon Project

## Project References

- **Goal & architecture:** See `/Project/Obicon.md` for what the app is and how the parts fit together
- **Todo list:** See `/Project/todo.md` for current task tracking — keep it up to date as work lands
- **API spec:** See `/docs/api-spec.md` — update it whenever an endpoint, DTO, or message changes

## Build and Run

- **Build everything:** `dotnet build Obicon.slnx` (no test suite exists yet; verify with a build plus manual smoke tests)
- **Server:** `dotnet run --project src/Obicon.Server` → http://localhost:5000, Swagger at `/swagger`
  - API auth: header `Authorization: uwu`. `/ws`, `/metrics`, and `/swagger` are exempt (WebSocket authenticates with the node token instead)
  - Data: SQLite file `obicon.db` in the project directory, schema created on startup (no migrations — extend `ObiconDbContext` and delete the file or migrate manually)
- **Node:** `Node__Token="<token>" dotnet run --project src/Obicon.Node`
  - Every setting in `appsettings.json` (`Node` section) can be overridden by env vars: `Node__ServerUrl`, `Node__MaxConcurrentTests`, etc.
  - Health endpoint: `http://localhost:8080/health` (HttpListener, not Kestrel)
  - On Linux, ping/traceroute need raw-socket privileges (`cap_net_raw`) — without them these tests fail with a clear error, which is expected
- **Client (frontend):** `dotnet run --project src/Obicon.Client --urls http://localhost:5003` — plain static files from `wwwroot`, no build step; a browser refresh picks up changes

## Project Layout

- `src/Obicon.Shared` — the wire contract shared by server and node: `WebSocketMessage`, message types (`TestAssignmentMessage`, `TestResultMessage`, ...), `TestType`, `TestJobStatus`. Anything both sides serialize belongs here, not in the server
- `src/Obicon.Server` — ASP.NET Core API (`/v1`), WebSocket host (`/ws/nodes`), SQLite persistence (EF Core, `Data/ObiconDbContext.cs`), and `TestQueueProcessor` which dispatches queued jobs to connected nodes
- `src/Obicon.Node` — .NET console app (generic host): `ServerConnection` (dedicated comm task: register, heartbeat, reconnect), `TestExecutor` (max concurrency, `[timeout]+5s` hard kill), test runners, `HealthService`, `MonitoringService`
- `src/Obicon.Client` — static frontend; `js/api.js` is the shared API helper (already handles 204 and the auth header)

## Conventions

- **JSON casing differs by channel, on purpose:** HTTP API responses are camelCase (ASP.NET default); WebSocket payloads are PascalCase (`System.Text.Json` defaults + explicit `[JsonPropertyName]`). Keep both as they are — the node and frontend depend on them
- **Validation:** all request DTOs use DataAnnotations → automatic 400 ProblemDetails naming the field. Add attributes for every new required/range-checked field
- **Responses:** never return EF entities directly; map through DTOs in `Models/Responses/` (`TestJobResponse.From(job)` pattern)
- **Frontend cache busting:** bump `?v=N` on `<script>` tags whenever a JS file changes — browsers cache them
- **Timestamps:** all persistence uses UTC; `ObiconDbContext` re-marks SQLite datetimes as UTC on read so they serialize with `Z`
- **DI cycle warning:** `ServerConnection` and `TestExecutor` mutually reference each other; the executor resolves `IServerConnection` lazily. Keep it that way when touching constructors

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
