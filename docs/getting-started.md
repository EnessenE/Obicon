# Getting started

This guide takes you from nothing to a running test in a few minutes using the published container images — no build required. Building from source is covered at the end; that path is for contributing and self-building.

The API listens on port 5000 and the UI is plain static HTML/JS that calls it from your browser. Run the pieces wherever you like, as long as your browser can reach the server on port 5000.

## 1. Run the server

```bash
docker compose up -d
```

The API is now on http://localhost:5000 (Swagger at http://localhost:5000/swagger). The compose file in the repo root runs the server next to a PostgreSQL database, which is created and migrated automatically on startup — after upgrades the schema is migrated in place, so you never need to delete it. Without compose, run a PostgreSQL container and point `ConnectionStrings__Default` at it (see [Server](server.md)).

## 2. Serve the UI

The UI is static files with no backend. Serve `src/Obicon.Client/wwwroot` with any static file server — for example:

```bash
docker run -d --name obicon-ui -p 5003:80 \
  -v ./src/Obicon.Client/wwwroot:/usr/share/nginx/html:ro \
  nginx:alpine
```

Open http://localhost:5003.

## 3. Connect a node

On the **Settings** page, create an enroll token (optionally scoped to a pool), then start a node with it — the server creates the node automatically on first contact:

```bash
docker run -d --name obicon-node \
  --cap-add=NET_RAW \
  -e Node__ServerUrl="ws://<server-host>:5000/ws/nodes" \
  -e Node__EnrollToken="<enroll token>" \
  -e Node__RequireTls=false \
  ghcr.io/enessene/obicon/node:latest
```

`<server-host>` is the address of the server as seen from the node's container. `NET_RAW` is needed for ping and traceroute tests, and `Node__RequireTls=false` allows the plain `ws://` connection of a local setup — use `wss://` and keep TLS required for real deployments. Alternatively, create a node on the **Nodes** page and pass its token with `Node__Token` instead.

Within a second or two the node connects, registers, and appears as connected on the Nodes page. See [Node](node.md) for all configuration options.

## 4. Create and run a test

On the **Tests** page, create a test — for example an HTTP test against `https://example.com`, targeting the node you just connected, with a frequency of 60 seconds.

Before saving, use **dry run** to execute it once immediately and see the full result (timings, status codes, certificate details). After saving, the scheduler enqueues it at the chosen frequency, and the **Queue** page shows every run with its status and result.

## 5. Watch the metrics

Point Prometheus at the server's `/metrics` endpoint (http://localhost:5000/metrics, no auth) and each node's metrics endpoint (default http://localhost:9464/metrics, exposed in Docker with `Node__MetricsHost=+`). See [Metrics](metrics.md) for what's exported.

The Grafana dashboards in [`observability/`](../observability/) import as-is — one for node health, one for tests, and one for the server — with dropdowns to filter by node, test type, or log level.

## Building from source

For contributing and self-building, run the projects directly:

```bash
# Server (API on http://localhost:5000)
dotnet run --project src/Obicon.Server

# Frontend (UI on http://localhost:5003)
dotnet run --project src/Obicon.Client --urls http://localhost:5003

# Node with a token created in the UI, or an enroll token
Node__Token="<token>" dotnet run --project src/Obicon.Node
```

The tests run with `dotnet test tests/Obicon.Server.Tests` and `dotnet test tests/Obicon.Node.Tests`.

## Where to go next

- [Server](server.md) — settings, configuration layers, auth
- [Node](node.md) — pools, labels, auto-enrollment, Docker
- [API specification](api-spec.md) — everything the UI does is a plain REST call
- [Observability](../observability/) — ready-made Grafana dashboards
