# Obicon

Self-hosted synthetic API testing. A central server schedules and queues tests; lightweight nodes deployed anywhere run them (ping, traceroute, HTTP, HTTPS, TCP, DNS) and report results back, with Prometheus metrics included.

## Quick start

```bash
# 1. Start the server (API on http://localhost:5000)
dotnet run --project src/Obicon.Server

# 2. Start the frontend (UI on http://localhost:5003)
dotnet run --project src/Obicon.Client --urls http://localhost:5003

# 3. Add a node in the UI, then run it with the token you get
Node__Token="<token>" dotnet run --project src/Obicon.Node
```

Open http://localhost:5003, create a test, and watch the queue fill up.

## Documentation

- [Getting started](docs/getting-started.md)
- [Server](docs/server.md) | [Node](docs/node.md) | [Metrics](docs/metrics.md)
- [API specification](docs/api-spec.md)

## Status

Pre-1.0, under active development. Server and Node 1.0.0 are released; see the [changelog](CHANGELOG.md).
