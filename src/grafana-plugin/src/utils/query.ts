import { toDataFrame, type DataFrame, type DataQueryRequest, type DataQueryResponse } from '@grafana/data';
import { getDataSourceSrv } from '@grafana/runtime';
import { lastValueFrom, type Observable } from 'rxjs';

/**
 * Runs a single expression query against a datasource and returns the result
 * as data frames. Used for the Prometheus metrics and the logs view; the
 * query model ({ expr, refId }) matches Prometheus and Loki.
 */
export async function runExprQuery(uid: string, expr: string, fromMs: number, toMs: number): Promise<DataFrame[]> {
  const ds = await getDataSourceSrv().get(uid);
  const request = {
    from: String(fromMs),
    to: String(toMs),
    range: {
      from: new Date(fromMs),
      to: new Date(toMs),
      raw: { from: `now-${Math.round((toMs - fromMs) / 1000)}s`, to: 'now' },
    },
    targets: [{ expr, refId: 'A', queryType: 'range' }],
  } as unknown as DataQueryRequest;

  const res = await lastValueFrom(
    ds.query(request) as unknown as Observable<DataQueryResponse>
  );
  return res.data.map(toDataFrame);
}

/**
 * Returns the latest non-null value of the first numeric field across the
 * frames, or null when there is none (e.g. the series has no samples yet).
 */
export function latestNumber(frames: DataFrame[]): number | null {
  for (const frame of frames) {
    for (const field of frame.fields) {
      if (field.type !== 'number') {
        continue;
      }
      const values = field.values as unknown[];
      for (let i = values.length - 1; i >= 0; i--) {
        const v = values[i];
        if (v !== null && v !== undefined && !Number.isNaN(v)) {
          return v as number;
        }
      }
    }
  }
  return null;
}

/**
 * One log line extracted from a logs datasource query result.
 */
export interface LogLine {
  /** ISO timestamp of the entry, empty when the frame had no time field. */
  time: string;
  /** The rendered log line. */
  line: string;
  /** The stream labels, empty when the frame had none. */
  labels: string;
}

/**
 * Extracts log lines from logs query frames. Works with the frame layout of
 * Loki (a string field named "line" or "body", a time field, and "labels"),
 * but does not hard-require it.
 */
export function framesToLogLines(frames: DataFrame[]): LogLine[] {
  const lines: LogLine[] = [];
  for (const frame of frames) {
    const lineField = frame.fields.find((f) =>
      ['line', 'body', 'msg', 'message'].some((name) => f.name.toLowerCase().startsWith(name))
    );
    if (!lineField) {
      continue;
    }
    const timeField = frame.fields.find((f) => f.type === 'time');
    const labelsField = frame.fields.find((f) => f.name.toLowerCase() === 'labels');

    const count = (lineField.values as unknown[]).length;
    for (let i = 0; i < count; i++) {
      lines.push({
        time: timeField
          ? new Date((timeField.values as number[])[i]).toISOString()
          : '',
        line: String((lineField.values as unknown[])[i] ?? ''),
        labels: labelsField ? String((labelsField.values as unknown[])[i] ?? '') : '',
      });
    }
  }
  lines.sort((a, b) => b.time.localeCompare(a.time));
  return lines.slice(0, 500);
}
