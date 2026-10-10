# Contributing

Building Obicon from source, running the test suites, and the conventions that keep the codebase clean.

## Building

```bash
dotnet build Obicon.slnx
```

## Running from source

The server needs a reachable PostgreSQL database. The easiest way is the standalone database compose in [`test/`](../test/), which matches the server's default connection string — no server configuration needed:

```bash
docker compose -f test/docker-compose.yml up -d
```

Then run the pieces:

```bash
# Server (API on http://localhost:5000)
dotnet run --project src/Obicon.Server

# Frontend (UI on http://localhost:5003)
dotnet run --project src/Obicon.Client --urls http://localhost:5003

# Node with a token created in the UI, or an enroll token
Node__Token="<token>" dotnet run --project src/Obicon.Node
```

Alternatively, use the full stack compose in the repo root (`docker compose up -d`), which runs the published server image next to its database. The user-facing setup path is [Getting started](getting-started.md).

## Tests

| Suite | Command | Needs Docker |
|-------|---------|--------------|
| Server — unit tests plus `WebApplicationFactory` integration tests, one database per factory on a shared Testcontainers PostgreSQL | `dotnet test tests/Obicon.Server.Tests` | Yes |
| Node — unit tests (log capture sink, logging policy, identity store) | `dotnet test tests/Obicon.Node.Tests` | No |
| Integration — the full stack in Docker: server and node images built from the repo Dockerfiles, driven through the real API and WebSocket | `dotnet test tests/Obicon.Integration.Tests` | Yes |

## Database conventions

The schema is plain, fully relational PostgreSQL, meant to be queried by hand:

- **Snake_case identifiers only** (`tests`, `node_id`, `created_at`) — every table, column, and constraint. No query ever needs a quoted identifier: `select * from tests`, not `select * from "Tests"`. The naming is applied centrally in `ObiconDbContext.OnModelCreating`, so new entities inherit it
- **No `jsonb`, ever.** Relationships are junction tables (`pool_members`, `test_target_nodes`, `test_target_pools`), dictionaries are key-value rows (`node_labels`, `test_headers`), and structured payloads are normalized into their own tables (see the `test_job_*_details` family). Only native `text[]` columns hold plain string lists
- **Cascading foreign keys** on every junction and child row, so deletes leave no stale references
- **Index the hot paths:** anything a recurring query filters or sorts on gets an index in `ObiconDbContext` (token lookups, queue listing, per-test latest result)
- **Query shape:** loads spanning multiple collection navigations use `AsSplitQuery()` (single-query includes over many joins produce huge plan costs that cross PostgreSQL's JIT threshold and slow every execution), read-only loads skip tracking, and list endpoints paginate server-side — never load an unbounded window
- **Timestamps are UTC** throughout
- **One migration while unreleased:** model changes regenerate the single `InitialCreate` instead of stacking increments (see agents.md for the exact procedure); incremental migrations begin with the first released schema

## Conventions

- The C# Coding Guidelines ([csharpcodingguidelines.com](https://csharpcodingguidelines.com)) are enforced by the root `.editorconfig` and analyzer rules; the `coding-guidelines` CI job verifies formatting and builds with warnings as errors — keep new code violation-free
- Keep `docs/` and the API spec in sync with user-facing behavior: ports, settings, env vars, endpoints. A docs page that lags the code is a bug
- `CHANGELOG.md` tracks versions per component (`## [Server x.y.z]` and `## [Node x.y.z]` headings) — bump only the component that changed
- Every model field, DTO, and server setting carries a concise XML doc comment; logging goes through `[LoggerMessage]` source-generated methods

## CI and releases

Pull requests to `main` run build, test, and coding-guidelines jobs. Merges publish the server and node images to ghcr.io and create per-component tags and releases, using the matching changelog section as notes. The frontend is not published as an image; run it with `dotnet run --project src/Obicon.Client`.
