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

**The dashboards assume the default `TestMetricsLabels` set** (`test_type`, `test_name`, `node_name`, `node_labels`). The per-run label selection is yours to change through that setting — deselecting a label empties every dashboard query that filters on it, which is the cardinality trade-off in action.

The server can also push metrics **and** node logs via OTLP instead of (or next to) being scraped: set `Otlp:Endpoint` to your collector or OTLP-capable backend. Node log records arrive with `node_id`, `node_name`, `node_version`, `source_context`, and the entry's properties (`job_id`, `test_id`) as fields — the same keys as the test metrics — so both streams join in one dashboard.

## Joining metrics and logs in Grafana

With the log funnel flowing (Loki ingests OTLP natively), give the dashboards a Loki datasource and reuse the same variables in LogQL. The funnel's records carry the logger scope `Obicon.NodeLog`, so:

```logql
# All shipped node logs
{otel_scope_name="Obicon.NodeLog"}

# Logs of one node, matching the dashboards' node_name variable
{otel_scope_name="Obicon.NodeLog"} | node_name="$node_name"

# Errors and warnings mentioning a job, next to a failure spike in obicon_tests_runs_total
{otel_scope_name="Obicon.NodeLog"} | level=~"Error|Warning" | job_id="$job_id"
```

Field filtering (e.g. `| node_name="fra-1"`) needs Loki 3.3+ or a `| json` stage on JSON bodies; on older versions, match the rendered text instead (`|~ "fra-1"`). The `node_name` and `test_name` dashboard variables drive PromQL and LogQL alike, so a failure spike and its log lines sit in one time range.
