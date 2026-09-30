# Metrics

Obicon exports OpenTelemetry metrics in Prometheus format on both the server and the nodes.

## Server — `http://localhost:5000/metrics`

No auth required. Scraped metrics:

| Metric | Type | Labels | Meaning |
|--------|------|--------|---------|
| `obicon.tests.runs` | counter | `status`, `test_type`, `test_id`, `test_name`, `node_id`, `node_name` | Completed test runs, one label set per test and node |
| `obicon.tests.duration_ms` | histogram | `test_type`, `test_id`, `test_name`, `node_id`, `node_name` | Test execution duration |
| `obicon.server.actions` | counter | `action` | Server lifecycle actions (e.g. `created_pool`, `token_regenerated`) |
| `obicon.server.noruns` | counter | `reason` (`never_acknowledged`, `never_started`, `node_offline`) | Jobs that never ran |

Standard ASP.NET Core and HttpClient instrumentation metrics are exported alongside them. The server also exposes a health check at `GET /v1/health`.

## Node — `http://localhost:9464/metrics`

Each node exports its own meter (host/port configurable via `Node:MetricsHost` and `Node:MetricsPort`; set the host to `+` to expose it outside the machine):

| Metric | Type | Meaning |
|--------|------|---------|
| `obicon.node.tests_executed` | counter | Test executions on this node |
| `obicon.node.test_duration_ms` | histogram | Test execution duration |
| `obicon.node.heartbeats` | counter | Heartbeats sent to the server |
| `obicon.node.reconnects` | counter | Reconnect attempts |

## Prometheus scrape config example

```yaml
scrape_configs:
  - job_name: obicon-server
    static_configs:
      - targets: ["my-server:5000"]
  - job_name: obicon-nodes
    static_configs:
      - targets: ["node-1:9464", "node-2:9464"]
```

A ready-made Grafana dashboard for these metrics lives in [`observability/obicon-dashboard.json`](../observability/obicon-dashboard.json).
