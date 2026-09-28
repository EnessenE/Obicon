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
  "Type": 3,  // Http = 2, Https = 3
  "NodeIds": ["11111111-1111-1111-1111-111111111111"],
  "Frequency": 2,  // TwoMinutes = 2
  "IsActive": true
}
```

**Response:** 201 Created
```json
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
Triggers immediate execution of a test.

**Response:** 200 OK
```json
{
  "Message": "Test run triggered"
}
```

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
