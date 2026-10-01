# Obicon

<p align="center"><img src="assets/logo-512.png" alt="Obicon logo" width="200"></p>

Self-hosted synthetic monitoring, built for observability. Lightweight nodes deployed anywhere run your tests (ping, traceroute, HTTP, HTTPS, TCP, DNS) and report back through a central server, and everything lands in your observability stack, no glue needed.

## Quick start

Published images on ghcr.io — no build required. (Building from source is covered in [Getting started](docs/getting-started.md); that path is for contributing and self-building.)

```bash
# 1. Start the server (API on http://localhost:5000)
docker run -d -p 5000:5000 -v obicon-data:/app/data \
  -e ConnectionStrings__Default="Data Source=/app/data/obicon.db" \
  ghcr.io/enessene/obicon/server:latest

# 2. Connect a node with an enroll token (create it on the server's settings page)
docker run -d --cap-add=NET_RAW \
  -e Node__ServerUrl="ws://<server-host>:5000/ws/nodes" \
  -e Node__EnrollToken="<enroll token>" \
  -e Node__RequireTls=false \
  ghcr.io/enessene/obicon/node:latest
```

Serve the UI — static files in `src/Obicon.Client/wwwroot` — with any static file server, open it, create a test, and watch the queue fill up. The UI is not yet ready for release: expect rough edges and breaking changes. The server serves Prometheus metrics at `/metrics` on port 5000 and each node at `/metrics` on its own port (default 9464), and the Grafana dashboards in [`observability/`](observability/) import as-is.

## Documentation

- [Getting started](docs/getting-started.md)
- [Server](docs/server.md) | [Node](docs/node.md) | [Metrics](docs/metrics.md)
- [API specification](docs/api-spec.md)
- [Grafana dashboards](observability/)
