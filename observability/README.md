# Observability

Grafana dashboards and related resources for Obicon. Import via Grafana: Dashboards -> New -> Import, then pick your Prometheus datasource. All dashboards have dropdown variables to filter by node, test type, test, or log level.

| Dashboard | File | Covers |
|-----------|------|--------|
| Node Health | `obicon-nodes.json` | Per-node heartbeats, reconnects, executions, and durations (from each node's own metrics endpoint) |
| Tests | `obicon-tests.json` | Test runs by status and type, durations, queue depth over time, current result per test, NoRuns, plus a Tests table (latest result, 24h runs, success rate, avg duration — click a test to drill into its runs) and a per-node runs table |
| Server | `obicon-server.json` | Received node logs (by level and source), server actions, queue state, web server load |

## Metrics

- **Server** (`/metrics`): test runs and durations, `obicon.tests.queue_jobs` (queue state by status) and `obicon.tests.current_result` (the latest result of each created test), both gauges sampled every 5s, plus `obicon.server.build_info` (server version), NoRuns, server actions, and node logs received.
- **Nodes** (`Node:MetricsHost`/`Node:MetricsPort`): heartbeats, reconnects, and test executions/durations per node. These require scraping each node's own endpoint (e.g. a Prometheus PodMonitor).

Metric names come from the OpenTelemetry meters `Obicon.Tests`, `Obicon.Server` (server) and `Obicon.Node` (nodes); see `docs/metrics.md` if you add or change an instrument.
