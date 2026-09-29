# Obicon Project Todo List

## Current Status
- [x] Phase 1: Core Infrastructure (AuthMiddleware, WebSockets setup)
- [x] Phase 2: Node Management (Node model, service, controller, token generation)
- [x] Phase 3: Test Management (Test model, service, controller, enums)
- [x] Phase 4: Queue System (TestJob model, service, background processor)
- [x] Phase 5: WebSocket Communication (Shared messages, middleware, connection manager)
- [x] Phase 6: Configuration (ServerSettings, IConfigRepository, JSON impl)
- [x] Phase 7: Node implementation (WS connection + heartbeat, test executor, ping/traceroute/http/https/tcp/dns runners, health endpoint, monitoring task, max concurrency + [timeout]+5s hard kill)
- [x] Phase 8: Queue dispatch (Target field on tests, TestQueueProcessor sends TestAssignment to connected nodes, results stored on jobs, stale-job reaper, run-once dry runs, server stats endpoint, request validation)
- [x] Phase 9: Node expansion (labels, node pools with membership from both directions, node edit + token regeneration with instant expiry and live disconnect)
- [x] Phase 10: Test targeting (pool IDs, node status endpoint, HTTP expected status codes, TLS cert expiry check, DNS expected result, enable/disable toggle)
- [x] Phase 11: Scheduling + observability (frequency scheduler, IP version targeting, check estimates, OpenTelemetry meters on server and node with Prometheus export on /metrics, action logging standard)
- [x] Phase 12: Settings + enrollment (settings API with forced-from-config detection, settings UI, hashed expiring enroll tokens, node auto-enrollment with self-managed labels and pools)
- [x] Phase 13: Release infrastructure (CHANGELOG.md with separate Server and Node versions, GitHub Actions pipeline: build + test on PR/merge, publish server and node images to ghcr.io, per-component tags and releases, .idea gitignored)
- [ ] Phase 14: Prometheus integration hardening (alert rules, dashboards)

## Next Priorities

### High Priority
- [ ] Dump test results to Prometheus (endpoint excluded from auth already)
- [ ] Add validation to all API endpoints
- [ ] Implement proper error handling

### Medium Priority
- [ ] Add unit tests for services
- [ ] Add integration tests for controllers
- [x] Implement persistent storage (SQLite via EF Core: nodes, tests, jobs)
- [ ] Add rate limiting to API endpoints

### Low Priority
- [ ] Prometheus integration
- [ ] Advanced authentication (JWT)
- [ ] Frontend UI
- [ ] k6 test type support

## Backlog
- Database persistence (PostgreSQL/SQLite)
- Full EF Core migrations (SchemaMigrator covers missing tables/columns and one-time data fixups; column type changes and renames still need manual handling)
- Test result history and analytics
- Alerting system
- Node auto-reconnection logic
- Test timeout enforcement
- Queue prioritization
- Node health monitoring
- API versioning beyond v1
- Use labels when targeting tests at nodes
