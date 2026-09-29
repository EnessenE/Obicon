We are making an app called "Obicon".

For the agent:

Have a todo list you maintain in this folder.
Store docs relevant to info for yourself in /docs
Add agents.md refering to both

The goal of this app is to be a self-hosted synthetic API testing solution.

Parts:
- Primary server (Obicon server)
- Configuration source
- Self hosted node (Obicon node)
- Prometheus

Both parts (primary server and self hosted nodes) are written in .NET 10. The primary as a ASP.NET core app, The selfhosted nodes as a .NET console app to be a small and light as possible.

Nodes and the primary server talk a websocket to eachother. The primary server is websocket host, and the nodes connect to it, identifying themselves with their auth token.

Flow to add a node:
On the primary server:
- Add a node + info about it
- You get an auth token
- Use auth token on the node to authenticate

- You can now configure tests on the node.

# Current implementation state (updated)

The sections below describe the system as it is implemented today. Deviations from the original spec are called out inline.

## Primary server (src/Obicon.Server)

- ASP.NET Core API under `/v1`: nodes, tests, queue, server stats, health (see /docs/api-spec.md)
- All data persists in SQLite (`obicon.db`) via EF Core — the spec's "JSON file config source" became the database for nodes, tests, and jobs. `IConfigRepository`/`ServerSettings` still exist for server settings, abstracted behind an interface
- Auth layer: API requests require the header `Authorization: uwu`. The WebSocket endpoint authenticates with the node token instead (`/ws`, `/metrics`, `/swagger` are exempt from the header check)
- Queue: `POST /v1/tests/{id}/run` and `POST /v1/tests/run-once` enqueue jobs; `TestQueueProcessor` sends `TestAssignment` to the connected node, stores reported results on the job, and reaps jobs whose node never reports back
- Heartbeats update the node's `LastSeenAt` so the UI shows live node status
- Test results are not yet dumped into Prometheus (todo). k6 is still a later addition

## Self hosted node (src/Obicon.Node)

Console app on the generic host, configured through `appsettings.json` (env-overridable, e.g. `Node__Token`):
- Dedicated communication task (`ServerConnection`): connects with the token, registers, sends a heartbeat every 1s (configurable), reconnects with backoff
- Test executor: one task per test, capped at `MaxConcurrentTests` concurrent runs; each test task destroys itself at `[test timeout]+5s`, test timeout capped at 60s
- Test types: ping, traceroute, http, https, tcp, dns
- Dedicated monitoring task logging node statistics
- `/health` endpoint via HttpListener (no web server, keeps the node light)
- Settings validated at startup (server URL, token, intervals) with clear error logs

## Frontend (src/Obicon.Client)

The spec originally said no frontend was needed; one exists now. It is plain static HTML/JS (Bootstrap) served on port 5003, calling the API on 5000:
- Nodes (create/delete, shows the auth token with copy)
- Tests (create with target + node selection, dry-run on a random node before submitting, run, delete)
- Queue (live job list with statuses and results)
- Server (health + aggregated stats)

## Open work

Tracked in /Project/todo.md. Highest priorities: frequency-based scheduling of tests (the Frequency field is stored but nothing schedules runs yet) and Prometheus export of test results.
