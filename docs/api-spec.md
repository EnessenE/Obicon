# Obicon API Specification

## Base URL
```
http://localhost:5000
```

## Authentication
All endpoints require the `Authorization` header carrying the configured API key (`ServerSettings:AuthHeader` on the server, default `secureobiconkey`):
```
Authorization: secureobiconkey
```

## Conventions

- Routes are lowercase and plural, hyphenating multi-word resources: `/v1/nodes`, `/v1/pools`, `/v1/tests`, `/v1/test-runs`, `/v1/enroll-tokens`, `/v1/settings`. Exceptions: `/v1/enroll` (the node self-enrollment action) and `/v1/health`
- Responses are `application/json` only, camelCased
- Enums travel as camelCase strings (`"ping"`, `"ipv4"`, `"completed"`), never as bare integers; unknown values are rejected on the way in
- Every list endpoint returns a page envelope: `{ "items": [...], "total": <rows matching the query across all pages>, "limit": <page size>, "offset": <page start> }`. List requests accept `limit` (default 100, range 1-500) and `offset` (default 0, zero-based)
- Errors use RFC 9457 ProblemDetails (`application/problem+json`): `type`, `title`, `detail`, `status`, `instance`:

```json
{
  "title": "Bad request",
  "detail": "Target is required",
  "status": 400,
  "instance": "/v1/tests"
}
```

Status mapping: 400 invalid input, 401 missing or wrong API key, 403 understood but not allowed for the caller (enrollment disabled, foreign nodes), 404 unknown resource, 409 conflicts (duplicate names, forced settings, editing an enrolled node)
- `PATCH` changes only the fields present in the body; `PUT` replaces the resource wholesale

## Endpoints

### Health
```
GET /v1/health
```
Returns server health status via the ASP.NET Core health checks middleware.

**Response:** 200 OK (Healthy), 503 Service Unavailable (Unhealthy)
```json
{
  "Status": "Healthy",
  "TotalDuration": "00:00:00.0021841",
  "Entries": {
    "database": {
      "Data": {},
      "Description": "PostgreSQL database is reachable",
      "Duration": "00:00:00.0015032",
      "Status": "Healthy"
    }
  }
}
```

---

### Nodes

#### Create Node
```
POST /v1/nodes
```
Creates a new node and returns its authentication token.

**Request Body:**
```json
{
  "name": "My Node"
}
```

**Response:** 201 Created
```json
{
  "id": "00000000-0000-0000-0000-000000000000",
  "name": "My Node",
  "authToken": "the plain token - shown here because this is a creation response; only its SHA-256 hash is stored",
  "isActive": true,
  "createdAt": "2024-01-01T00:00:00Z",
  "lastSeenAt": null,
  "labels": [],
  "enrollmentType": "manual",
  "version": null,
  "versionSupported": null,
  "ipAddress": null,
  "internalIpv4": null,
  "internalIpv6": null,
  "externalIpv4": null,
  "externalIpv6": null,
  "settings": {}
}
```
`enrollmentType` is `manual` here or `auto-enrollment` when the node registered itself. `version`, `ipAddress`, and `settings` are filled by the node when it connects: the node reports its software version and operating settings, and the server records the IP of its WebSocket connection. They are empty until the first connection. `versionSupported` is the server's verdict on the reported version (same major.minor as the server, as checked on the node's connection): true, false, or null when the node never reported a version. The four `internal*`/`external*` addresses are the node's own resolved LAN and public addresses per family, null when unavailable.

#### List Nodes
```
GET /v1/nodes?limit={limit}&offset={offset}
```
Returns one page of registered nodes, oldest first, in the page envelope described under Conventions. `limit` defaults to 100 (range 1-500); `offset` is zero-based.

**Response:** 200 OK
```json
{
  "items": [
    {
      "id": "00000000-0000-0000-0000-000000000000",
      "name": "My Node",
      "authToken": "",
      "isActive": true,
      "createdAt": "2024-01-01T00:00:00Z",
      "lastSeenAt": "2024-01-01T00:00:01Z",
      "enrollmentType": "manual",
      "labels": [],
      "version": "0.2.0",
      "versionSupported": true,
      "ipAddress": "192.168.1.42",
      "internalIpv4": "192.168.1.42",
      "internalIpv6": null,
      "externalIpv4": "77.166.248.192",
      "externalIpv6": null,
      "settings": {
        "MaxConcurrentTests": "4",
        "HeartbeatIntervalSeconds": "1",
        "DefaultTestTimeoutSeconds": "60",
        "MaxTestTimeoutSeconds": "60",
        "ReconnectDelaySeconds": "5"
      }
    }
  ],
  "total": 1,
  "limit": 100,
  "offset": 0
}
```
`authToken` is empty here: the plain token is only returned on creation, token regeneration, or enrollment, and only its SHA-256 hash is stored. `ipAddress` is the address the server observed on the WebSocket; the four reported addresses are the node's own resolved LAN and public addresses per family, null when unavailable.

#### Get Node
```
GET /v1/nodes/{id}
```
Returns details for a specific node.

**Response:** 200 OK
```json
{
  "id": "00000000-0000-0000-0000-000000000000",
  "name": "My Node",
  "authToken": "",
  "isActive": true,
  "createdAt": "2024-01-01T00:00:00Z",
  "lastSeenAt": "2024-01-01T00:00:01Z",
  "labels": [],
  "enrollmentType": "manual",
  "version": "0.2.0",
  "versionSupported": true,
  "ipAddress": "192.168.1.42",
  "internalIpv4": "192.168.1.42",
  "internalIpv6": null,
  "externalIpv4": "77.166.248.192",
  "externalIpv6": null,
  "settings": {
    "MaxConcurrentTests": "4",
    "HeartbeatIntervalSeconds": "1",
    "DefaultTestTimeoutSeconds": "60",
    "MaxTestTimeoutSeconds": "60",
    "ReconnectDelaySeconds": "5"
  }
}
```
Same structure as the List Nodes entries.

#### Update Node
```
PUT /v1/nodes/{id}
```
Updates a node's name and labels, and optionally regenerates its auth token. A regenerated token expires immediately: the old token no longer authenticates and any live connection using it is closed. `PUT` returns 409 for nodes that enrolled themselves (they manage their own name and labels).

**Request Body:**
```json
{
  "name": "Node Updated Name",
  "labels": ["edge", "eu-west"],
  "regenerateToken": false
}
```

**Response:** 200 OK (same structure as Get Node, with the updated `name` and `labels`)
`authToken` is populated only when `regenerateToken` was true; otherwise it is empty.

#### Get Pools for Node
```
GET /v1/nodes/{id}/pools
```
Returns all pools the node belongs to.

**Response:** 200 OK - list of pools (same structure as `GET /v1/pools` entries)

#### Node Status
```
GET /v1/nodes/status
```
Returns the live status of every node: its active flag and whether it currently has a WebSocket connection.

**Response:** 200 OK
```json
[
  {
    "id": "00000000-0000-0000-0000-000000000000",
    "name": "My Node",
    "isActive": true,
    "isConnected": true,
    "lastSeenAt": "2024-01-01T00:00:01Z"
  }
]
```

#### Delete Node
```
DELETE /v1/nodes/{id}
```
Deletes a node, removes it from every pool it belongs to, and removes it from every test that directly targets it (pool-targeted tests are unaffected; a test left with no targets simply stops producing runs).

**Response:** 204 No Content

---

### Pools

Pools group nodes; a node can be in multiple pools.

#### Create Pool
```
POST /v1/pools
```
**Request Body:**
```json
{
  "name": "EU edge",
  "description": "Nodes close to EU customers"
}
```

**Response:** 201 Created
```json
{
  "id": "55555555-5555-5555-5555-555555555555",
  "name": "EU edge",
  "description": "Nodes close to EU customers",
  "nodeIds": [],
  "createdAt": "2024-01-01T00:00:00Z"
}
```

#### List Pools
```
GET /v1/pools?limit={limit}&offset={offset}
```
Returns one page of pools in the page envelope described under Conventions (`limit` default 100, range 1-500; `offset` zero-based).

**Response:** 200 OK - page of pools

#### Get Pool
```
GET /v1/pools/{id}
```
**Response:** 200 OK (same structure as Create Pool)

#### Rename Pool
```
PUT /v1/pools/{id}
```
Updates a pool's name and description.

**Request Body:**
```json
{
  "name": "EU edge renamed",
  "description": "New description, replacing the old one"
}
```
**Response:** 200 OK (same structure as Create Pool)

#### Set Pool Members
```
PUT /v1/pools/{id}/nodes
```
Replaces the pool's member list. Unknown node IDs are rejected with 400.

**Request Body:**
```json
{
  "nodeIds": ["11111111-1111-1111-1111-111111111111"]
}
```
**Response:** 200 OK (same structure as Create Pool)

#### Delete Pool
```
DELETE /v1/pools/{id}
```
Deletes the pool. Nodes are not affected.

**Response:** 204 No Content

---

### Tests

#### Test Types
```
GET /v1/tests/types
```
Returns every test type with whether the server currently offers it, resolved from the `EnabledTestTypes` setting. Used by the settings UI to render one toggle per type.

**Response:** 200 OK
```json
[
  { "type": "ping", "name": "Ping", "enabled": true },
  { "type": "traceroute", "name": "Traceroute", "enabled": false }
]
```
**Errors:** 400 Bad Request when `EnabledTestTypes` holds something other than a list of type names.

#### Create Test
```
POST /v1/tests
```
Creates a new test.

**Request Body:**
```json
{
  "name": "My HTTP Test",
  "type": "http",  // string enum, see TestType below
  "target": "http://example.com/health",
  "nodeIds": ["11111111-1111-1111-1111-111111111111"],
  "poolIds": ["55555555-5555-5555-5555-555555555555"],
  "frequency": 120,  // interval in seconds; must be one of the FrequencyPresetsSeconds presets
  "isActive": true,
  "ipVersion": "any",  // string enum: any, ipv4, ipv6, both
  "timeoutSeconds": 30,  // max execution time per run, seconds
  "expectedStatusCodes": "200-399",
  "checkCertificateExpiryDays": 14,
  "expectedDnsResult": null,
  "expectedBodyPattern": null,  // HTTP/HTTPS: body must match this regex; null = no check
  "headers": {},  // HTTP/HTTPS: custom request headers
  "proxyUrl": null,  // HTTP/HTTPS: http(s) proxy URL; null = direct
  "cacheBust": false,  // HTTP/HTTPS: append a unique query parameter to bypass caches
  "tracerouteMaxHops": null,  // Traceroute: hop limit; null = 30, range 1-64
  "tracerouteQueriesPerHop": null,  // Traceroute: probes per hop; null = 3, range 1-10
  "tracerouteQueryTimeoutMs": null,  // Traceroute: per-probe wait; null = 2000, range 100-60000
  "tracerouteResolveHostnames": null,  // Traceroute: resolve each hop to a hostname; null = true
  "pingCount": null,  // Ping: probes per run; null = 4, range 1-100
  "pingTimeoutMs": null,  // Ping: per-probe wait; null = 2000, range 100-60000
  "pingIntervalMs": null,  // Ping: wait between probes; null = 0, range 0-10000
  "httpMethod": null,  // HTTP/HTTPS: GET, HEAD, POST, PUT, DELETE, PATCH, OPTIONS, or TRACE; null = GET
  "followRedirects": null,  // HTTP/HTTPS: follow redirects; null = true
  "dnsNameserver": null,  // DNS: nameserver to query instead of the system's; null = system
  "dnsQueryType": null  // DNS: A, AAAA, CNAME, TXT, MX, or CAA; null = A + AAAA
}
```

Validation (returns 400 with details on failure): `name` and `target` are required, at least one node ID or pool ID must be given, `type` must be a valid enum value, `frequency` is the interval in seconds and must be one of the `FrequencyPresetsSeconds` server setting values (default `[10,30,60,120,300,600,3600]`), `timeoutSeconds` must be 1-3600 (capped by the server's MaxTestTimeoutSeconds), `expectedStatusCodes` must match `\d{3}(-\d{3})?(,\d{3}(-\d{3})?)*` (e.g. `200-399` or `200,301`), `checkCertificateExpiryDays` must be 0-3650, `expectedBodyPattern` must be a valid regular expression, `headers` names must be non-empty without whitespace or colons, and `proxyUrl` must be an absolute `http://` or `https://` URL, and the traceroute settings must be in range (max hops 1-64, queries per hop 1-10, query timeout 100-60000 ms), as must the ping settings (count 1-100, probe timeout 100-60000 ms, interval 0-10000 ms), `httpMethod` must be one of GET, HEAD, POST, PUT, DELETE, PATCH, OPTIONS, TRACE, and `dnsQueryType` must be A, AAAA, CNAME, TXT, MX, CAA, or ANY. The test type must also be enabled on this server: the `EnabledTestTypes` setting — a list of test type names, e.g. `["Ping","Http","Dns"]` (default empty = all types) — rejects create, edit, and ad-hoc run requests for disabled types with 400.

Targeting: the test runs on the union of `nodeIds` and all members of `poolIds` (deduplicated).

Expectations, evaluated by the node:
- `expectedStatusCodes` (HTTP/HTTPS): the response status must match, otherwise the run fails
- `expectedBodyPattern` (HTTP/HTTPS): the response body must match this regular expression (1-second match timeout), otherwise the run fails; the result is reported in the `body_matched` metric
- `headers` (HTTP/HTTPS): custom headers sent with the request, e.g. authentication
- `proxyUrl` (HTTP/HTTPS): the request goes through this HTTP proxy; per-phase DNS/TLS timings are omitted for proxied runs
- `cacheBust` (HTTP/HTTPS): a unique `_cb` query parameter is appended to the request URL so caches serve a fresh response
- `checkCertificateExpiryDays` (HTTPS): the run fails if the TLS certificate expires within this many days; the expiry date is always reported in the output
- `expectedDnsResult` (DNS): when set, the run fails unless this address is among the resolved addresses; null accepts any successful resolution

**Response:** 201 Created
```json
{
  "id": "22222222-2222-2222-2222-222222222222",
  "name": "My HTTP Test",
  "type": "http",
  "target": "http://example.com/health",
  "nodeIds": ["11111111-1111-1111-1111-111111111111"],
  "poolIds": ["55555555-5555-5555-5555-555555555555"],
  "frequency": 120,
  "isActive": true,
  "ipVersion": "any",
  "timeoutSeconds": 30,
  "expectedStatusCodes": "200-399",
  "checkCertificateExpiryDays": 14,
  "expectedDnsResult": null,
  "expectedBodyPattern": null,
  "headers": {},
  "proxyUrl": null,
  "cacheBust": false,
  "createdAt": "2024-01-01T00:00:00Z",
  "updatedAt": null
}
```

#### List Tests
```
GET /v1/tests?limit={limit}&offset={offset}
```
Returns one page of tests, oldest first, in the page envelope described under Conventions (`limit` default 100, range 1-500; `offset` zero-based).

**Response:** 200 OK
```json
{
  "items": [
    {
      "id": "22222222-2222-2222-2222-222222222222",
      "name": "My HTTP Test",
      "type": "http",
      "nodeIds": ["11111111-1111-1111-1111-111111111111"],
      "frequency": 120,
      "isActive": true,
      "createdAt": "2024-01-01T00:00:00Z",
      "updatedAt": null
    }
  ],
  "total": 1,
  "limit": 100,
  "offset": 0
}
```

#### Get Test
```
GET /v1/tests/{id}
```
Returns details for a specific test.

**Response:** 200 OK (same structure as Create Test)

#### Get Tests for Node
```
GET /v1/tests/node/{nodeId}
```
Returns the active tests that directly target the given node, ordered by creation time. Pool membership is not considered.

**Response:** 200 OK - list of tests

#### Update Test
```
PUT /v1/tests/{id}
```
Updates a test.

**Request Body:**
```json
{
  "name": "My HTTP Test",
  "type": "http",
  "target": "http://example.com/health",
  "nodeIds": ["11111111-1111-1111-1111-111111111111", "33333333-3333-3333-3333-333333333333"],
  "frequency": 60,
  "isActive": true
}
```
`name` is required and renames the test; the remaining fields replace the test wholesale, like on create.

**Response:** 200 OK (same structure as Create Test)

#### Delete Test
```
DELETE /v1/tests/{id}
```
Deletes a test.

**Response:** 204 No Content

#### Change Test Active State
```
PATCH /v1/tests/{id}
```
Partial update of a test. `isActive` is the only patchable field and is required in the body; inactive tests are not run by the scheduler.

**Request Body:**
```json
{ "isActive": false }
```

**Response:** 200 OK (same structure as Create Test, with the new `isActive`)

#### Trigger Test Run
```
POST /v1/tests/{id}/runs
```
Triggers immediate execution of a test. Enqueues one job per targeted node (direct node IDs plus all pool members, one per IP family for `ipVersion: "both"`); the queue processor sends each job to its node and stores the reported result on the job.

**Response:** 200 OK - the created jobs, in enqueue order (same structure as the items of List Test Runs)

#### Run Test Once (dry run)
```
POST /v1/test-runs
```
Runs a single test immediately on a selection of nodes without creating a test first. Accepts explicit `nodeIds` and/or `poolIds`: the explicit nodes always run, and each pool contributes its top 3 connected members — least busy first (fewest queued/assigned/running jobs). All referenced nodes and pools must exist; jobs only go to connected nodes among the selection.

**Request Body:**
```json
{
  "type": "dns",
  "target": "example.com",
  "nodeIds": ["11111111-1111-1111-1111-111111111111"],
  "poolIds": ["55555555-5555-5555-5555-555555555555"],
  "timeoutSeconds": 30,
  "expectedStatusCodes": "200-399",
  "checkCertificateExpiryDays": null,
  "expectedDnsResult": "93.184.216.34",
  "expectedBodyPattern": null,
  "headers": null,
  "proxyUrl": null,
  "cacheBust": false,
  "tracerouteMaxHops": null,
  "tracerouteQueriesPerHop": null,
  "tracerouteQueryTimeoutMs": null,
  "tracerouteResolveHostnames": null,
  "pingCount": null,
  "pingTimeoutMs": null,
  "pingIntervalMs": null,
  "httpMethod": null,
  "followRedirects": null,
  "dnsNameserver": null,
  "dnsQueryType": null
}
```
`timeoutSeconds` is optional (default 60, range 1-60). Accepts the same HTTP expectation fields as a test (`expectedStatusCodes`, `expectedBodyPattern`, `headers`, `proxyUrl`, `cacheBust`, `checkCertificateExpiryDays`, `expectedDnsResult`) and the same traceroute, ping, HTTP, and DNS settings. At least one node ID or pool ID is required.

**Response:** 200 OK - one job per selected node and IP family (a request with `ipVersion: "both"` schedules two jobs per node), in the order of the request; poll each at `GET /v1/test-runs/{id}` until `status` is `"completed"`, `"failed"`, or `"timeout"`.

**Errors:** 400 Bad Request for invalid expectations, unknown node or pool IDs, an empty selection, or when none of the selected nodes are connected.

---

### Metrics

```
GET /metrics
```
Prometheus scrape endpoint (no auth). Exposes:
- `obicon.tests.runs` (counter) and `obicon.tests.duration_ms` (histogram) from the `Obicon.Tests` meter, one label set per test and node combination, exported only while the `TestMetricsEnabled` setting is on. Labels come from the `TestMetricsLabels` setting (default `["test_type","test_name","node_name","node_labels"]`); `test_id` and the counter's `status` are always attached, and the histogram omits `status`
- `obicon.server.actions` (counter, dim `action`), `obicon.server.noruns` (counter, dim `reason`: `never_acknowledged` / `never_started` / `node_offline`), and `obicon.server.nodelogs` (counter, dims `level`, `source_context`, `node_id`, `node_name`) counting received node log entries, from the `Obicon.Server` meter. The NoRun scenario is checked every 10 seconds
- Standard ASP.NET Core and HttpClient instrumentation metrics

Nodes expose their `Obicon.Node` meter (`obicon.node.tests_executed`, `obicon.node.test_duration_ms`, `obicon.node.heartbeats`, `obicon.node.reconnects`) on `http://localhost:9464/metrics` by default, configurable via `Node:MetricsHost` and `Node:MetricsPort` (e.g. `Node__MetricsPort=9500`; host `+` exposes metrics outside the machine).

### Settings

Server settings resolve as: forced by appsettings/env (read-only) → database override → default. Read-only derived settings (e.g. `SchedulerLoopIntervalSeconds`) are computed from other settings: `PUT` returns 409 for them, and their `source` is `Derived`. Each setting carries a `group` naming the section it is displayed under in the settings UI, e.g. `General` or `Observability`.

Values are typed on the wire: booleans as `true`/`false`, integers as numbers, and collection settings (`EnabledTestTypes`, `FrequencyPresetsSeconds`, `TestMetricsLabels`) as native JSON arrays - never JSON-encoded strings. `PUT` accepts the same native forms (a plain string is also accepted for scalar settings), and forced configuration in appsettings or environment variables uses native arrays the same way.

#### List Settings
```
GET /v1/settings
```
**Response:** 200 OK
```json
[
  {
    "key": "NodeAutoEnrollmentEnabled",
    "description": "If enabled, nodes can register themselves with a valid enroll token...",
    "value": false,
    "isForced": false,
    "isReadOnly": false,
    "source": "Default",
    "group": "General"
  }
]
```
`source` is one of `Default`, `Configuration (forced)`, `Database`, or `Derived`.

#### Change Setting
```
PUT /v1/settings/{key}
```
**Request Body:**
```json
{ "value": true }
```
**Response:** 200 OK (the updated setting)
**Errors:** 409 when the setting is forced by configuration, 400 for unknown keys or invalid values.

---

### Enroll Tokens

Enroll tokens let nodes register themselves (requires the `NodeAutoEnrollmentEnabled` setting). Only the SHA-256 hash is stored; the plain token is returned exactly once, on creation. Token names default to `enroll-token-dd-MM-yyyy-HH-mm-ss`.

A token can be **scoped to a pool** (`poolId`): nodes enrolling with it are always added to that pool, on top of the pools they request themselves. A token without `poolId` is server-wide.

#### Create Token
```
POST /v1/enroll-tokens
```
**Request Body:**
```json
{ "name": "raspberry-pis", "expiresAt": "2026-12-31T00:00:00Z", "poolId": null }
```
Both fields optional. **Response:** 201 Created, includes the plain `token` once and the `poolId`. **Errors:** 400 for an unknown pool ID.

#### List Tokens
```
GET /v1/enroll-tokens?limit={limit}&offset={offset}
```
Returns one page of tokens in the page envelope described under Conventions (`limit` default 100, range 1-500; `offset` zero-based).

**Response:** 200 OK - page without plain tokens, with `createdAt`, `expiresAt`, `revokedAt`.

#### Revoke Token
```
POST /v1/enroll-tokens/{id}/revoke
```
**Response:** 204 No Content

#### Delete Token
```
DELETE /v1/enroll-tokens/{id}
```
**Response:** 204 No Content

---

### Node Enrollment

```
POST /v1/enroll
```
Authenticates with the enroll token in the body instead of the API Authorization header. Requires the `NodeAutoEnrollmentEnabled` setting (403 otherwise). Enrolled nodes manage their own name, labels, and pools; `PUT /v1/nodes/{id}` returns 409 for them. A token scoped to a pool (see Enroll Tokens) always adds the enrolled node to that pool, in addition to the pools requested in the body.

**Request Body:**
```json
{
  "enrollToken": "the-plain-enroll-token",
  "nodeId": null,
  "nodeName": "pi-1",
  "labels": ["edge", "home"],
  "pools": ["raspberry-pis"]
}
```
`nodeId` is set when updating an already enrolled node; pools are matched by name and created when missing.

**Response:** 200 OK
```json
{
  "id": "00000000-0000-0000-0000-000000000000",
  "name": "pi-1",
  "authToken": "the-node-auth-token",
  "labels": ["edge", "home"],
  "poolIds": ["55555555-5555-5555-5555-555555555555"]
}
```

---

### Server Stats

#### Stats
```
GET /v1/stats
```
Returns aggregated statistics about the server.

**Response:** 200 OK
```json
{
  "version": "0.2.0",
  "totalTests": 3,
  "activeTests": 2,
  "totalNodes": 4,
  "connectedNodes": 1,
  "queuedJobs": 0,
  "runningJobs": 0,
  "completedJobs": 12,
  "failedJobs": 1,
  "timedOutJobs": 0,
  "noRunJobs": 0,
  "uptime": "1.02:03:04",
  "timestamp": "2024-01-01T00:00:00Z"
}
```

---

### Test Runs

#### List Test Runs
```
GET /v1/test-runs?limit={limit}&offset={offset}&status={status}&nodeId={nodeId}&testId={testId}&search={search}&sortBy={sortBy}&sortOrder={sortOrder}
```
Returns one page of test runs plus the total number of runs matching the filters across all pages. All query parameters are optional.

| Parameter | Default | Meaning |
|-----------|---------|---------|
| `limit` | `50` | Page size, range 1 to 500 |
| `offset` | `0` | Zero-based offset of the first run in the page |
| `status` | *(all)* | Only runs with this TestJobStatus value (string, e.g. `completed`) |
| `nodeId` | *(all)* | Only runs executed by this node |
| `testId` | *(all)* | Only runs of this test |
| `search` | *(none)* | Case-insensitive text matched against target, output, and error message |
| `sortBy` | `createdAt` | Sort column: `createdAt`, `durationMs`, or `status` |
| `sortOrder` | `desc` | Sort direction: `asc` or `desc` |

**Response:** 200 OK
```json
{
  "items": [
    {
      "id": "44444444-4444-4444-4444-444444444444",
      "testId": "22222222-2222-2222-2222-222222222222",
      "nodeId": "11111111-1111-1111-1111-111111111111",
      "testType": "dns",
      "target": "example.com",
      "timeoutSeconds": 60,
      "ipVersion": "both",
      "status": "queued",
      "createdAt": "2024-01-01T00:00:00Z",
      "acknowledgedAt": null,
      "startedAt": null,
      "completedAt": null,
      "success": null,
      "durationMs": null,
      "output": null,
      "errorMessage": null,
      "details": null
    }
  ],
  "total": 423,
  "limit": 50,
  "offset": 0
}
```
For one-off runs (from `POST /v1/test-runs`), `testId` is `00000000-0000-0000-0000-000000000000`. `details` carries the structured result sections described under the TestResult message; it is null for results reported by nodes older than 0.4.0 (which also lose their old flat metrics).

The runs are a bounded window, not a history: finished jobs are governed by the `TestResultStorageMode` setting (`Full` keeps the payload, `MetadataOnly` keeps the row without it, `None` deletes the row on completion — so with `None` only in-flight jobs are ever listed) and the `JobRetentionDays` sweep deletes finished jobs after the window (default 30 days). Anything older lives in the user's metrics and log backend.

#### Get Run
```
GET /v1/test-runs/{id}
```
Returns a single test run.

**Response:** 200 OK (same structure as the items of List Test Runs)

## TestJobStatus
Reported on the wire as a camelCase string:
| Wire value | Description |
|------------|-------------|
| `queued` | Waiting in the queue to be assigned to a node |
| `assigned` | Dispatched; the node acknowledged receipt but has not started it |
| `running` | Currently executed by the node |
| `completed` | Ran successfully |
| `failed` | Ran and failed |
| `timeout` | Exceeded its execution time |
| `noRun` | The job never ran: assigned but never acknowledged within [test timeout] / NoRunGraceFactor, acknowledged but never started within [test timeout] + 15s, or the node was never connected within [test timeout] + 15s while the job sat queued |

---

## TestType
Sent and returned as a camelCase string:
| Wire value | Description |
|------------|-------------|
| `ping` | ICMP echo probe |
| `traceroute` | Network path trace |
| `http` | Plain HTTP request test |
| `https` | TLS HTTP request test |
| `tcp` | TCP connect test |
| `dns` | DNS resolution test |
| `tls` | Handshake against host:port (default 443), reporting the certificate and negotiated parameters; `checkCertificateExpiryDays` applies |

## Test Frequency

`Frequency` is the interval between runs, in plain seconds (no enum). The allowed values come from the `FrequencyPresetsSeconds` server setting (default: `[10,30,60,120,300,600,3600]`); create and update reject any value outside it. Adjust the setting on the Settings page or via `PUT /v1/settings/FrequencyPresetsSeconds` to offer different intervals, e.g. `[15,45,1800]`.

Frequencies are enforced by the `TestScheduler` background loop, which wakes every `SchedulerLoopIntervalSeconds` (a read-only setting derived from the lowest `FrequencyPresetsSeconds` preset, default 10). Each wake runs one database query plus an in-memory scan and then sleeps (`Task.Delay`), so the CPU cost is one short database burst per wake — a lower interval means proportionally more wakes per hour. Changing `FrequencyPresetsSeconds` takes effect on the next cycle without a restart. Active tests are enqueued each time their interval elapses; after server downtime an overdue test runs once and resynchronizes instead of catching up.

Databases from before this change stored `Frequency` as the old `TestFrequency` enum (0-6); the server converts those rows to seconds once at startup (`SchemaMigrations` table records it).

## IpVersion
Sent and returned as a camelCase string:
| Wire value | Description |
|------------|-------------|
| `any` | Use whatever the host resolves to |
| `ipv4` | Force IPv4, fail if no A record |
| `ipv6` | Force IPv6, fail if no AAAA record |
| `both` | The server schedules one job pinned to IPv4 and one pinned to IPv6 for every targeted node, so both families are tested independently |

---

## WebSocket

### Connection
```
ws://localhost:5000/ws/nodes?token={authToken}
```

### Message Format
```json
{
  "type": "NodeRegistration|NodeHeartbeat|ServerHello|ServerPolicyUpdate|TestAssignment|TestResult|TestStatusUpdate|ErrorReport|NodeLog|NodeInfoUpdate",
  "data": { ... }
}
```

### Message Types

#### ServerHello
Sent by server immediately after accepting a node's WebSocket connection. The node logs the server version and checks compatibility: a server outside the node's supported range (same major.minor) always closes the connection — the node has no override. Mirrored on the server: a node reporting an unsupported version is disconnected unless the `AllowUnsupportedNodeVersions` server setting is enabled.

The message also carries the server's observability policy: `LogShippingEnabled` mirrors the `NodeLogShippingEnabled` setting (nodes may ship log entries only while it is true), `NodeLocalLoggingEnabled` mirrors the `NodeLocalLoggingEnabled` default for whether nodes log locally — a node's own configuration takes precedence — and `ExternalIpResolvingEnabled` mirrors the `NodeExternalIpResolvingEnabled` setting (default false): while it is false, nodes do not contact any external-IP check service and report those addresses as unavailable.
```json
{
  "type": "ServerHello",
  "data": {
    "ServerVersion": "0.2.0",
    "LogShippingEnabled": false,
    "NodeLocalLoggingEnabled": true,
    "ExternalIpResolvingEnabled": false
  }
}
```

#### NodeRegistration
Sent by node on connection.
```json
{
  "type": "NodeRegistration",
  "data": {
    "NodeId": "string",
    "NodeName": "string",
    "NodeVersion": "0.2.0",
    "MaxConcurrentTests": 4,
    "HeartbeatIntervalSeconds": 1,
    "DefaultTestTimeoutSeconds": 60,
    "MaxTestTimeoutSeconds": 60,
    "ReconnectDelaySeconds": 5,
    "InternalIpv4": "192.168.1.42",
    "InternalIpv6": null,
    "ExternalIpv4": null,
    "ExternalIpv6": null
  }
}
```
The settings fields let the server show what the node is configured for; they are all optional (older nodes omit them) and surface through the nodes API in the `Settings` dictionary. The address fields are the node's own resolved internal (LAN) and external (public) addresses per family, refreshed on an interval and pushed as `NodeInfoUpdate` messages; a null family is reported as unavailable.

#### NodeHeartbeat
Sent by node periodically (default: every 1 second).
```json
{
  "type": "NodeHeartbeat",
  "data": {
    "NodeId": "string",
    "Timestamp": "ISO8601 datetime"
  }
}
```

#### TestAssignment
Sent by server to assign a test to a node.
```json
{
  "type": "TestAssignment",
  "data": {
    "JobId": "string",
    "TestId": "string",
    "TestType": 0-5,
    "Target": "string",
    "Frequency": 60,  // seconds
    "ExpectedBodyPattern": null,  // HTTP/HTTPS: body regex; null = no check
    "Headers": null,  // HTTP/HTTPS: custom request headers
    "ProxyUrl": null,  // HTTP/HTTPS: proxy URL; null = direct
    "CacheBust": false,  // HTTP/HTTPS: append a cache-busting query parameter
    "TracerouteMaxHops": null,  // Traceroute: hop limit; null = 30
    "TracerouteQueriesPerHop": null,  // Traceroute: probes per hop; null = 3
    "TracerouteQueryTimeoutMs": null,  // Traceroute: per-probe wait in ms; null = 2000
    "TracerouteResolveHostnames": null,  // Traceroute: resolve each hop to a hostname; null = true
    "PingCount": null,  // Ping: probes per run; null = 4
    "PingTimeoutMs": null,  // Ping: per-probe wait in ms; null = 2000
    "PingIntervalMs": null,  // Ping: wait between probes in ms; null = 0
    "HttpMethod": null,  // HTTP/HTTPS: GET, HEAD, POST, PUT, DELETE, PATCH, OPTIONS, or TRACE; null = GET
    "FollowRedirects": null,  // HTTP/HTTPS: follow redirects; null = true
    "DnsNameserver": null,  // DNS: nameserver to query instead of the system's; null = system
    "DnsQueryType": null  // DNS: A, AAAA, CNAME, TXT, MX, or CAA; null = A + AAAA
  }
}
```

#### TestResult
Sent by node to report test results.
```json
{
  "type": "TestResult",
  "data": {
    "JobId": "string",
    "TestId": "string",
    "NodeId": "string",
    "Success": true/false,
    "DurationMs": 1234,
    "Output": "string",
    "Details": { "Traceroute": { "ResolvedAddress": "93.184.216.34", "TargetReached": true, "HopCount": 2, "Hops": [ { "Hop": 1, "Address": "10.0.0.1", "Status": "TtlExpired", "RoundtripMs": 5.0, "Error": null } ] } }
  }
}
```

`Details` carries the structured measurements of the run — exactly one populated section matching the test type, with HTTP and HTTPS both using `Http`. It is persisted on the job and exposed by the queue API as `details` (camelCased there, like every HTTP response, while the WebSocket payload uses the PascalCase names shown here); the web UI renders it. Sections:

- **Traceroute** — `ResolvedAddress`, `TargetReached`, `HopCount`, and `Hops`: one record per hop with `Hop` (number), `Address` (null when nothing responded), `Hostname` (best-effort reverse lookup, null when unresolved or disabled), `Status` (`TtlExpired`, `Success`, `TimedOut`, ...), `RoundtripMs` (average of the answered probes), `Probes` (one record per probe with `Status` and `RoundtripMs`), and `Error` when the hop ended the trace
- **Ping** — `Target`, `ResolvedAddress`, `DnsMs`, `ReplyAddress`, `ReplyStatus`, `RoundtripMs` (average of the answered probes), `Ttl`, `WallclockMs`, `Sent`, `Received`, `LossPercent`, `MinRoundtripMs`/`AvgRoundtripMs`/`MaxRoundtripMs` (null when nothing was received), `Replies` (one record per probe with `ReplyAddress`, `ReplyStatus`, `RoundtripMs`, `Ttl`), `Error`. The run succeeds when at least one probe got a reply, classic ping semantics
- **Tcp** — `Host`, `Port`, `ResolvedAddress`, `Family`, `DnsMs`, `ConnectMs`, `Error`
- **Http** (HTTP and HTTPS) — `Url`, `Method`, `FinalUrl`, `StatusCode`, `ReasonPhrase`, `ResolvedAddress`, the phase timings `DnsMs`/`ConnectMs`/`TlsMs`/`TtfbMs`/`TransferMs` (`DnsMs`, `ConnectMs`, and `TlsMs` are null for proxied requests), `TlsProtocol`, `TlsCipher`, `BytesRead`, `BytesTruncated`, `ProxyUrl`, `BodyMatched` (null when no pattern was set), `Certificate` (`Subject`, `Issuer`, `NotBefore`, `NotAfter`, `DaysRemaining`, `SubjectAlternativeNames`), `Error`
- **Dns** — `Host`, `QueryType` (the queried record type), `NameserversQueried`, `AnsweringNameserver`, `NameserverRttMs`, `Records` (one per returned record with `RecordType`, `Value`, and `TtlSeconds`), `Resolved` (record values after the IP version filter, which applies to address records only), `ResponseStatus` (the answering nameserver's DNS status, e.g. `NOERROR` or `NXDOMAIN`), `Via` (`nameserver`, `os-resolver`, or `literal`), `ExpectedAddress`, `ExpectedMatched`, `Error`
- **Tls** — `Host`, `Port`, `ResolvedAddress`, `Family`, `DnsMs`, `ConnectMs`, `HandshakeMs`, `Protocol`, `Cipher`, `Certificate` (same shape as the HTTP certificate), `Error`. The run succeeds when the handshake completes and the certificate is within the expiry threshold

#### TestStatusUpdate
Sent by node to report in-progress job status: `Assigned` (1) when it accepts a job and `Running` (2) when execution starts. Final outcomes (Completed, Failed, Timeout) are reported via `TestResult` instead; `Queued` and `NoRun` are server-side only.
```json
{
  "type": "TestStatusUpdate",
  "data": {
    "JobId": "string",
    "TestId": "string",
    "NodeId": "string",
    "Status": 1-2,
    "Message": "string"
  }
}
```

#### ErrorReport
Sent by node to report errors.
```json
{
  "type": "ErrorReport",
  "data": {
    "NodeId": "string",
    "ErrorType": "string",
    "ErrorMessage": "string",
    "StackTrace": "string",
    "Timestamp": "ISO8601 datetime"
  }
}
```

#### NodeLog
Sent by node to ship one of its log entries to the server. Accepted only while the `NodeLogShippingEnabled` setting is true (announced in the server hello); while it is false, entries are dropped. When received and `ShipNodeLogsToConsole` is true, the server writes the entry to its own console and log, tagged with the node's identity. `Properties` carries the structured metadata of the entry: source context, scope properties (e.g. `JobId`), and any named values of the log call.
```json
{
  "type": "NodeLog",
  "data": {
    "NodeId": "string",
    "NodeName": "string",
    "NodeVersion": "0.2.0",
    "Timestamp": "ISO8601 datetime",
    "Level": "Debug|Information|Warning|Error",
    "Message": "string",
    "Exception": null,
    "Properties": {
      "SourceContext": "Obicon.Node.Services.MonitoringService",
      "JobId": "44444444-4444-4444-4444-444444444444"
    }
  }
}
```
Node-side controls: `Node:LogShippingEnabled` (opt the node out), `Node:LogShippingMinLevel` (minimum shipped level, default `Information`), and `Node:LocalLoggingEnabled` (override of the server's local-logging policy).

#### ServerPolicyUpdate
Sent by server to every connected node when a node-facing setting changes at runtime (`NodeLogShippingEnabled`, `NodeLocalLoggingEnabled`, or `NodeExternalIpResolvingEnabled`), so nodes apply the new policy on the fly without reconnecting. A node's `Node:LocalLoggingEnabled` override still wins over the announced local logging default, and a change to the external IP policy triggers a prompt address refresh on the node.
```json
{
  "type": "ServerPolicyUpdate",
  "data": {
    "LogShippingEnabled": true,
    "NodeLocalLoggingEnabled": true,
    "ExternalIpResolvingEnabled": true
  }
}
```

#### NodeInfoUpdate
Sent by node when it has refreshed its own addresses: it re-resolves its internal (LAN) IPv4 and IPv6 addresses and asks the configured check services for its external (public) IPv4 and IPv6 addresses on an interval (`Node:IpCheckIntervalMinutes`, default 30 minutes), reporting changes immediately. The addresses also travel with every registration.
```json
{
  "type": "NodeInfoUpdate",
  "data": {
    "NodeId": "string",
    "InternalIpv4": "192.168.1.42",
    "InternalIpv6": null,
    "ExternalIpv4": "77.166.248.192",
    "ExternalIpv6": null
  }
}
```
Null fields mean that address family is unavailable on the node.
