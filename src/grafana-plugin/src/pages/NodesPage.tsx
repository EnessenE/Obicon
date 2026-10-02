import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { css } from '@emotion/css';
import { GrafanaTheme2, type AppRootProps } from '@grafana/data';
import {
  Alert,
  Badge,
  Button,
  Field,
  Icon,
  Input,
  LoadingPlaceholder,
  Modal,
  Select,
  Switch,
  Tooltip,
  useStyles2,
} from '@grafana/ui';
import { PluginPage } from '@grafana/runtime';
import { obiconApi } from '../api';
import { errText } from '../utils/errors';
import type { ObiconNode, ObiconNodeStatus, ObiconPool } from '../types';
import { testIds } from '../components/testIds';
import { SimpleTable } from '../components/SimpleTable';
import { timeAgo } from '../components/StatTile';

type Props = { meta: AppRootProps['meta'] };

/**
 * A node merged with its live connection status.
 */
interface NodeRow extends ObiconNode {
  isConnected: boolean;
}

type SortDir = 1 | -1;
type SortField = 'name' | 'version' | 'state' | 'lastSeen' | 'ip' | 'labels';
type StateFilter = 'all' | 'connected' | 'active' | 'inactive';
type SupportFilter = 'all' | 'supported' | 'unsupported' | 'unknown';

const stateRank = (row: NodeRow) => (row.isConnected ? 2 : row.isActive ? 1 : 0);
const stateText = (row: NodeRow) =>
  row.isConnected ? { text: 'Connected', color: 'green' as const } : row.isActive ? { text: 'Active', color: 'blue' as const } : { text: 'Inactive', color: 'red' as const };

function NodesPage(_props: Props) {
  const s = useStyles2(getStyles);
  const [rows, setRows] = useState<NodeRow[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  const [search, setSearch] = useState('');
  const [stateFilter, setStateFilter] = useState<StateFilter>('all');
  const [supportFilter, setSupportFilter] = useState<SupportFilter>('all');
  const [sort, setSort] = useState<{ field: SortField; dir: SortDir }>({ field: 'name', dir: 1 });

  const [editNode, setEditNode] = useState<NodeRow | null>(null);

  const load = useCallback(async () => {
    try {
      const [nodes, statuses] = await Promise.all([obiconApi.getNodes(), obiconApi.getNodeStatuses()]);
      const statusById = new Map((statuses as ObiconNodeStatus[]).map((st) => [st.id, st]));
      setRows(
        nodes.map((n) => ({
          ...n,
          isConnected: statusById.get(n.id)?.isConnected ?? false,
        }))
      );
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

  const visible = useMemo(() => {
    const q = search.trim().toLowerCase();
    const filtered = (rows ?? []).filter((row) => {
      if (stateFilter === 'connected' && !row.isConnected) {
        return false;
      }
      if (stateFilter === 'active' && (row.isConnected || !row.isActive)) {
        return false;
      }
      if (stateFilter === 'inactive' && (row.isConnected || row.isActive)) {
        return false;
      }
      if (supportFilter === 'supported' && row.versionSupported !== true) {
        return false;
      }
      if (supportFilter === 'unsupported' && row.versionSupported !== false) {
        return false;
      }
      if (supportFilter === 'unknown' && row.versionSupported !== null) {
        return false;
      }
      if (!q) {
        return true;
      }
      const haystack = [row.name, row.version ?? '', row.ipAddress ?? '', row.externalIpv4 ?? '', row.internalIpv4 ?? '', ...row.labels]
        .join(' ')
        .toLowerCase();
      return haystack.includes(q);
    });

    const cmp = (a: NodeRow, b: NodeRow): number => {
      const dir = sort.dir;
      switch (sort.field) {
        case 'name':
          return a.name.localeCompare(b.name) * dir;
        case 'version': {
          const av = a.version ?? '';
          const bv = b.version ?? '';
          return (av === bv ? 0 : av === '' ? 1 : bv === '' ? -1 : av.localeCompare(bv)) * dir;
        }
        case 'state':
          return (stateRank(a) - stateRank(b)) * dir;
        case 'lastSeen': {
          const at = a.lastSeenAt ? new Date(a.lastSeenAt).getTime() : 0;
          const bt = b.lastSeenAt ? new Date(b.lastSeenAt).getTime() : 0;
          return (at - bt) * dir;
        }
        case 'ip':
          return (a.ipAddress ?? '').localeCompare(b.ipAddress ?? '') * dir;
        case 'labels':
          return a.labels.join(',').localeCompare(b.labels.join(',')) * dir;
      }
    };
    return filtered.sort(cmp);
  }, [rows, search, stateFilter, supportFilter, sort]);

  const sortHeader = (field: SortField, label: string) => (
    <button type="button" className={s.sortButton} onClick={() => setSort((cur) => (cur.field === field ? { field, dir: cur.dir === 1 ? -1 : 1 } : { field, dir: 1 }))}>
      {label}
      {sort.field === field && <Icon name={sort.dir === 1 ? 'sort-amount-up' : 'sort-amount-down'} />}
    </button>
  );

  return (
    <PluginPage>
      <div data-testid={testIds.nodes.container}>
        <h2>Nodes</h2>

        {error && (
          <Alert severity="error" title="Could not load nodes">
            {error}
          </Alert>
        )}

        {!rows && !error && <LoadingPlaceholder text="Loading nodes…" />}

        {rows && (
          <>
            <div className={s.controls}>
              <Input
                className={s.search}
                data-testid={testIds.nodes.search}
                placeholder="Search nodes, labels, addresses…"
                value={search}
                onChange={(e) => setSearch(e.currentTarget.value)}
              />
              <Select
                aria-label="State filter"
                options={[
                  { label: 'All states', value: 'all' },
                  { label: 'Connected', value: 'connected' },
                  { label: 'Active', value: 'active' },
                  { label: 'Inactive', value: 'inactive' },
                ]}
                value={stateFilter}
                onChange={(v) => setStateFilter(v.value as StateFilter)}
                width={16}
              />
              <Select
                aria-label="Version filter"
                options={[
                  { label: 'All versions', value: 'all' },
                  { label: 'Supported', value: 'supported' },
                  { label: 'Unsupported', value: 'unsupported' },
                  { label: 'Unknown', value: 'unknown' },
                ]}
                value={supportFilter}
                onChange={(v) => setSupportFilter(v.value as SupportFilter)}
                width={16}
              />
              <Button variant="secondary" size="sm" icon="sync" onClick={() => load()}>
                Refresh
              </Button>
            </div>

            <div data-testid={testIds.nodes.table}>
              <SimpleTable
                columns={[
                  { id: 'name', header: sortHeader('name', 'Name') },
                  { id: 'version', header: sortHeader('version', 'Version') },
                  { id: 'state', header: sortHeader('state', 'State') },
                  { id: 'lastSeen', header: sortHeader('lastSeen', 'Last seen') },
                  { id: 'ip', header: sortHeader('ip', 'Address') },
                  { id: 'labels', header: sortHeader('labels', 'Labels') },
                  { id: 'actions', header: '' },
                ]}
                rows={visible.map((row) => [
                  <button key={`${row.id}:name`} type="button" className={s.nameButton} onClick={() => setEditNode(row)}>
                    {row.name}
                  </button>,
                  <span key={`${row.id}:version`}>
                    {row.version ?? 'unknown'}{' '}
                    {row.versionSupported === false && (
                      <Tooltip content="The server does not support this node version (major.minor mismatch)">
                        <Icon name="exclamation-triangle" className={s.warnIcon} />
                      </Tooltip>
                    )}
                  </span>,
                  <Badge key={`${row.id}:state`} color={stateText(row).color} text={stateText(row).text} />,
                  <span key={`${row.id}:lastSeen`}>{timeAgo(row.lastSeenAt)}</span>,
                  <span key={`${row.id}:ip`}>{row.ipAddress ?? row.externalIpv4 ?? 'unknown'}</span>,
                  <span key={`${row.id}:labels`}>{row.labels.length > 0 ? row.labels.join(', ') : '—'}</span>,
                  <Button key={`${row.id}:edit`} size="sm" variant="secondary" onClick={() => setEditNode(row)}>
                    Details
                  </Button>,
                ])}
                emptyText="No nodes match"
              />
            </div>
          </>
        )}

        {editNode && <NodeDetailsModal node={editNode} onClose={() => setEditNode(null)} onSaved={load} />}
      </div>
    </PluginPage>
  );
}

/**
 * Details + edit dialog for a single node. Auto-enrolled nodes manage their
 * own name and labels (the API returns 409), so their edit fields are locked.
 */
function NodeDetailsModal({ node, onClose, onSaved }: { node: NodeRow; onClose: () => void; onSaved: () => void }) {
  const s = useStyles2(getStyles);
  const [name, setName] = useState(node.name);
  const [labels, setLabels] = useState(node.labels.join(', '));
  const [regenerateToken, setRegenerateToken] = useState(false);
  const [pools, setPools] = useState<ObiconPool[] | null>(null);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [newToken, setNewToken] = useState<string | null>(null);

  useEffect(() => {
    obiconApi
      .getNodePools(node.id)
      .then(setPools)
      .catch(() => setPools([]));
  }, [node.id]);

  const isSelfManaged = node.enrollmentType === 'auto-enrollment';

  const save = async () => {
    setSaving(true);
    setError(null);
    try {
      const updated = await obiconApi.updateNode(node.id, {
        name: name.trim(),
        labels: labels
          .split(',')
          .map((l) => l.trim())
          .filter((l) => l.length > 0),
        regenerateToken,
      });
      if (regenerateToken && updated.authToken) {
        setNewToken(updated.authToken);
      } else {
        onClose();
        onSaved();
      }
    } catch (e) {
      setError(errText(e));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal isOpen onDismiss={onClose} title={`Node: ${node.name}`} className={s.modal}>
      {error && (
        <Alert severity="error" title="Could not save the node">
          {error}
        </Alert>
      )}

      {newToken ? (
        <Alert severity="warning" title="New auth token — shown only once">
          <pre className={s.token}>{newToken}</pre>
          Update the node&apos;s <code>Node__Token</code> with this value; the old token stopped working immediately.
          <Modal.ButtonRow>
            <Button onClick={onClose}>Done</Button>
          </Modal.ButtonRow>
        </Alert>
      ) : (
        <>
          <div className={s.detailGrid}>
            <div>
              <strong>State</strong>
              <div>
                <Badge color={stateText(node).color} text={stateText(node).text} />
              </div>
            </div>
            <div>
              <strong>Last seen</strong>
              <div>{timeAgo(node.lastSeenAt)}</div>
            </div>
            <div>
              <strong>Version</strong>
              <div>
                {node.version ?? 'unknown'}
                {node.versionSupported === false && (
                  <Tooltip content="The server does not support this node version (major.minor mismatch)">
                    <Icon name="exclamation-triangle" className={s.warnIcon} />
                  </Tooltip>
                )}
              </div>
            </div>
            <div>
              <strong>Enrollment</strong>
              <div>{node.enrollmentType}</div>
            </div>
            <div>
              <strong>Connection IP</strong>
              <div>{node.ipAddress ?? 'unknown'}</div>
            </div>
            <div>
              <strong>Internal IPv4</strong>
              <div>{node.internalIpv4 ?? 'unavailable'}</div>
            </div>
            <div>
              <strong>Internal IPv6</strong>
              <div>{node.internalIpv6 ?? 'unavailable'}</div>
            </div>
            <div>
              <strong>External IPv4</strong>
              <div>{node.externalIpv4 ?? 'unavailable'}</div>
            </div>
            <div>
              <strong>External IPv6</strong>
              <div>{node.externalIpv6 ?? 'unavailable'}</div>
            </div>
            <div>
              <strong>Pools</strong>
              <div>{pools === null ? '…' : pools.length > 0 ? pools.map((p) => p.name).join(', ') : '—'}</div>
            </div>
            <div>
              <strong>Labels</strong>
              <div>{node.labels.length > 0 ? node.labels.join(', ') : '—'}</div>
            </div>
            {Object.keys(node.settings).length > 0 && (
              <div className={s.fullWidth}>
                <strong>Reported settings</strong>
                <div className={s.settings}>
                  {Object.entries(node.settings).map(([k, v]) => (
                    <span key={k} className={s.settingChip}>
                      {k}={v}
                    </span>
                  ))}
                </div>
              </div>
            )}
          </div>

          <h4 className={s.editHeader}>Edit</h4>
          {isSelfManaged && (
            <Alert severity="info" title="Self-managed node">
              This node enrolled itself and manages its own name and labels — the server rejects changes to them.
            </Alert>
          )}
          <Field label="Name">
            <Input value={name} onChange={(e) => setName(e.currentTarget.value)} disabled={isSelfManaged} />
          </Field>
          <Field label="Labels" description="Comma-separated">
            <Input value={labels} onChange={(e) => setLabels(e.currentTarget.value)} disabled={isSelfManaged} />
          </Field>
          {!isSelfManaged && (
            <Field
              label="Regenerate auth token"
              description="The old token stops working immediately and any live connection using it is closed"
            >
              <Switch value={regenerateToken} onChange={(e) => setRegenerateToken(e.currentTarget.checked)} />
            </Field>
          )}
          <Modal.ButtonRow>
            <Button variant="secondary" onClick={onClose}>
              Close
            </Button>
            {!isSelfManaged && (
              <Button onClick={save} disabled={saving}>
                {saving ? 'Saving…' : 'Save'}
              </Button>
            )}
          </Modal.ButtonRow>
        </>
      )}
    </Modal>
  );
}

export default NodesPage;

const getStyles = (theme: GrafanaTheme2) => ({
  controls: css`
    label: nodes-controls;
    display: flex;
    gap: ${theme.spacing(2)};
    align-items: center;
    margin-bottom: ${theme.spacing(2)};
    flex-wrap: wrap;
  `,
  search: css`
    label: nodes-search;
    width: 320px;
  `,
  sortButton: css`
    label: nodes-sort-button;
    background: none;
    border: none;
    color: ${theme.colors.text.secondary};
    cursor: pointer;
    padding: 0;
    font-size: ${theme.typography.bodySmall.fontSize};
    display: inline-flex;
    align-items: center;
    gap: ${theme.spacing(0.5)};
  `,
  nameButton: css`
    label: nodes-name-button;
    background: none;
    border: none;
    color: ${theme.colors.text.link};
    cursor: pointer;
    padding: 0;
    font-size: ${theme.typography.body.fontSize};
    font-weight: ${theme.typography.fontWeightMedium};
  `,
  warnIcon: css`
    label: nodes-warn-icon;
    color: ${theme.colors.warning.text};
    margin-left: ${theme.spacing(0.5)};
  `,
  modal: css`
    label: nodes-modal;
    width: 720px;
    max-width: 92vw;
  `,
  detailGrid: css`
    label: nodes-detail-grid;
    display: grid;
    grid-template-columns: repeat(3, 1fr);
    gap: ${theme.spacing(2)};
    margin-bottom: ${theme.spacing(2)};
  `,
  fullWidth: css`
    label: nodes-full-width;
    grid-column: 1 / -1;
  `,
  settings: css`
    label: nodes-settings;
    display: flex;
    flex-wrap: wrap;
    gap: ${theme.spacing(0.5)};
    margin-top: ${theme.spacing(0.5)};
  `,
  settingChip: css`
    label: nodes-setting-chip;
    background: ${theme.colors.background.secondary};
    border: 1px solid ${theme.colors.border.weak};
    border-radius: ${theme.shape.radius.default};
    padding: ${theme.spacing(0.25)} ${theme.spacing(0.75)};
    font-family: ${theme.typography.fontFamilyMonospace};
    font-size: ${theme.typography.bodySmall.fontSize};
  `,
  editHeader: css`
    label: nodes-edit-header;
    margin: ${theme.spacing(3)} 0 ${theme.spacing(1.5)};
  `,
  token: css`
    label: nodes-token;
    white-space: pre-wrap;
    word-break: break-all;
    background: ${theme.colors.background.secondary};
    padding: ${theme.spacing(1)};
    border-radius: ${theme.shape.radius.default};
    font-size: ${theme.typography.bodySmall.fontSize};
    margin: ${theme.spacing(1)} 0;
  `,
});
