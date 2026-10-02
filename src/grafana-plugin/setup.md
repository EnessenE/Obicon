# Setting up the Obicon plugin in your Grafana instance

This guide installs the Obicon app plugin (`enessene-obicon-app`) into a
Grafana instance and wires it to an Obicon server. Two ways to get it running:

- [Quick start (dev container)](#1-quick-start-dev-container) — for trying it out
- [Installing into an existing instance](#2-installing-into-an-existing-grafana) — for a real Grafana deployment

## Prerequisites

- A Grafana instance, version 12.3 or newer
- A running Obicon server (`http://<host>:5000`) reachable **from the Grafana server** (not from your browser)
- A Prometheus datasource scraping the Obicon server's `/metrics` endpoint — required, it powers the Overview page:

  ```yaml
  # prometheus.yml
  scrape_configs:
    - job_name: obicon-server
      static_configs:
        - targets: ["<obicon-host>:5000"]   # /metrics, no auth needed
  ```

- Optional: a logs datasource (e.g. Loki) — when configured, the plugin's Logs page can browse it
- To build from source: Node.js 22 or newer (20 works) and npm

## 1. Quick start (dev container)

The bundled Docker compose starts a throwaway Grafana 13 with the plugin loaded and unsigned plugins allowed:

```bash
cd src/grafana-plugin
npm install
npm run build        # produces dist/
docker compose up    # Grafana on http://localhost:3000
```

Log in (`admin`/`admin`), open **Administration → Plugins → Obicon**, enable it, then continue with
[Configure the plugin](#3-configure-the-plugin).

> The compose file mounts `dist/` into the container, so run `npm run dev` in a second terminal to
> rebuild while you edit — reload the page to pick up changes.

## 2. Installing into an existing Grafana

### Build the plugin

```bash
cd src/grafana-plugin
npm install
npm run build        # output lands in dist/
```

### Install the plugin files

Copy or mount `dist/` into Grafana's plugins directory as `enessene-obicon-app`:

```bash
cp -r dist /var/lib/grafana/plugins/enessene-obicon-app
```

With Docker, mount it instead of copying:

```yaml
services:
  grafana:
    volumes:
      - ./grafana-plugin/dist:/var/lib/grafana/plugins/enessene-obicon-app:ro
```

The directory name must be the plugin id; use `GF_PATHS_PLUGINS_DIR` (or `plugins =` in
`grafana.ini`) if your plugins live elsewhere.

### Allow the unsigned plugin (or sign it)

Until it is published/sign, Grafana refuses to load it by default. Allow it by id:

```ini
# grafana.ini
[plugins]
allow_loading_unsigned_plugins = enessene-obicon-app
```

or as an environment variable:

```bash
GF_PLUGINS_ALLOW_LOADING_UNSIGNED_PLUGINS=enessene-obicon-app
```

Restart Grafana.

> For a long-lived deployment, consider signing instead: `npm run sign` with a Grafana Cloud
> API key produces a signed `dist/` that loads without the unsigned flag.

### Enable the app

In Grafana: **Administration → Plugins and assets → Obicon → Enable**. The Obicon pages
(Overview, Nodes, Tests, Logs) appear in the main navigation.

## 3. Configure the plugin

On the plugin's **Configuration** page (the "Configuration" entry in the plugin's nav, or
**Administration → Plugins → Obicon**), set:

| Field | Value |
|-------|-------|
| **Server URL** | Base URL of the Obicon API as reachable **from the Grafana server**, e.g. `http://obicon:5000` (same compose network) or `http://host.docker.internal:5000` (Grafana in Docker, Obicon on the host). No trailing path; the plugin calls `/v1/...` under it. |
| **API Authorization header** | The value the Obicon API expects in `Authorization`. This is the server's `ServerSettings:AuthHeader` setting — `uwu` unless you changed it. Stored as a secret. |
| **Prometheus datasource** | The datasource scraping Obicon's `/metrics`. Required. |
| **Logs datasource** | Optional. A logs datasource (e.g. Loki). |
| **Logs query** | Optional. Query for the Logs page, e.g. `{job="obicon"}`. |

Click **Save settings**. Grafana proxies all Obicon API traffic through the backend, so the
browser never talks to the server directly and no CORS configuration is needed on the Obicon
server.

### Provisioning instead of the UI (optional)

You can pre-configure the plugin with Grafana's provisioning:

```yaml
# /etc/grafana/provisioning/plugins/obicon.yaml
apiVersion: 1
apps:
  - type: enessene-obicon-app
    org_id: 1
    disabled: false
    jsonData:
      serverUrl: http://obicon:5000
      prometheusUid: <prometheus-datasource-uid>
      logsUid: <logs-datasource-uid>      # optional
      logsQuery: '{job="obicon"}'         # optional
    secureJsonData:
      authHeader: uwu                     # your server's AuthHeader value
```

## 4. Verify

1. **Overview** shows server stats and Prometheus tiles (success rate, runs, queue depth).
   "Could not reach the Obicon server" means the Server URL or auth header is wrong.
   "Prometheus query failed" means the Prometheus datasource can't see Obicon's metrics.
2. **Nodes** lists your nodes and their connection state within a few seconds.
3. **Tests**: create a test, use **Dry run** to execute it once on a selected node, and check
   the per-node output before saving.

## Troubleshooting

| Symptom | Likely cause |
|---------|--------------|
| Plugin not listed under Installed plugins | `dist/` not in the plugins dir with the right folder name, or the unsigned flag isn't set — check `grafana` logs at startup |
| `plugin route match not found` on API calls | Grafana didn't load the plugin's routes: `plugin.json` is read at startup — rebuild and **restart Grafana** after any change to it |
| `[plugin.unavailable] plugin unavailable` | The client called `/api/plugins/<id>/resources/...`, which requires a plugin backend; the plugin uses `/api/plugin-proxy/<id>/obicon/...` instead — make sure the built `dist/` matches this source |
| "Could not reach the Obicon server" | Grafana can't reach the Server URL (test from the Grafana host: `curl -H "Authorization: <header>" http://<host>:5000/v1/health`) or the Authorization header doesn't match the server's `AuthHeader` setting |
| 502 Bad Gateway, `connection refused` to `[::1]:5000` or `127.0.0.1` | `localhost` inside the Grafana container is the container itself. Point the Server URL at the host as seen from Docker: `http://host.docker.internal:5000` (Docker Desktop; on Linux use the host IP or add `extra_hosts: ["host.docker.internal:host-gateway"]`) |
| Nodes page empty, but server reachable | No nodes enrolled yet — connect a node first (see the main repo's docs/node.md) |
| Overview tiles show "n/a" | Prometheus isn't scraping `obicon.tests.*` — check the server's `/metrics` and the `TestMetricsEnabled` setting |
| Logs page empty or errors | The logs datasource is Loki-style; make sure the query matches your log pipeline's labels |
