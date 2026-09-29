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
Returns server health status.

**Response:**
```json
{
  "Status": "Healthy",
  "Timestamp": "2024-01-01T00:00:00Z"
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
  "AuthToken": "12345678-1234-1234-1234-123456789012",
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
    "AuthToken": "12345678-1234-1234-1234-123456789012",
    "IsActive": true,
    "CreatedAt": "2024-01-01T00:00:00Z",
    "LastSeenAt": "2024-01-01T00:00:01Z"
  }
]
```

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
  "AuthToken": "12345678-1234-1234-1234-123456789012",
  "IsActive": true,
  "CreatedAt": "2024-01-01T00:00:00Z",
  "LastSeenAt": "2024-01-01T00:00:01Z"
}
```

#### Update Node
```
PUT /v1/nodes/{id}
```
Updates a node's name.

**Request Body:**
```json
"Node Updated Name"
```

**Response:** 200 OK
```json
{
  "Id": "00000000-0000-0000-0000-000000000000",
  "Name": "Node Updated Name",
  "AuthToken": "12345678-1234-1234-1234-123456789012",
  "IsActive": true,
  "CreatedAt": "2024-01-01T00:00:00Z",
  "LastSeenAt": "2024-01-01T00:00:01Z"
}
```

#### Delete Node
```
DELETE /v1/nodes/{id}
```
Deletes a node.

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
  "Frequency": 2,  // TwoMinutes = 2
  "IsActive": true
}
```

Validation (returns 400 with details on failure): `Name` and `Target` are required, at least one node ID must be given, `Type` and `Frequency` must be valid enum values.

**Response:** 201 Created
```json
{
  "Id": "22222222-2222-2222-2222-222222222222",
  "Name": "My HTTP Test",
  "Type": 2,
  "Target": "http://example.com/health",
  "NodeIds": ["11111111-1111-1111-1111-111111111111"],
  "Frequency": 2,
  "IsActive": true,
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
    "Frequency": 2,
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
  "Frequency": 1,
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

#### Trigger Test Run
```
POST /v1/tests/{id}/run
```
Triggers immediate execution of a test. Enqueues one job per assigned node; the queue processor sends each job to its node and stores the reported result on the job.

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
Runs a single test immediately on the given node without creating a test first. The node must exist and be connected.

**Request Body:**
```json
{
  "Type": 5,  // Dns
  "Target": "example.com",
  "NodeId": "11111111-1111-1111-1111-111111111111",
  "TimeoutSeconds": 30
}
```
`TimeoutSeconds` is optional (default 60, range 1-60).

**Response:** 200 OK - the created job; poll `GET /v1/queue/{id}` until `Status` is 3 (Completed), 4 (Failed), or 5 (Timeout).

**Error:** 400 Bad Request if the node does not exist or is not connected.

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
    "StartedAt": null,
    "CompletedAt": null,
    "Success": null,
    "DurationMs": null,
    "Output": null,
    "ErrorMessage": null
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
| 1 | Assigned |
| 2 | Running |
| 3 | Completed |
| 4 | Failed |
| 5 | Timeout |

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

## TestFrequency Enum
| Value | Description |
|-------|-------------|
| 0 | TenSeconds |
| 1 | ThirtySeconds |
| 2 | OneMinute |
| 3 | TwoMinutes |
| 4 | FiveMinutes |
| 5 | TenMinutes |
| 6 | OneHour |

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
    "Frequency": 0-6
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
    "Output": "string"
  }
}
```

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
