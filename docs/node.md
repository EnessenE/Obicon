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
| `LogShippingEnabled` | `true` | Ship this node's log entries to the server while the server allows it |
| `LocalLoggingEnabled` | *(null)* | Override the server's local-logging policy: null follows the server, `true` always logs locally, `false` never does |
| `LogShippingMinLevel` | `Information` | Minimum level of entries shipped: `Debug`, `Information`, `Warning`, or `Error` |
| `IpCheckIntervalMinutes` | `30` | How often the node re-resolves its internal and external IP and reports changes |
| `ExternalIpCheckUrl` | `https://checkip.amazonaws.com` | Service that returns the node's public IPv4 address in plain text or JSON |
| `ExternalIpCheckUrlIpv6` | `https://api6.ipify.org` | Service that returns the node's public IPv6 address; unreachable means IPv6 reports as unavailable |

## Address reporting

The node keeps track of four addresses of its own and reports them to the server: its **internal (LAN) IPv4 and IPv6 addresses** from its network interfaces, and its **external (public) IPv4 and IPv6 addresses** by asking the configured check services. All refresh on the `Node:IpCheckIntervalMinutes` interval; changes are logged and pushed to the server immediately as `NodeInfoUpdate` messages, and current values also travel with every registration. The external checks are best effort — a family the node cannot resolve (e.g. no IPv6 connectivity) is reported as unavailable, and offline nodes keep their last known addresses. All appear on the nodes page next to the connection-observed address.

## Log shipping

The node captures every log event flowing through its Serilog pipeline and ships the entries to the server as `NodeLog` WebSocket messages, including as much metadata as available: node name, version, UTC timestamp, level, rendered message, exception, and all structured properties (source context, scope properties like `JobId`, and named values).

Shipping only happens while the server announced `LogShippingEnabled` in its hello; entries are dropped otherwise, and the node can opt out entirely with `Node:LogShippingEnabled`. The server's local-logging policy from the same hello mutes only **test-related output** on the node's own console (test assignments, test execution, monitoring stats) — connection lifecycle, policy changes, and errors always appear locally, and muted entries still ship. Every setting change is logged by the node before it takes effect; the node's `Node:LocalLoggingEnabled` override wins over the server's default.

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
