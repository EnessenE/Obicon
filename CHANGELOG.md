# Changelog

All notable changes to Obicon are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com), and each component uses
[semantic versioning](https://semver.org) independently.

Versions are tracked per component: `## [Server x.y.z]` and `## [Node x.y.z]`.
The CI pipeline publishes `ghcr.io/<owner>/<repo>/server:<server version>` and
`.../node:<node version>`, and creates the tags/releases `server-vx.y.z` and
`node-vx.y.z` with the matching section below as notes. The frontend has no
separate version; its changes are listed under the server release.

## [Server 0.1.0] - 2026-09-29

Initial release.

### Server
- ASP.NET Core API (`/v1`) with Swagger and Serilog logging
- SQLite persistence (EF Core) with automatic schema and data migration on upgrade; no need to delete `obicon.db`
- Node management: create and edit nodes, labels, auth tokens regenerated on demand (tokens stored as SHA-256 hashes)
- Node pools; tests target nodes and/or pools, membership visible from both sides
- Node activity view: connected, active, and inactive nodes
- Tests: Ping, Traceroute, HTTP, HTTPS, TCP, DNS with per-test timeout, IP version selection, expected status codes, TLS certificate expiry checks, and DNS result expectations; enable/disable without deleting
- Configurable frequency presets (`FrequencyPresetsSeconds`); the scheduler loop runs at the lowest preset, reported by the read-only `SchedulerLoopIntervalSeconds` setting
- Test queue with NoRun detection (never acknowledged, never started, node offline) and timeout reaping
- Server settings: forced-by-configuration, database overrides, and read-only derived settings, each with a description
- Node auto-enrollment via expiring enroll tokens (hashed at rest), manageable from the settings page
- OpenTelemetry Prometheus metrics on `/metrics` (per-test results with node labels, server actions, NoRuns); native ASP.NET Core health checks
- WebSocket hub for nodes on `/ws/nodes` with heartbeat-based connection tracking

### Frontend
- Static Bootstrap 5 UI: dashboard, nodes, pools, tests (create/edit with live check estimates and dry runs), queue, server stats, settings

## [Node 0.1.0] - 2026-09-29

Initial release.

- Console node connecting over WebSocket; runs all test types with verbose per-run results (DNS resolver details, HTTP/TLS timing breakdowns)
- Configurable Prometheus metrics endpoint via `Node:MetricsHost` (default `localhost`, `+` exposes outside the machine) and `Node:MetricsPort` (default 9464), plus a health endpoint
- Configuration through appsettings or `Node__*` environment variables
