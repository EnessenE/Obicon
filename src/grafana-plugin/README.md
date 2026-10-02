# Obicon Grafana Plugin

A Grafana app plugin for [Obicon](../../README.md), the self-hosted synthetic
monitoring stack. It manages nodes and tests on an Obicon server and shows
Obicon's Prometheus metrics — without leaving Grafana.

The plugin is frontend-only: all traffic to the Obicon server goes through a
Grafana app proxy route, so the browser never talks to the server directly
and no CORS configuration is needed.

**Installing and configuring it in a Grafana instance: see [setup.md](setup.md).**

## What it does

- **Overview** — Obicon server stats plus Prometheus-driven tiles: test
  success rate and run count over 24h, NoRuns, queue depth, and the latest
  result of every test (`obicon.tests.current_result`)
- **Nodes** — every registered node with its version (including the server's
  version-support verdict), connection state, last-seen time, address, and
  labels. Search by name/label/address, filter by state and version support,
  click a column header to sort, and open a node for a details/edit dialog
  (rename, labels, token regeneration; auto-enrolled nodes are read-only).
  Refreshes automatically every 10 seconds
- **Tests** — list, create, edit, enable/disable, delete, trigger, and dry-run
  tests (including per-type expectations: status codes, body regex, headers,
  proxy, cache busting, TLS certificate expiry, DNS expectations). Dry runs
  poll the Obicon queue until every job finishes and show per-node output
- **Results** — every job the Obicon server has queued (`GET /v1/queue`),
  newest first: status, duration, per-node output and metrics, filterable by
  test and status; no Prometheus needed
- **Server** — Obicon's runtime settings (`GET`/`PUT /v1/settings`), grouped
  like the Obicon settings UI, with source badges (Default / Database
  override / Forced by configuration / Derived read-only) and per-setting
  save; forced and read-only settings are shown but locked. Plus the server's
  stats (version, uptime, node/test/queue counts)
- **Logs** — when a logs datasource is configured, query and browse it
  (e.g. Loki with the query from the plugin configuration)

## Configuration

On the plugin's configuration page (Grafana Admin → Plugins → Obicon):

| Field | Required | Purpose |
|-------|----------|---------|
| Server URL | yes | Base URL of the Obicon server API as reachable from Grafana, e.g. `http://obicon:5000` |
| API Authorization header | yes | The `Authorization` header value the Obicon API expects (the server default is `uwu`); stored as a secret |
| Prometheus datasource | yes | Datasource scraping Obicon's `/metrics` — powers the Overview page |
| Logs datasource | no | A logs datasource (e.g. Loki) — enables the Logs page |
| Logs query | no | Query for the Logs page, e.g. `{job="obicon"}` |

## Developing

```bash
npm install                 # install frontend dependencies
npm run dev                 # build and watch the plugin frontend
docker compose up --build   # start a Grafana dev server (http://localhost:3000)
```

The dev Grafana provisions the plugin from `provisioning/plugins/apps.yaml`
(with placeholder configuration — set the real values on the plugin's config
page or edit the provisioning file). Point it at a running Obicon server.

Other checks:

```bash
npm run build        # production build into dist/
npm run typecheck    # tsc --noEmit
npm run lint         # eslint
npm run test:ci      # jest unit tests
npm run e2e          # Playwright tests against the dev Grafana (needs docker)
npm run sign         # sign a release build (needs GRAFANA_API_KEY)
```

> This machine currently has Node 20; the tooling prefers Node 22+ (see
> `.nvmrc`). Builds and tests pass on Node 20, but use Node 22 if you have it.

## Layout

- `src/plugin.json` — plugin metadata, nav pages, and the `obicon` proxy route
- `src/api.ts` — Obicon REST client through the Grafana proxy
- `src/components/AppConfig` — the configuration page
- `src/pages/` — Overview, Nodes, Tests, Logs
- `src/utils/query.ts` — Prometheus/logs datasource queries
- `tests/` — Playwright e2e specs; `provisioning/` — dev-server provisioning
