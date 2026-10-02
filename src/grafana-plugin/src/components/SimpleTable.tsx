import React from 'react';
import { css } from '@emotion/css';
import { GrafanaTheme2 } from '@grafana/data';
import { useStyles2 } from '@grafana/ui';

/**
 * Column of a SimpleTable.
 */
export interface SimpleTableColumn {
  /** Column identifier. */
  id: string;
  /** Header content; omit (or empty) for a header-less column. */
  header?: React.ReactNode;
  /** When true, the column stretches to fill the remaining width. */
  grow?: boolean;
}

interface SimpleTableProps {
  columns: SimpleTableColumn[];
  /** Rows of cells, matching the column order; cells are React nodes. */
  rows: React.ReactNode[][];
  /** Optional text shown when there are no rows. */
  emptyText?: string;
  /** Extra class on the table element. */
  className?: string;
}

/**
 * A plain styled table for React-node cells. Used instead of the Grafana
 * Table component because the pages render mixed markup, not data frames.
 */
export function SimpleTable({ columns, rows, emptyText, className }: SimpleTableProps) {
  const s = useStyles2(getStyles);
  const hasHeaders = columns.some((c) => c.header);

  return (
    <table className={`${s.table} ${className ?? ''}`}>
      {hasHeaders && (
        <thead>
          <tr>
            {columns.map((c) => (
              <th key={c.id} className={`${s.headerCell} ${c.grow ? s.grow : ''}`}>
                {c.header ?? ''}
              </th>
            ))}
          </tr>
        </thead>
      )}
      <tbody>
        {rows.length === 0 && emptyText ? (
          <tr>
            <td className={s.emptyCell} colSpan={columns.length}>
              {emptyText}
            </td>
          </tr>
        ) : (
          rows.map((row, i) => (
            <tr key={i} className={s.row}>
              {row.map((cell, j) => (
                <td key={j} className={`${s.cell} ${columns[j]?.grow ? s.grow : ''}`}>
                  {cell}
                </td>
              ))}
            </tr>
          ))
        )}
      </tbody>
    </table>
  );
}

const getStyles = (theme: GrafanaTheme2) => ({
  table: css`
    label: simple-table;
    width: 100%;
    border-collapse: collapse;
    font-size: ${theme.typography.bodySmall.fontSize};
  `,
  row: css`
    label: simple-table-row;
    &:hover {
      background: ${theme.colors.background.secondary};
    }
  `,
  headerCell: css`
    label: simple-table-header;
    text-align: left;
    padding: ${theme.spacing(1)} ${theme.spacing(1.25)};
    border-bottom: 1px solid ${theme.colors.border.strong};
    color: ${theme.colors.text.secondary};
    white-space: nowrap;
  `,
  cell: css`
    label: simple-table-cell;
    padding: ${theme.spacing(1.25)} ${theme.spacing(1.25)};
    border-bottom: 1px solid ${theme.colors.border.weak};
    vertical-align: top;
    word-break: break-word;
  `,
  grow: css`
    label: simple-table-grow;
    width: 100%;
  `,
  emptyCell: css`
    label: simple-table-empty;
    padding: ${theme.spacing(3)};
    text-align: center;
    color: ${theme.colors.text.secondary};
  `,
});
