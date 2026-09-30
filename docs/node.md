# Node

The Obicon node is a small .NET console app that runs close to your users or infrastructure, connects to the primary server, and executes tests on demand. It holds no local state beyond its identity file.

## Running

```bash
dotnet run --project src/Obicon.Node
```

Configure it through `appsettings.json` (`Node` section) or environment variables — every setting can be overridden with `Node__<Name>`:

```bash
Node__ServerUrl="ws://my-server:5000/ws/nodes" \
Node__Token="<auth token>" \
Node__NodeName="fra-1" \
dotnet run --project src/Obicon.Node
```

| Setting | Default | Purpose |
|---------|---------|---------|
| `ServerUrl` | `ws://localhost:5000/ws/nodes` | Server WebSocket URL |
| `Token` | *(empty)* | Node auth token (skip if enrolling) |
| `NodeName` | *(empty)* | Name shown in the UI |
| `HeartbeatIntervalSeconds` | `1` | Heartbeat cadence |
| `MaxConcurrentTests` | `4` | Concurrent test run cap |
| `DefaultTestTimeoutSeconds` | `60` | Timeout when a test doesn't set one |
| `ReconnectDelaySeconds` | `5` | Wait before reconnecting |
| `HealthUrlPrefix` | `http://localhost:8080/` | Health endpoint prefix |
| `MonitoringIntervalSeconds` | `5` | Node statistics logging interval |
| `MetricsHost` / `MetricsPort` | `localhost` / `9464` | Prometheus metrics listener |
| `EnrollToken` | *(empty)* | Enroll token for auto-enrollment |
| `Labels` / `Pools` | *(empty)* | Self-managed labels and pool names (used at enrollment) |
| `AllowUnsupportedServerVersion` | `false` | Stay connected to a server outside the supported version range |

Endpoints the node exposes:

- **Health:** `http://localhost:8080/health`
- **Metrics:** `http://localhost:9464/metrics` (set `MetricsHost` to `+` to expose outside the machine)

## Getting a node's token

**Manual:** create the node on the server's Nodes page (or `POST /v1/nodes`) and copy the shown token into `Node__Token`. Tokens are displayed exactly once; the server stores only a hash.

**Auto-enrollment:** enable the `NodeAutoEnrollmentEnabled` setting on the server, create an enroll token on the settings page, then start the node with `Node__EnrollToken` instead of `Node__Token`. The node registers itself (creating or updating its identity in the local `node-identity.json`), manages its own name, labels, and pools, and receives its auth token automatically. Enroll tokens expire and can be revoked.

## Version compatibility

On every connection the node logs the server's version and a notice whenever it changes. A server is supported while it shares the node's major and minor version (node 0.2.x supports server 0.2.y). An unsupported server connection is closed; set `Node:AllowUnsupportedServerVersion` to continue anyway. The server mirrors this gate with its own `AllowUnsupportedNodeVersions` setting.

## Test execution

The node runs one task per assigned test, capped at `MaxConcurrentTests`. Each run is hard-killed at its timeout plus 5 seconds. Supported types: ping, traceroute, HTTP, HTTPS, TCP, DNS — including expected status codes, body regex checks, custom headers, proxies, and cache busting for HTTP(S), TLS certificate expiry checks, and DNS result expectations (evaluated on the node).

On Linux, ping and traceroute need raw-socket privileges: grant `cap_net_raw` to the binary or run with `sudo`.

## Docker

```bash
docker run -d \
  -e Node__ServerUrl="ws://my-server:5000/ws/nodes" \
  -e Node__Token="<auth token>" \
  -p 9464:9464 \
  --cap-add=NET_RAW \
  ghcr.io/<owner>/<repo>/node
```

`--cap-add=NET_RAW` is needed for ping/traceroute; add `-e Node__MetricsHost=+` to expose the metrics endpoint outside the container.
