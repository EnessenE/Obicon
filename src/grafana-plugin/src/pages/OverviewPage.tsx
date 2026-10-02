import React, { useEffect, useState } from 'react';
import { Alert, LoadingPlaceholder } from '@grafana/ui';
import { PluginPage } from '@grafana/runtime';
import type { AppRootProps } from '@grafana/data';
import { obiconApi } from '../api';
import { errText } from '../utils/errors';
import { getObiconSettings, PLUGIN_CONFIG_PATH } from '../constants';
import type { ObiconServerStats } from '../types';
import { testIds } from '../components/testIds';
import { SimpleTable } from '../components/SimpleTable';
import { StatTile } from '../components/StatTile';
import { latestNumber, runExprQuery } from '../utils/query';

type Props = { meta: AppRootProps['meta'] };

const DAY_MS = 24 * 60 * 60 * 1000;

/**
 * Latest result of every test, from the obicon.tests.current_result gauge.
 */
interface CurrentResultRow {
  testName: string;
  testId: string;
  status: string;
}

const CURRENT_RESULT_LABELS: Record<number, string> = {
  [-1]: 'never ran',
  0: 'Queued',
  1: 'Assigned',
  2: 'Running',
  3: 'Completed',
  4: 'Failed',
  5: 'Timeout',
  6: 'NoRun',
};

function OverviewPage({ meta }: Props) {
  const [stats, setStats] = useState<ObiconServerStats | null>(null);
  const [apiError, setApiError] = useState<string | null>(null);
  const [successRate, setSuccessRate] = useState<number | null>(null);
  const [runs24h, setRuns24h] = useState<number | null>(null);
  const [noRuns24h, setNoRuns24h] = useState<number | null>(null);
  const [queued, setQueued] = useState<number | null>(null);
  const [currentResults, setCurrentResults] = useState<CurrentResultRow[]>([]);
  const [promError, setPromError] = useState<string | null>(null);

  const settings = getObiconSettings(meta);

  useEffect(() => {
    let alive = true;
    obiconApi
      .getServerStats()
      .then((s) => alive && setStats(s))
      .catch((e) => alive && setApiError(errText(e)));
    return () => {
      alive = false;
    };
  }, []);

  useEffect(() => {
    if (!settings.prometheusUid) {
      return;
    }
    let alive = true;

    const run = async () => {
      try {
        const to = Date.now();
        const from = to - DAY_MS;
        const [successFrames, runsFrames, noRunFrames, queuedFrames, resultFrames] = await Promise.all([
          runExprQuery(settings.prometheusUid!, `100 * sum(rate(obicon_tests_runs_total{status="success"}[24h])) / sum(rate(obicon_tests_runs_total[24h]))`, from, to),
          runExprQuery(settings.prometheusUid!, `sum(increase(obicon_tests_runs_total[24h]))`, from, to),
          runExprQuery(settings.prometheusUid!, `sum(increase(obicon_server_noruns_total[24h]))`, from, to),
          runExprQuery(settings.prometheusUid!, `sum(obicon_tests_queue_jobs{status=~"Queued|Assigned|Running"})`, from, to),
          runExprQuery(settings.prometheusUid!, `max by (test_name, test_id) (obicon_tests_current_result)`, from, to),
        ]);
        if (!alive) {
          return;
        }
        setSuccessRate(latestNumber(successFrames));
        setRuns24h(latestNumber(runsFrames));
        setNoRuns24h(latestNumber(noRunFrames));
        setQueued(latestNumber(queuedFrames));

        const rows: CurrentResultRow[] = [];
        for (const frame of resultFrames) {
          const nameField = frame.fields.find((f) => f.name === 'test_name');
          const idField = frame.fields.find((f) => f.name === 'test_id');
          const valueField = frame.fields.find((f) => f.type === 'number');
          if (!nameField || !idField || !valueField) {
            continue;
          }
          const names = nameField.values as string[];
          const ids = idField.values as string[];
          const values = valueField.values as number[];
          for (let i = 0; i < names.length; i++) {
            rows.push({
              testName: names[i],
              testId: ids[i],
              status: CURRENT_RESULT_LABELS[values[i]] ?? String(values[i]),
            });
          }
        }
        rows.sort((a, b) => a.testName.localeCompare(b.testName));
        setCurrentResults(rows);
        setPromError(null);
      } catch (e) {
        if (alive) {
          setPromError(errText(e));
        }
      }
    };

    run();
    const timer = setInterval(run, 30000);
    return () => {
      alive = false;
      clearInterval(timer);
    };
  }, [settings.prometheusUid]);

  return (
    <PluginPage>
      <div data-testid={testIds.overview.container}>
        <h2>Overview</h2>

        {!settings.serverUrl && (
          <Alert severity="info" title="Not configured">
            Configure the Obicon server URL and datasources on the{' '}
            <a href={PLUGIN_CONFIG_PATH}>plugin configuration page</a>.
          </Alert>
        )}

        {apiError && (
          <Alert severity="error" title="Could not reach the Obicon server">
            {apiError}
          </Alert>
        )}
        {promError && (
          <Alert severity="error" title="Prometheus query failed">
            {promError}
          </Alert>
        )}

        <div style={{ display: 'flex', flexWrap: 'wrap', gap: 12, marginTop: 16 }}>
          <StatTile label="Test success rate (24h)" value={successRate === null ? 'n/a' : `${successRate.toFixed(1)}%`} tone={successRate === null ? undefined : successRate >= 95 ? 'good' : successRate >= 80 ? 'warn' : 'bad'} />
          <StatTile label="Test runs (24h)" value={runs24h === null ? 'n/a' : String(Math.round(runs24h))} />
          <StatTile label="NoRuns (24h)" value={noRuns24h === null ? 'n/a' : String(Math.round(noRuns24h))} tone={noRuns24h && noRuns24h > 0 ? 'warn' : undefined} />
          <StatTile label="Jobs in queue" value={queued === null ? 'n/a' : String(Math.round(queued))} />
          <StatTile label="Connected nodes" value={stats ? `${stats.connectedNodes}/${stats.totalNodes}` : 'n/a'} />
          <StatTile label="Active tests" value={stats ? `${stats.activeTests}/${stats.totalTests}` : 'n/a'} />
          {stats && <StatTile label="Server version" value={stats.version} />}
          {stats && <StatTile label="Server uptime" value={stats.uptime} />}
        </div>

        {!stats && !apiError && <LoadingPlaceholder text="Loading server stats…" />}

        <h3 style={{ marginTop: 32 }}>Latest result per test</h3>
        <SimpleTable
          columns={[
            { id: 'name', header: 'Test' },
            { id: 'status', header: 'Latest result' },
          ]}
          rows={currentResults.map((r) => [r.testName, r.status])}
          emptyText="No test results exported yet"
        />
      </div>
    </PluginPage>
  );
}

export default OverviewPage;
