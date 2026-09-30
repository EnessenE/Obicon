# Obicon API Specification

## Base URL
```
http://localhost:5000
```

## Authentication
All endpoints require the following header:
```
Authorization: uwu
```

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
      "Description": "SQLite database is reachable",
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
  "Name": "My Node"
}
```

**Response:** 201 Created
```json
{
  "Id": "00000000-0000-0000-0000-000000000000",
  "Name": "My Node",
  "AuthToken": "the plain token - shown here because this is a creation response; only its SHA-256 hash is stored",
  "IsActive": true,
  "CreatedAt": "2024-01-01T00:00:00Z",
  "LastSeenAt": null
}
```

#### List Nodes
```
GET /v1/nodes
```
Returns all registered nodes.

**Response:** 200 OK
```json
[
  {
    "Id": "00000000-0000-0000-0000-000000000000",
    "Name": "My Node",
    "AuthToken": "",
    "IsActive": true,
    "CreatedAt": "2024-01-01T00:00:00Z",
    "LastSeenAt": "2024-01-01T00:00:01Z",
    "EnrollmentType": "manual",
    "Labels": []
  }
]
```
`AuthToken` is empty here: the plain token is only returned on creation, token regeneration, or enrollment, and only its SHA-256 hash is stored.

#### Get Node
```
GET /v1/nodes/{id}
```
Returns details for a specific node.

**Response:** 200 OK
```json
{
  "Id": "00000000-0000-0000-0000-000000000000",
  "Name": "My Node",
  "AuthToken": "",
  "IsActive": true,
  "CreatedAt": "2024-01-01T00:00:00Z",
  "LastSeenAt": "2024-01-01T00:00:01Z",
    "EnrollmentType": "manual",
    "Labels": []
}
```

#### Update Node
```
PUT /v1/nodes/{id}
```
Updates a node's name and labels, and optionally regenerates its auth token. A regenerated token expires immediately: the old token no longer authenticates and any live connection using it is closed.

**Request Body:**
```json
{
  "Name": "Node Updated Name",
  "EnrollmentType": "manual",
  "Labels": ["edge", "eu-west"],
  "RegenerateToken": false
}
```

**Response:** 200 OK
```json
{
  "Id": "00000000-0000-0000-0000-000000000000",
  "Name": "Node Updated Name",
  "AuthToken": "",
  "IsActive": true,
  "CreatedAt": "2024-01-01T00:00:00Z",
  "LastSeenAt": "2024-01-01T00:00:01Z",
  "EnrollmentType": "manual",
  "Labels": ["edge", "eu-west"]
}
```
`AuthToken` is populated only when `RegenerateToken` was true; otherwise it is empty.

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
    "Id": "00000000-0000-0000-0000-000000000000",
    "Name": "My Node",
    "IsActive": true,
    "IsConnected": true,
    "LastSeenAt": "2024-01-01T00:00:01Z"
  }
]
```

#### Delete Node
```
DELETE /v1/nodes/{id}
```
Deletes a node.

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
  "Name": "EU edge"
}
```

**Response:** 201 Created
```json
{
  "Id": "55555555-5555-5555-5555-555555555555",
  "Name": "EU edge",
  "NodeIds": [],
  "CreatedAt": "2024-01-01T00:00:00Z"
}
```

#### List Pools
```
GET /v1/pools
```
**Response:** 200 OK - list of pools

#### Get Pool
```
GET /v1/pools/{id}
```
**Response:** 200 OK (same structure as Create Pool)

#### Rename Pool
```
PUT /v1/pools/{id}
```
**Request Body:**
```json
{
  "Name": "EU edge renamed"
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
  "NodeIds": ["11111111-1111-1111-1111-111111111111"]
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

#### Create Test
```
POST /v1/tests
```
Creates a new test.

**Request Body:**
```json
{
  "Name": "My HTTP Test",
  "Type": 2,  // Http = 2, see TestType enum below
  "Target": "http://example.com/health",
  "NodeIds": ["11111111-1111-1111-1111-111111111111"],
  "PoolIds": ["55555555-5555-5555-5555-555555555555"],
  "Frequency": 120,  // interval in seconds; must be one of the FrequencyPresetsSeconds presets
  "IsActive": true,
  "IpVersion": 0,  // Any = 0, Ipv4 = 1, Ipv6 = 2
  "TimeoutSeconds": 30,  // max execution time per run, seconds
  "ExpectedStatusCodes": "200-399",
  "CheckCertificateExpiryDays": 14,
  "ExpectedDnsResult": null
}
```

Validation (returns 400 with details on failure): `Name` and `Target` are required, at least one node ID or pool ID must be given, `Type` must be a valid enum value, `Frequency` is the interval in seconds and must be one of the `FrequencyPresetsSeconds` server setting values (default `10,30,60,120,300,600,3600`), `TimeoutSeconds` must be 1-3600 (capped by the server's MaxTestTimeoutSeconds), `ExpectedStatusCodes` must match `\d{3}(-\d{3})?(,\d{3}(-\d{3})?)*` (e.g. `200-399` or `200,301`), `CheckCertificateExpiryDays` must be 0-3650.

Targeting: the test runs on the union of `NodeIds` and all members of `PoolIds` (deduplicated).

Expectations, evaluated by the node:
- `ExpectedStatusCodes` (HTTP/HTTPS): the response status must match, otherwise the run fails
- `CheckCertificateExpiryDays` (HTTPS): the run fails if the TLS certificate expires within this many days; the expiry date is always reported in the output
- `ExpectedDnsResult` (DNS): when set, the run fails unless this address is among the resolved addresses; null accepts any successful resolution

**Response:** 201 Created
```json
{
  "Id": "22222222-2222-2222-2222-222222222222",
  "Name": "My HTTP Test",
  "Type": 2,
  "Target": "http://example.com/health",
  "NodeIds": ["11111111-1111-1111-1111-111111111111"],
  "PoolIds": ["55555555-5555-5555-5555-555555555555"],
  "Frequency": 120,
  "IsActive": true,
  "IpVersion": 0,
  "TimeoutSeconds": 30,
  "ExpectedStatusCodes": "200-399",
  "CheckCertificateExpiryDays": 14,
  "ExpectedDnsResult": null,
  "CreatedAt": "2024-01-01T00:00:00Z",
  "UpdatedAt": null
}
```

#### List Tests
```
GET /v1/tests
```
Returns all tests.

**Response:** 200 OK
```json
[
  {
    "Id": "22222222-2222-2222-2222-222222222222",
    "Name": "My HTTP Test",
    "Type": 3,
    "NodeIds": ["11111111-1111-1111-1111-111111111111"],
    "Frequency": 120,
    "IsActive": true,
    "CreatedAt": "2024-01-01T00:00:00Z",
    "UpdatedAt": null
  }
]
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
  "Type": 2,
  "Target": "http://example.com/health",
  "NodeIds": ["11111111-1111-1111-1111-111111111111", "33333333-3333-3333-3333-333333333333"],
  "Frequency": 60,
  "IsActive": true
}
```

**Response:** 200 OK (same structure as Create Test)

#### Delete Test
```
DELETE /v1/tests/{id}
```
Deletes a test.

**Response:** 204 No Content

#### Toggle Test
```
POST /v1/tests/{id}/toggle
```
Flips a test between active and inactive without deleting it. Inactive tests are not run.

**Response:** 200 OK (same structure as Create Test, with flipped `IsActive`)

#### Trigger Test Run
```
POST /v1/tests/{id}/run
```
Triggers immediate execution of a test. Enqueues one job per targeted node (direct node IDs plus all pool members); the queue processor sends each job to its node and stores the reported result on the job.

**Response:** 200 OK
```json
{
  "Message": "Test run triggered"
}
```

#### Run Test Once (dry run)
```
POST /v1/tests/run-once
```
Runs a single test immediately on the given node without creating a test first. The node must exist and be connected. Accepts the same expectation fields as a test (`ExpectedStatusCodes`, `CheckCertificateExpiryDays`, `ExpectedDnsResult`).

**Request Body:**
```json
{
  "Type": 5,  // Dns
  "Target": "example.com",
  "NodeId": "11111111-1111-1111-1111-111111111111",
  "TimeoutSeconds": 30,
  "ExpectedStatusCodes": "200-399",
  "CheckCertificateExpiryDays": null,
  "ExpectedDnsResult": "93.184.216.34"
}
```
`TimeoutSeconds` is optional (default 60, range 1-60).

**Response:** 200 OK - the created job; poll `GET /v1/queue/{id}` until `Status` is 3 (Completed), 4 (Failed), or 5 (Timeout).

**Error:** 400 Bad Request if the node does not exist or is not connected.

---

### Metrics

```
GET /metrics
```
Prometheus scrape endpoint (no auth). Exposes:
- `obicon.tests.runs` (counter, dims `status`, `test_type`, `test_id`, `test_name`, `node_id`, `node_name`) and `obicon.tests.duration_ms` (histogram, dims `test_type`, `test_id`, `test_name`, `node_id`, `node_name`) from the `Obicon.Tests` meter. One label set per test and node combination
- `obicon.server.actions` (counter, dim `action`) and `obicon.server.noruns` (counter, dim `reason`: `never_acknowledged` / `never_started` / `node_offline`) from the `Obicon.Server` meter. The NoRun scenario is checked every 10 seconds
- Standard ASP.NET Core and HttpClient instrumentation metrics

Nodes expose their `Obicon.Node` meter (`obicon.node.tests_executed`, `obicon.node.test_duration_ms`, `obicon.node.heartbeats`, `obicon.node.reconnects`) on `http://localhost:9464/metrics` by default, configurable via `Node:MetricsHost` and `Node:MetricsPort` (e.g. `Node__MetricsPort=9500`; host `+` exposes metrics outside the machine).

### Settings

Server settings resolve as: forced by appsettings/env (read-only) → database override → default. Read-only derived settings (e.g. `SchedulerLoopIntervalSeconds`) are computed from other settings: `PUT` returns 409 for them, and their `Source` is `Derived`.

#### List Settings
```
GET /v1/settings
```
**Response:** 200 OK
```json
[
  {
    "Key": "NodeAutoEnrollmentEnabled",
    "Description": "If enabled, nodes can register themselves with a valid enroll token...",
    "Value": "false",
    "IsForced": false,
    "Source": "Default"
  }
]
```

#### Change Setting
```
PUT /v1/settings/{key}
```
**Request Body:**
```json
{ "Value": "true" }
```
**Response:** 200 OK (the updated setting)
**Errors:** 409 when the setting is forced by configuration, 400 for unknown keys or invalid values.

---

### Enroll Tokens

Enroll tokens let nodes register themselves (requires the `NodeAutoEnrollmentEnabled` setting). Only the SHA-256 hash is stored; the plain token is returned exactly once, on creation. Token names default to `enroll-token-dd-MM-yyyy-HH-mm-ss`.

#### Create Token
```
POST /v1/enroll-tokens
```
**Request Body:**
```json
{ "Name": "raspberry-pis", "ExpiresAt": "2026-12-31T00:00:00Z" }
```
Both fields optional. **Response:** 201 Created, includes the plain `Token` once.

#### List Tokens
```
GET /v1/enroll-tokens
```
**Response:** 200 OK - list without plain tokens, with `CreatedAt`, `ExpiresAt`, `RevokedAt`.

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
Authenticates with the enroll token in the body instead of the API Authorization header. Requires the `NodeAutoEnrollmentEnabled` setting (403 otherwise). Enrolled nodes manage their own name, labels, and pools; `PUT /v1/nodes/{id}` returns 409 for them.

**Request Body:**
```json
{
  "EnrollToken": "the-plain-enroll-token",
  "NodeId": null,
  "NodeName": "pi-1",
  "Labels": ["edge", "home"],
  "Pools": ["raspberry-pis"]
}
```
`NodeId` is set when updating an already enrolled node; pools are matched by name and created when missing.

**Response:** 200 OK
```json
{
  "Id": "00000000-0000-0000-0000-000000000000",
  "Name": "pi-1",
  "AuthToken": "the-node-auth-token",
  "Labels": ["edge", "home"],
  "PoolIds": ["55555555-5555-5555-5555-555555555555"]
}
```

---

### Server Stats

#### Stats
```
GET /v1/server/stats
```
Returns aggregated statistics about the server.

**Response:** 200 OK
```json
{
  "TotalTests": 3,
  "ActiveTests": 2,
  "TotalNodes": 4,
  "ConnectedNodes": 1,
  "QueuedJobs": 0,
  "RunningJobs": 0,
  "CompletedJobs": 12,
  "FailedJobs": 1,
  "TimedOutJobs": 0,
  "NoRunJobs": 0,
  "Uptime": "1.02:03:04",
  "Timestamp": "2024-01-01T00:00:00Z"
}
```

---

### Queue

#### List Queue
```
GET /v1/queue
```
Returns all test jobs in the queue, newest first.

**Response:** 200 OK
```json
[
  {
    "Id": "44444444-4444-4444-4444-444444444444",
    "TestId": "22222222-2222-2222-2222-222222222222",
    "NodeId": "11111111-1111-1111-1111-111111111111",
    "TestType": 5,
    "Target": "example.com",
    "TimeoutSeconds": 60,
    "Status": 0,
    "CreatedAt": "2024-01-01T00:00:00Z",
    "AcknowledgedAt": null,
    "StartedAt": null,
    "CompletedAt": null,
    "Success": null,
    "DurationMs": null,
    "Output": null,
    "ErrorMessage": null,
    "Metrics": null
  }
]
```
For one-off runs (from `POST /v1/tests/run-once`), `TestId` is `00000000-0000-0000-0000-000000000000`.

#### Get Job
```
GET /v1/queue/{id}
```
Returns a single test job.

**Response:** 200 OK (same structure as List Queue entries)

## TestJobStatus Enum
| Value | Description |
|-------|-------------|
| 0 | Queued |
| 1 | Assigned - dispatched; the node acknowledged receipt but has not started it |
| 2 | Running |
| 3 | Completed |
| 4 | Failed |
| 5 | Timeout |
| 6 | NoRun - the job never ran: assigned but never acknowledged within [test timeout] / NoRunGraceFactor, acknowledged but never started within [test timeout] + 15s, or the node was never connected within [test timeout] + 15s while the job sat queued |

---

## TestType Enum
| Value | Description |
|-------|-------------|
| 0 | Ping |
| 1 | Traceroute |
| 2 | Http |
| 3 | Https |
| 4 | Tcp |
| 5 | Dns |

## Test Frequency

`Frequency` is the interval between runs, in plain seconds (no enum). The allowed values come from the `FrequencyPresetsSeconds` server setting (default: `10,30,60,120,300,600,3600`); create and update reject any value outside it. Adjust the setting on the Settings page or via `PUT /v1/settings/FrequencyPresetsSeconds` to offer different intervals, e.g. `15,45,1800`.

Frequencies are enforced by the `TestScheduler` background loop, which wakes every `SchedulerLoopIntervalSeconds` (a read-only setting derived from the lowest `FrequencyPresetsSeconds` preset, default 10). Each wake runs one SQLite query plus an in-memory scan and then sleeps (`Task.Delay`), so the CPU cost is one short database burst per wake — a lower interval means proportionally more wakes per hour. Changing `FrequencyPresetsSeconds` takes effect on the next cycle without a restart. Active tests are enqueued each time their interval elapses; after server downtime an overdue test runs once and resynchronizes instead of catching up.

Databases from before this change stored `Frequency` as the old `TestFrequency` enum (0-6); the server converts those rows to seconds once at startup (`SchemaMigrations` table records it).

## IpVersion Enum
| Value | Description |
|-------|-------------|
| 0 | Any - use whatever the host resolves to |
| 1 | Ipv4 - force IPv4, fail if no A record |
| 2 | Ipv6 - force IPv6, fail if no AAAA record |

---

## WebSocket

### Connection
```
wss://localhost:5000/ws/nodes?token={authToken}
```

### Message Format
```json
{
  "type": "NodeRegistration|NodeHeartbeat|TestAssignment|TestResult|TestStatusUpdate|ErrorReport",
  "data": { ... }
}
```

### Message Types

#### NodeRegistration
Sent by node on connection.
```json
{
  "type": "NodeRegistration",
  "data": {
    "NodeId": "string",
    "NodeName": "string"
  }
}
```

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
    "Frequency": 60  // seconds
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
    "Metrics": { "dns_resolved": "93.184.216.34", "dns_ms": 12.5, "connect_ms": 3.2, "tls_ms": 41.0, "ttfb_ms": 120.7, "transfer_ms": 8.1, "bytes_read": 1256 }
  }
}
```

The `Metrics` dictionary carries detailed measurements: HTTP/HTTPS runs report DNS resolution, TCP connect, TLS handshake (including protocol and cipher), time to first byte, and transfer timings plus certificate details; DNS runs report the nameservers queried, which one answered, its round-trip time, and the A/AAAA records; ping, TCP, and traceroute report the resolved address and phase timings.

#### TestStatusUpdate
Sent by node to update test status.
```json
{
  "type": "TestStatusUpdate",
  "data": {
    "JobId": "string",
    "TestId": "string",
    "NodeId": "string",
    "Status": 0-5,
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
