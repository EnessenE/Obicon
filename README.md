# Obicon

<p align="center"><img src="assets/logo-512.png" alt="Obicon logo" width="200"></p>

Self-hosted synthetic monitoring, built for observability. Lightweight nodes deployed anywhere run your tests (ping, traceroute, HTTP, HTTPS, TCP, DNS, TLS) and report back through a central server, and everything lands in your observability stack, no glue needed.

## Quick start

Everything below uses the published images on ghcr.io — no build, no web UI, just Docker and the REST API. (Building from source and running the tests are covered in [Contributing](docs/contributing.md); the full walkthrough with more node options is [Getting started](docs/getting-started.md).)

```bash
# 1. Start the server and its PostgreSQL database (API on http://localhost:5000)
docker compose up -d

# 2. Create a node — the response shows its auth token exactly once
curl -X POST http://localhost:5000/v1/nodes \
  -H "Authorization: secureobiconkey" \
  -H "Content-Type: application/json" \
  -d '{ "name": "node-1" }'

# 3. Run a node container with that token
docker run -d --cap-add=NET_RAW \
  -e Node__ServerUrl="ws://<server-host>:5000/ws/nodes" \
  -e Node__Token="<auth token>" \
  -e Node__RequireTls=false \
  -p 9464:9464 \
  -e Node__MetricsHost=+ \
  ghcr.io/enessene/obicon/node:latest

# 4. Create a test and watch the runs land
curl -X POST http://localhost:5000/v1/tests \
  -H "Authorization: secureobiconkey" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "example.com reachable",
    "type": "https",
    "target": "https://example.com",
    "nodeIds": ["<node id from step 2>"],
    "frequency": 60,
    "isActive": true,
    "timeoutSeconds": 30
  }'

curl "http://localhost:5000/v1/test-runs?limit=10" -H "Authorization: secureobiconkey"
```

The `docker-compose.yml` in the repo root runs the server image next to a PostgreSQL container and creates the database automatically — copying it out of the repo is enough, nothing is built locally. Without compose:

```bash
docker network create obicon
docker run -d --name obicon-db --network obicon \
  -e POSTGRES_DB=obicon -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=postgres \
  postgres:18-alpine
docker run -d -p 5000:5000 --network obicon \
  -e ConnectionStrings__Default="Host=obicon-db;Database=obicon;Username=postgres;Password=postgres" \
  ghcr.io/enessene/obicon/server:latest
```

Notes for running it yourself:

- **API auth:** every request carries `Authorization: <key>` (default `secureobiconkey`, configured through `ServerSettings:AuthHeader`); Swagger at `/swagger` lets you try every endpoint interactively, and the full reference is the [API specification](docs/api-spec.md)
- **Version pinning:** the server and its nodes must share a major.minor version — the server disconnects older or newer nodes unless `AllowUnsupportedNodeVersions` is enabled, so pin your images to matching release tags instead of `latest` (each release is tagged, e.g. `ghcr.io/enessene/obicon/server:0.5.0`; see the [changelog](CHANGELOG.md) for versions)
- **Unattended enrollment:** instead of creating nodes manually, enable the `NodeAutoEnrollmentEnabled` setting, create an enroll token with `POST /v1/enroll-tokens`, and start nodes with `Node__EnrollToken` — see [Node](docs/node.md)
- **Observability:** the server serves Prometheus metrics at `/metrics` on port 5000 (no auth) and each node at `/metrics` on its own port (default 9464), and the Grafana dashboards in [`observability/`](observability/) import as-is

(The web UI that ships in the repo is not published as an image and is not needed to run Obicon — everything it does is a plain REST call.)

## Documentation

- [Getting started](docs/getting-started.md)
- [Contributing](docs/contributing.md)
- [Server](docs/server.md) | [Node](docs/node.md) | [Metrics](docs/metrics.md)
- [API specification](docs/api-spec.md)
- [Grafana dashboards](observability/)
