# Getting started

This guide takes an Obicon user from nothing to a running test in a few minutes using the published container images — no build required. Everything is done over the REST API: create a node, start a node container, create a test, watch the queue. Building from source, running the test suites, and repo conventions are covered in [Contributing](contributing.md).

The web UI that ships in the repo is not part of the published images — you do not need it to run Obicon. Everything it does is a plain REST call, all documented in the [API specification](api-spec.md), and the interactive reference is Swagger at http://localhost:5000/swagger.

All API requests carry the auth header (default value `secureobiconkey`, configurable — see [Server](server.md#authentication)). The examples below use `curl` against a server on http://localhost:5000.

## 1. Run the server

```bash
git clone https://github.com/EnessenE/Obicon.git
cd Obicon
docker compose up -d
```

The API is now on http://localhost:5000 (Swagger at http://localhost:5000/swagger). The compose file in the repo root runs the server image next to a PostgreSQL database, which is created and migrated automatically on startup. If you would rather not clone the repo, copy `docker-compose.yml` out of it and run it anywhere — the images come from ghcr.io, so nothing is built locally.

## 2. Connect a node

Create a node and copy its auth token from the response — it is shown exactly once:

```bash
curl -X POST http://localhost:5000/v1/nodes \
  -H "Authorization: secureobiconkey" \
  -H "Content-Type: application/json" \
  -d '{ "name": "fra-1" }'
```

Then start the node container with that token:

```bash
docker run -d --name obicon-node \
  --cap-add=NET_RAW \
  -e Node__ServerUrl="ws://<server-host>:5000/ws/nodes" \
  -e Node__Token="<auth token>" \
  ghcr.io/enessene/obicon/node:latest
```

`<server-host>` is the address of the server as seen from the node's container, and `NET_RAW` is needed for ping and traceroute tests. For unattended enrollment instead of manual tokens, enable the `NodeAutoEnrollmentEnabled` setting, create an enroll token, and start nodes with `Node__EnrollToken` — see [Node](node.md).

Within a second or two the node connects and shows up as connected in `GET /v1/nodes/status`. See [Node](node.md) for all configuration options.

## 3. Create and run a test

Create an HTTPS test against example.com, running on that node every 60 seconds (`nodeIds` takes the node's `id` from step 2):

```bash
curl -X POST http://localhost:5000/v1/tests \
  -H "Authorization: secureobiconkey" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "example.com reachable",
    "type": 3,
    "target": "https://example.com",
    "nodeIds": ["<node id>"],
    "frequency": 60,
    "isActive": true,
    "timeoutSeconds": 30
  }'
```

The scheduler enqueues it at the chosen frequency. To execute a test once without creating anything first, use a dry run (`POST /v1/tests/run-once`) — it runs immediately on the selected nodes and reports through the queue like any other job. Every run is a job: `GET /v1/queue` lists them with status, duration, output, and structured details (timings, certificates, DNS records), and `GET /v1/queue/{id}` tracks a single one.

## 4. Watch the metrics

Point Prometheus at the server's `/metrics` endpoint (http://localhost:5000/metrics, no auth) and each node's metrics endpoint (default http://localhost:9464/metrics, exposed in Docker with `Node__MetricsHost=+`). See [Metrics](metrics.md) for what's exported.

The Grafana dashboards in [`observability/`](../observability/) import as-is — one for node health, one for tests, and one for the server — with dropdowns to filter by node, test type, or log level.

## Where to go next

- [Server](server.md) — settings, configuration layers, auth
- [Node](node.md) — pools, labels, auto-enrollment, Docker
- [API specification](api-spec.md) — every endpoint used above, in full
- [Observability](../observability/) — ready-made Grafana dashboards
- [Contributing](contributing.md) — building from source and running the tests
