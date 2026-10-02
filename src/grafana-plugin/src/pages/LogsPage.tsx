import React, { useCallback, useEffect, useState } from 'react';
import { css } from '@emotion/css';
import { GrafanaTheme2, type AppRootProps } from '@grafana/data';
import { Alert, Button, Field, Input, LoadingPlaceholder, useStyles2 } from '@grafana/ui';
import { PluginPage } from '@grafana/runtime';
import { getObiconSettings, PLUGIN_CONFIG_PATH } from '../constants';
import { errText } from '../utils/errors';
import { testIds } from '../components/testIds';
import { framesToLogLines, runExprQuery, type LogLine } from '../utils/query';

type Props = { meta: AppRootProps['meta'] };

const RANGE_OPTIONS = [
  { label: 'Last 15 minutes', ms: 15 * 60 * 1000 },
  { label: 'Last 1 hour', ms: 60 * 60 * 1000 },
  { label: 'Last 6 hours', ms: 6 * 60 * 60 * 1000 },
  { label: 'Last 24 hours', ms: 24 * 60 * 60 * 1000 },
];

const DEFAULT_QUERY = '{job=~".+"}';

function LogsPage({ meta }: Props) {
  const s = useStyles2(getStyles);
  const settings = getObiconSettings(meta);

  const [query, setQuery] = useState(settings.logsQuery || DEFAULT_QUERY);
  const [rangeMs, setRangeMs] = useState(60 * 60 * 1000);
  const [lines, setLines] = useState<LogLine[] | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!settings.logsUid) {
      return;
    }
    setLoading(true);
    try {
      const to = Date.now();
      const frames = await runExprQuery(settings.logsUid, query, to - rangeMs, to);
      setLines(framesToLogLines(frames));
      setError(null);
    } catch (e) {
      setError(errText(e));
    } finally {
      setLoading(false);
    }
  }, [settings.logsUid, query, rangeMs]);

  useEffect(() => {
    if (settings.logsUid) {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      load();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [settings.logsUid]);

  if (!settings.logsUid) {
    return (
      <PluginPage>
        <div data-testid={testIds.logs.container}>
          <h2>Logs</h2>
          <Alert severity="info" title="No logs datasource configured">
            Add a logs datasource (e.g. Loki) on the <a href={PLUGIN_CONFIG_PATH}>plugin configuration page</a> to show
            log data here.
          </Alert>
        </div>
      </PluginPage>
    );
  }

  return (
    <PluginPage>
      <div data-testid={testIds.logs.container}>
        <h2>Logs</h2>

        <div className={s.controls}>
          <Field label="Query">
            <Input
              width={48}
              value={query}
              placeholder="{job=…}"
              onChange={(e) => setQuery(e.currentTarget.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter') {
                  load();
                }
              }}
            />
          </Field>
          <Button variant="secondary" icon="sync" onClick={() => load()}>
            Refresh
          </Button>
        </div>
        <div className={s.rangeOptions}>
          {RANGE_OPTIONS.map((r) => (
            <Button key={r.ms} size="sm" variant={rangeMs === r.ms ? 'primary' : 'secondary'} onClick={() => setRangeMs(r.ms)}>
              {r.label.replace('Last ', '')}
            </Button>
          ))}
        </div>

        {error && (
          <Alert severity="error" title="Logs query failed">
            {error}
          </Alert>
        )}
        {loading && <LoadingPlaceholder text="Loading logs…" />}

        {lines && (
          <div className={s.logList}>
            {lines.length === 0 && <div className={s.empty}>No log lines matched.</div>}
            {lines.map((l, i) => (
              <div key={i} className={s.logLine}>
                {l.labels && <span className={s.labels}>{l.labels}</span>}
                {l.time && <span className={s.time}>{l.time}</span>}
                <span className={s.text}>{l.line}</span>
              </div>
            ))}
          </div>
        )}
      </div>
    </PluginPage>
  );
}

export default LogsPage;

const getStyles = (theme: GrafanaTheme2) => ({
  controls: css`
    label: logs-controls;
    display: flex;
    gap: ${theme.spacing(2)};
    align-items: end;
  `,
  rangeOptions: css`
    label: logs-range-options;
    display: flex;
    gap: ${theme.spacing(0.5)};
    margin: ${theme.spacing(1)} 0 ${theme.spacing(2)};
  `,
  logList: css`
    label: logs-list;
    display: grid;
    gap: 2px;
  `,
  logLine: css`
    label: logs-line;
    display: flex;
    gap: ${theme.spacing(1)};
    font-family: ${theme.typography.fontFamilyMonospace};
    font-size: ${theme.typography.bodySmall.fontSize};
    padding: ${theme.spacing(0.25)} ${theme.spacing(0.5)};
    border-radius: ${theme.shape.radius.default};
    background: ${theme.colors.background.secondary};
    word-break: break-word;
  `,
  labels: css`
    label: logs-labels;
    color: ${theme.colors.text.secondary};
    white-space: nowrap;
  `,
  time: css`
    label: logs-time;
    color: ${theme.colors.text.secondary};
    white-space: nowrap;
  `,
  text: css`
    label: logs-text;
    white-space: pre-wrap;
  `,
  empty: css`
    label: logs-empty;
    color: ${theme.colors.text.secondary};
    padding: ${theme.spacing(2)};
  `,
});
