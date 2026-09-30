# Obicon

Self-hosted synthetic monitoring, built for observability. Lightweight nodes deployed anywhere run your tests (ping, traceroute, HTTP, HTTPS, TCP, DNS) and report back through a central server, and everything lands in your observability stack, no glue needed.

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
