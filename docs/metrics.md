# Metrics

Obicon exports OpenTelemetry metrics in Prometheus format on both the server and the nodes, and optionally pushes them (plus node logs) via OTLP to your own observability backend.

## Where history lives

The database keeps finished test results only for a bounded window (`JobRetentionDays`, default 30 days; see [Server](server.md#storage)). **Everything older exists only in your metrics and log backend** — point Prometheus (or any OTLP receiver) at Obicon and control retention there, e.g. Prometheus's `--storage.tsdb.retention.time` flag. Without a backend, you have no history beyond the window; with one, you decide how long anything lives.

## Server — `http://localhost:5000/metrics`

No auth required. The per-run test metrics (`obicon.tests.runs` and `obicon.tests.duration_ms`) are exported only while the `TestMetricsEnabled` setting is on; which labels ride along is chosen by the `TestMetricsLabels` setting (JSON array, default `["test_type","test_name","node_name","node_labels"]`). `test_id` and the counter's `status` are always attached; `node_name` is the human-readable node dimension (forks series on rename), `node_id` the rename-stable opt-in, and `node_labels` the churniest label — any label change on any node starts new series. Changing the set starts new series for subsequent runs. Scraped metrics:

| Metric | Type | Labels | Meaning |
|--------|------|--------|---------|
| `obicon.tests.runs` | counter | `status`, `test_id` (forced) plus the selected labels | Completed test runs, one label set per test and node |
| `obicon.tests.duration_ms` | histogram | `test_id` (forced) plus the selected labels | Test execution duration |
| `obicon.tests.queue_jobs` | gauge | `status` (Queued, Assigned, Running, Completed, Failed, Timeout, NoRun) | Current test job count per status, sampled every 5 seconds; always exported from the first boot, with 0 for statuses that have no jobs |
| `obicon.tests.current_result` | gauge | `test_id`, `test_name`, `status` | Latest job status of every created test: 0=Queued 1=Assigned 2=Running 3=Completed 4=Failed 5=Timeout 6=NoRun, -1=never ran; sampled every 5 seconds |
| `obicon.server.build_info` | gauge | `version` | Server build info; value is always 1, the label carries the version |
| `obicon.server.actions` | counter | `action` | Server lifecycle actions (e.g. `created_pool`, `token_regenerated`) |
| `obicon.server.noruns` | counter | `reason` (`never_acknowledged`, `never_started`, `node_offline`) | Jobs that never ran |
| `obicon.server.nodelogs` | counter | `level`, `source_context`, `node_id`, `node_name` | Log entries received from nodes |

Standard ASP.NET Core and HttpClient instrumentation metrics are exported alongside them. The server also exposes a health check at `GET /v1/health`.

On a fresh install, the per-run series (`obicon.tests.runs`, `obicon.tests.duration_ms`) first appear once a test finishes its first run — no series exist for instruments that have not recorded anything yet. `obicon.tests.queue_jobs` is visible from the first boot (all statuses at 0), and `obicon.tests.current_result` appears as soon as any test exists.

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

Ready-made Grafana dashboards for these metrics live in [`observability/`](../observability/): node health, tests, and the server. The tests dashboard has a `Tests` table — click a test name to filter the dashboard to that test's runs — and filters by node, node label, and test type.

## OTLP export (optional)

Set `Otlp:Endpoint` in the server's appsettings (env `Otlp__Endpoint`, e.g. `http://collector:4317`) to push metrics **and** accepted node log entries via OTLP to your observability backend — Prometheus 3.x's OTLP receiver, an OpenTelemetry collector, Mimir, VictoriaMetrics, or a vendor. Empty or absent keeps the scrape-only behavior, and the `/metrics` scrape endpoint stays up either way; pointing both at the same backend is your choice (and ingests twice). Node log records carry `node_id`, `node_name`, `node_version`, `source_context`, and the entry's properties (`job_id`, `test_id`) as fields — the same keys as the test metrics, so both streams join in one Grafana dashboard.
