import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { css } from '@emotion/css';
import { GrafanaTheme2 } from '@grafana/data';
import { Alert, Badge, Button, Icon, LoadingPlaceholder, Select, useStyles2 } from '@grafana/ui';
import { PluginPage } from '@grafana/runtime';
import { obiconApi } from '../api';
import { errText } from '../utils/errors';
import { JOB_STATUS_NAMES, ObiconNode, ObiconTest, ObiconTestJob, TestJobStatus, TEST_TYPE_NAMES } from '../types';
import { testIds } from '../components/testIds';
import { SimpleTable } from '../components/SimpleTable';
import { timeAgo } from '../components/StatTile';

const EMPTY_GUID = '00000000-0000-0000-0000-000000000000';
const MAX_ROWS = 200;

const ALL_JOB_STATUSES: TestJobStatus[] = [
  TestJobStatus.Queued,
  TestJobStatus.Assigned,
  TestJobStatus.Running,
  TestJobStatus.Completed,
  TestJobStatus.Failed,
  TestJobStatus.Timeout,
  TestJobStatus.NoRun,
];

const STATUS_COLORS: Record<TestJobStatus, 'blue' | 'purple' | 'orange' | 'green' | 'red'> = {
  [TestJobStatus.Queued]: 'blue',
  [TestJobStatus.Assigned]: 'purple',
  [TestJobStatus.Running]: 'orange',
  [TestJobStatus.Completed]: 'green',
  [TestJobStatus.Failed]: 'red',
  [TestJobStatus.Timeout]: 'orange',
  [TestJobStatus.NoRun]: 'red',
};

/**
 * The Results page: every test job the Obicon server has queued, straight
 * from GET /v1/queue — no Prometheus needed. Filter by test and status,
 * expand a row to see the full per-node output.
 */
function ResultsPage() {
  const s = useStyles2(getStyles);
  const [jobs, setJobs] = useState<ObiconTestJob[] | null>(null);
  const [tests, setTests] = useState<ObiconTest[]>([]);
  const [nodeNames, setNodeNames] = useState<Map<string, string>>(new Map());
  const [error, setError] = useState<string | null>(null);
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [testFilter, setTestFilter] = useState<string>('all');
  const [expanded, setExpanded] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      const [queue, testList, nodeList] = await Promise.all([
        obiconApi.getQueue(),
        obiconApi.getTests(),
        obiconApi.getNodes(),
      ]);
      setJobs(queue);
      setTests(testList);
      setNodeNames(new Map((nodeList as ObiconNode[]).map((n) => [n.id, n.name])));
      setError(null);
    } catch (e) {
      setError(errText(e));
    }
  }, []);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    load();
    const timer = setInterval(load, 10000);
    return () => clearInterval(timer);
  }, [load]);

  const testById = useMemo(() => new Map(tests.map((t) => [t.id, t])), [tests]);

  const statusOptions = useMemo(
    () => [
      { label: 'All statuses', value: 'all' },
      ...ALL_JOB_STATUSES.map((st) => ({ label: JOB_STATUS_NAMES[st], value: String(st) })),
    ],
    []
  );
  const testOptions = useMemo(
    () => [
      { label: 'All tests', value: 'all' },
      { label: 'One-off runs (dry runs)', value: EMPTY_GUID },
      ...tests.map((t) => ({ label: t.name, value: t.id })),
    ],
    [tests]
  );

  const testName = (job: ObiconTestJob) =>
    job.testId === EMPTY_GUID ? 'One-off run' : testById.get(job.testId)?.name ?? job.testId.slice(0, 8);

  const filtered = (jobs ?? [])
    .filter((j) => statusFilter === 'all' || j.status === Number(statusFilter))
    .filter((j) => testFilter === 'all' || j.testId === testFilter)
    .slice(0, MAX_ROWS);

  return (
    <PluginPage>
      <div data-testid={testIds.results.container}>
        <h2>Results</h2>

        {error && (
          <Alert severity="error" title="Could not load results">
            {error}
          </Alert>
        )}

        {!jobs && !error && <LoadingPlaceholder text="Loading results…" />}

        {jobs && (
          <>
            <div className={s.controls}>
              <Select
                aria-label="Status filter"
                options={statusOptions}
                value={statusFilter}
                onChange={(v) => setStatusFilter(v.value!)}
                width={20}
              />
              <Select
                aria-label="Test filter"
                options={testOptions}
                value={testFilter}
                onChange={(v) => setTestFilter(v.value!)}
                width={24}
              />
              <Button variant="secondary" size="sm" icon="sync" onClick={() => load()}>
                Refresh
              </Button>
            </div>

            <div data-testid={testIds.results.table}>
              <SimpleTable
                columns={[
                  { id: 'created', header: 'Created' },
                  { id: 'test', header: 'Test' },
                  { id: 'type', header: 'Type' },
                  { id: 'target', header: 'Target' },
                  { id: 'node', header: 'Node' },
                  { id: 'status', header: 'Status' },
                  { id: 'duration', header: 'Duration' },
                  { id: 'details' },
                ]}
                rows={filtered.map((j) => [
                  <button
                    key={`${j.id}:created`}
                    type="button"
                    className={s.rowToggle}
                    onClick={() => setExpanded(expanded === j.id ? null : j.id)}
                  >
                    <Icon name={expanded === j.id ? 'angle-down' : 'angle-right'} />
                    {timeAgo(j.createdAt)}
                  </button>,
                  <span key={`${j.id}:test`}>{testName(j)}</span>,
                  <span key={`${j.id}:type`}>{TEST_TYPE_NAMES[j.testType]}</span>,
                  <span key={`${j.id}:target`}>{j.target}</span>,
                  <span key={`${j.id}:node`}>{nodeNames.get(j.nodeId) ?? j.nodeId.slice(0, 8)}</span>,
                  <Badge
                    key={`${j.id}:status`}
                    color={STATUS_COLORS[j.status]}
                    text={JOB_STATUS_NAMES[j.status] + (j.status === TestJobStatus.Completed && j.success === false ? ' (failed check)' : '')}
                  />,
                  <span key={`${j.id}:duration`}>{j.durationMs === null ? '—' : `${j.durationMs} ms`}</span>,
                  j.id === expanded ? (
                    <div key={`${j.id}:details`} className={s.details}>
                      {j.errorMessage && <div className={s.errorText}>{j.errorMessage}</div>}
                      {j.output && <pre className={s.pre}>{j.output}</pre>}
                      {j.metrics && Object.keys(j.metrics).length > 0 && (
                        <div className={s.metrics}>
                          {Object.entries(j.metrics).map(([k, v]) => (
                            <div key={k}>
                              <strong>{k}</strong>: {String(v)}
                            </div>
                          ))}
                        </div>
                      )}
                      {!j.output && !j.errorMessage && <span className={s.muted}>No output recorded</span>}
                    </div>
                  ) : null,
                ])}
                emptyText={jobs.length === 0 ? 'No test jobs have run yet' : 'No jobs match the filters'}
              />
            </div>
            {jobs.length > MAX_ROWS && (
              <div className={s.muted}>Showing the {MAX_ROWS} most recent of {jobs.length} jobs</div>
            )}
          </>
        )}
      </div>
    </PluginPage>
  );
}

export default ResultsPage;

const getStyles = (theme: GrafanaTheme2) => ({
  controls: css`
    label: results-controls;
    display: flex;
    gap: ${theme.spacing(2)};
    align-items: center;
    margin-bottom: ${theme.spacing(2)};
  `,
  rowToggle: css`
    label: results-row-toggle;
    background: none;
    border: none;
    color: ${theme.colors.text.primary};
    cursor: pointer;
    padding: 0;
    font-size: ${theme.typography.bodySmall.fontSize};
    display: inline-flex;
    align-items: center;
    gap: ${theme.spacing(0.5)};
  `,
  details: css`
    label: results-details;
    display: grid;
    gap: ${theme.spacing(0.75)};
    font-size: ${theme.typography.bodySmall.fontSize};
    padding: ${theme.spacing(0.5)} 0;
    max-width: 900px;
  `,
  pre: css`
    label: results-pre;
    white-space: pre-wrap;
    word-break: break-word;
    max-height: 240px;
    overflow: auto;
    background: ${theme.colors.background.secondary};
    padding: ${theme.spacing(1)};
    border-radius: ${theme.shape.radius.default};
    font-size: ${theme.typography.bodySmall.fontSize};
    margin: 0;
  `,
  metrics: css`
    label: results-metrics;
    display: grid;
    gap: ${theme.spacing(0.25)};
  `,
  errorText: css`
    label: results-error-text;
    color: ${theme.colors.error.text};
  `,
  muted: css`
    label: results-muted;
    color: ${theme.colors.text.secondary};
    margin-top: ${theme.spacing(1)};
  `,
});
