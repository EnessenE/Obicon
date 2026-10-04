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

## Conventions

- The C# Coding Guidelines ([csharpcodingguidelines.com](https://csharpcodingguidelines.com)) are enforced by the root `.editorconfig` and analyzer rules; the `coding-guidelines` CI job verifies formatting and builds with warnings as errors — keep new code violation-free
- Keep `docs/` and the API spec in sync with user-facing behavior: ports, settings, env vars, endpoints. A docs page that lags the code is a bug
- `CHANGELOG.md` tracks versions per component (`## [Server x.y.z]` and `## [Node x.y.z]` headings) — bump only the component that changed
- Every model field, DTO, and server setting carries a concise XML doc comment; logging goes through `[LoggerMessage]` source-generated methods

## CI and releases

Pull requests to `main` run build, test, and coding-guidelines jobs. Merges publish the server and node images to ghcr.io and create per-component tags and releases, using the matching changelog section as notes. The frontend is not published as an image; run it with `dotnet run --project src/Obicon.Client`.
