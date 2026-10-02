# Changelog

## 0.1.0 (Unreleased)

Initial release.

- Configuration page: Obicon server URL and API Authorization header (secret), required Prometheus datasource, optional logs datasource with query; the save button is always active and names any missing required settings
- Overview page: server stats plus Prometheus tiles (24h success rate, runs, NoRuns, queue depth, latest result per test)
- Nodes page with version-support verdict, connection state, search and filters (state, version support), sortable columns, and a details/edit dialog per node (rename, labels, token regeneration; auto-enrolled nodes are read-only)
- Tests page: create, edit, enable/disable, delete, trigger, and dry-run tests with per-type expectations; switching the test type keeps the entered data
- Results page: all queued jobs from the Obicon API with status, duration, output, and metrics, filterable by test and status
- Server page: edit the Obicon server's runtime settings (forced/read-only shown as locked), plus server stats
- Logs page for the configured logs datasource
