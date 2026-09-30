# Getting started

This guide takes you from a fresh clone to a running test in a few minutes. Everything runs locally; no external services are required.

## 1. Run the server

```bash
dotnet run --project src/Obicon.Server
```

The API is now on http://localhost:5000 (Swagger at http://localhost:5000/swagger) and the database (`obicon.db`, SQLite) is created automatically on first start — including schema upgrades, so you never need to delete it.

## 2. Run the frontend (optional, but recommended)

```bash
dotnet run --project src/Obicon.Client --urls http://localhost:5003
```

Open http://localhost:5003. All pages call the API on port 5000.

## 3. Add a node

The quickest path is through the UI: on the **Nodes** page, create a node and copy the auth token it shows you. Tokens are shown exactly once — only their SHA-256 hash is stored.

Then start a node with that token:

```bash
Node__Token="<paste the token here>" dotnet run --project src/Obicon.Node
```

Within a second or two the node connects, registers, and appears as connected on the Nodes page. See [Node](node.md) for all configuration options, or the auto-enrollment flow if you don't want to create nodes by hand.

## 4. Create and run a test

On the **Tests** page, create a test — for example an HTTP test against `https://example.com`, targeting the node you just connected, with a frequency of 60 seconds.

Before saving, use **dry run** to execute it once immediately and see the full result (timings, status codes, certificate details). After saving, the scheduler enqueues it at the chosen frequency, and the **Queue** page shows every run with its status and result.

## 5. Watch the metrics

Point Prometheus at the server's `/metrics` endpoint (http://localhost:5000/metrics, no auth) and each node's metrics endpoint (default http://localhost:9464/metrics). See [Metrics](metrics.md) for what's exported.

## Where to go next

- [Server](server.md) — settings, configuration layers, auth
- [Node](node.md) — pools, labels, auto-enrollment, Docker
- [API specification](api-spec.md) — everything the UI does is a plain REST call
