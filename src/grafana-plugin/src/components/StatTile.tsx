import React from 'react';
import { css } from '@emotion/css';
import { GrafanaTheme2 } from '@grafana/data';
import { useStyles2 } from '@grafana/ui';

interface StatTileProps {
  label: string;
  /** Formatted value shown prominently; "n/a" when the metric has no samples. */
  value: string;
  /** Accent color class: 'good', 'warn', or 'bad'. */
  tone?: 'good' | 'warn' | 'bad';
}

/**
 * A compact value tile, similar in spirit to a Grafana stat panel but for a
 * single pre-formatted value.
 */
export function StatTile({ label, value, tone }: StatTileProps) {
  const s = useStyles2(getStyles);

  return (
    <div className={s.tile}>
      <div className={s.label}>{label}</div>
      <div className={tone ? s.valueTone[tone] : s.value}>{value}</div>
    </div>
  );
}

const getStyles = (theme: GrafanaTheme2) => ({
  tile: css`
    label: stat-tile;
    padding: ${theme.spacing(1.5)};
    background: ${theme.colors.background.secondary};
    border: 1px solid ${theme.colors.border.weak};
    border-radius: ${theme.shape.radius.default};
    min-width: 140px;
  `,
  label: css`
    label: stat-tile-label;
    color: ${theme.colors.text.secondary};
    font-size: ${theme.typography.bodySmall.fontSize};
    padding-bottom: ${theme.spacing(0.5)};
  `,
  value: css`
    label: stat-tile-value;
    font-size: ${theme.typography.h3.fontSize};
  `,
  valueTone: {
    good: css`
      label: stat-tile-value-good;
      font-size: ${theme.typography.h3.fontSize};
      color: ${theme.colors.success.text};
    `,
    warn: css`
      label: stat-tile-value-warn;
      font-size: ${theme.typography.h3.fontSize};
      color: ${theme.colors.warning.text};
    `,
    bad: css`
      label: stat-tile-value-bad;
      font-size: ${theme.typography.h3.fontSize};
      color: ${theme.colors.error.text};
    `,
  },
});

/**
 * Formats an ISO timestamp as a short relative time ("2m ago"), or "never"
 * for null. Used for node last-seen values.
 */
export function timeAgo(iso: string | null): string {
  if (!iso) {
    return 'never';
  }
  const seconds = Math.floor((Date.now() - new Date(iso).getTime()) / 1000);
  if (seconds < 5) {
    return 'just now';
  }
  if (seconds < 60) {
    return `${seconds}s ago`;
  }
  if (seconds < 3600) {
    return `${Math.floor(seconds / 60)}m ago`;
  }
  if (seconds < 86400) {
    return `${Math.floor(seconds / 3600)}h ago`;
  }
  return `${Math.floor(seconds / 86400)}d ago`;
}
