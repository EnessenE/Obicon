# Observability

Grafana dashboards and related resources for Obicon.

## Grafana dashboard

`obicon-dashboard.json` — import via Grafana: Dashboards -> New -> Import, then pick your Prometheus datasource.

It covers both sides:

- **Server** (`/metrics`): test runs by status and type, duration p50/p95/p99, NoRuns, server actions, and node logs received (by level) — everything the server records about tests, nodes, and shipped logs.
- **Nodes** (`Node:MetricsHost`/`Node:MetricsPort`): heartbeats and reconnects per node, and per-node test duration p95. These require scraping each node's own metrics endpoint (e.g. a Prometheus PodMonitor).

Metric names come from the OpenTelemetry meters `Obicon.Tests`, `Obicon.Server` (server) and `Obicon.Node` (nodes); see `docs/metrics.md` if you add or change an instrument.
