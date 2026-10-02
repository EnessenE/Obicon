import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { css } from '@emotion/css';
import { GrafanaTheme2, type AppRootProps } from '@grafana/data';
import {
  Alert,
  Badge,
  Button,
  ConfirmModal,
  Field,
  Input,
  LoadingPlaceholder,
  Modal,
  MultiSelect,
  Select,
  Switch,
  useStyles2,
} from '@grafana/ui';
import { PluginPage } from '@grafana/runtime';
import { obiconApi } from '../api';
import { errText } from '../utils/errors';
import { DEFAULT_FREQUENCY_PRESETS } from '../constants';
import {
  IpVersion,
  IP_VERSION_NAMES,
  isTerminalJobStatus,
  JOB_STATUS_NAMES,
  ObiconPool,
  ObiconRunOnceRequest,
  ObiconTest,
  ObiconTestJob,
  ObiconTestRequest,
  TestJobStatus,
  TEST_TYPE_NAMES,
  TestType,
} from '../types';
import { testIds } from '../components/testIds';
import { SimpleTable } from '../components/SimpleTable';

type Props = { meta: AppRootProps['meta'] };

/**
 * Form state for the create/edit modal. String fields that map to nullable
 * request fields are stored as strings and converted on submit.
 */
interface TestFormState {
  name: string;
  type: TestType;
  target: string;
  nodeIds: string[];
  poolIds: string[];
  frequency: number;
  isActive: boolean;
  ipVersion: IpVersion;
  timeoutSeconds: number;
  expectedStatusCodes: string;
  checkCertificateExpiryDays: string;
  expectedDnsResult: string;
  expectedBodyPattern: string;
  headersJson: string;
  proxyUrl: string;
  cacheBust: boolean;
}

interface DryRunJob extends ObiconTestJob {
  nodeName: string;
}

const emptyForm = (): TestFormState => ({
  name: '',
  type: TestType.Http,
  target: '',
  nodeIds: [],
  poolIds: [],
  frequency: 60,
  isActive: true,
  ipVersion: IpVersion.Any,
  timeoutSeconds: 60,
  expectedStatusCodes: '',
  checkCertificateExpiryDays: '',
  expectedDnsResult: '',
  expectedBodyPattern: '',
  headersJson: '',
  proxyUrl: '',
  cacheBust: false,
});

const formFromTest = (t: ObiconTest): TestFormState => ({
  ...emptyForm(),
  name: t.name,
  type: t.type,
  target: t.target,
  nodeIds: t.nodeIds,
  poolIds: t.poolIds,
  frequency: t.frequency,
  isActive: t.isActive,
  ipVersion: t.ipVersion,
  timeoutSeconds: t.timeoutSeconds,
  expectedStatusCodes: t.expectedStatusCodes ?? '',
  checkCertificateExpiryDays: t.checkCertificateExpiryDays === null ? '' : String(t.checkCertificateExpiryDays),
  expectedDnsResult: t.expectedDnsResult ?? '',
  expectedBodyPattern: t.expectedBodyPattern ?? '',
  headersJson: t.headers ? JSON.stringify(t.headers, null, 2) : '',
  proxyUrl: t.proxyUrl ?? '',
  cacheBust: t.cacheBust,
});

const toRequest = (f: TestFormState): ObiconTestRequest => ({
  name: f.name.trim(),
  type: f.type,
  target: f.target.trim(),
  nodeIds: f.nodeIds,
  poolIds: f.poolIds,
  frequency: f.frequency,
  isActive: f.isActive,
  ipVersion: f.ipVersion,
  timeoutSeconds: f.timeoutSeconds,
  expectedStatusCodes: f.expectedStatusCodes.trim() || null,
  checkCertificateExpiryDays: f.checkCertificateExpiryDays.trim() ? Number(f.checkCertificateExpiryDays) : null,
  expectedDnsResult: f.expectedDnsResult.trim() || null,
  expectedBodyPattern: f.expectedBodyPattern.trim() || null,
  headers: f.headersJson.trim() ? (JSON.parse(f.headersJson) as Record<string, string>) : null,
  proxyUrl: f.proxyUrl.trim() || null,
  cacheBust: f.cacheBust,
});

const toRunOnce = (f: TestFormState): ObiconRunOnceRequest => {
  const req = toRequest(f);
  return {
    type: req.type,
    target: req.target,
    nodeIds: req.nodeIds,
    poolIds: req.poolIds,
    timeoutSeconds: req.timeoutSeconds,
    expectedStatusCodes: req.expectedStatusCodes,
    checkCertificateExpiryDays: req.checkCertificateExpiryDays,
    expectedDnsResult: req.expectedDnsResult,
    expectedBodyPattern: req.expectedBodyPattern,
    headers: req.headers,
    proxyUrl: req.proxyUrl,
    cacheBust: req.cacheBust,
  };
};

function TestsPage(_props: Props) {
  const s = useStyles2(getStyles);
  const [tests, setTests] = useState<ObiconTest[] | null>(null);
  const [nodes, setNodes] = useState<Array<{ label: string; value: string }>>([]);
  const [pools, setPools] = useState<ObiconPool[]>([]);
  const [frequencies, setFrequencies] = useState<number[]>(DEFAULT_FREQUENCY_PRESETS);
  const [error, setError] = useState<string | null>(null);

  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<ObiconTest | null>(null);
  const [form, setForm] = useState<TestFormState>(emptyForm());
  const [formError, setFormError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const [dryRunJobs, setDryRunJobs] = useState<DryRunJob[] | null>(null);
  const [dryRunning, setDryRunning] = useState(false);
  const [pendingDelete, setPendingDelete] = useState<ObiconTest | null>(null);

  const load = useCallback(async () => {
    try {
      const [t, n, p, settings] = await Promise.all([
        obiconApi.getTests(),
        obiconApi.getNodes(),
        obiconApi.getPools(),
        obiconApi.getSettings(),
      ]);
      setTests(t);
      setNodes(n.map((node) => ({ label: node.name, value: node.id })));
      setPools(p);
      const freqSetting = settings.find((x) => x.key === 'FrequencyPresetsSeconds');
      if (freqSetting) {
        const parsed = freqSetting.value
          .split(',')
          .map((v) => Number(v.trim()))
          .filter((v) => Number.isFinite(v) && v > 0);
        if (parsed.length > 0) {
          setFrequencies(parsed);
        }
      }
      setError(null);
    } catch (e) {
      setError(errText(e));
    }
  }, []);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    load();
  }, [load]);

  const poolOptions = useMemo(() => pools.map((p) => ({ label: p.name, value: p.id })), [pools]);
  // Object.keys() of a numeric enum returns strings, which would never match
  // the numeric enum values — build options from the numeric values instead.
  const typeOptions = useMemo(
    () =>
      (Object.values(TestType).filter((v) => typeof v === 'number') as TestType[]).map((t) => ({
        label: TEST_TYPE_NAMES[t],
        value: t,
      })),
    []
  );
  const frequencyOptions = useMemo(() => frequencies.map((f) => ({ label: `${f}s`, value: f })), [frequencies]);
  const ipVersionOptions = useMemo(
    () =>
      (Object.values(IpVersion).filter((v) => typeof v === 'number') as IpVersion[]).map((v) => ({
        label: IP_VERSION_NAMES[v],
        value: v,
      })),
    []
  );

  const isHttpType = form.type === TestType.Http || form.type === TestType.Https;

  const openCreate = () => {
    setEditing(null);
    setForm(emptyForm());
    setFormError(null);
    setDryRunJobs(null);
    setModalOpen(true);
  };

  const openEdit = (t: ObiconTest) => {
    setEditing(t);
    setForm(formFromTest(t));
    setFormError(null);
    setDryRunJobs(null);
    setModalOpen(true);
  };

  const closeModal = () => {
    setModalOpen(false);
    setDryRunJobs(null);
  };

  const submit = async () => {
    setFormError(null);
    try {
      JSON.parse(form.headersJson || '{}');
    } catch {
      setFormError('Headers must be a valid JSON object, e.g. {"X-Token": "secret"}');
      return;
    }
    setSaving(true);
    try {
      if (editing) {
        await obiconApi.updateTest(editing.id, toRequest(form));
      } else {
        await obiconApi.createTest(toRequest(form));
      }
      setModalOpen(false);
      await load();
    } catch (e) {
      setFormError(errText(e));
    } finally {
      setSaving(false);
    }
  };

  const runOnce = async () => {
    setFormError(null);
    setDryRunning(true);
    setDryRunJobs(null);
    try {
      const jobs = await obiconApi.runOnce(toRunOnce(form));
      setDryRunJobs(
        jobs.map((j) => ({ ...j, nodeName: nodes.find((n) => n.value === j.nodeId)?.label ?? j.nodeId.slice(0, 8) }))
      );
    } catch (e) {
      setFormError(errText(e));
    } finally {
      setDryRunning(false);
    }
  };

  // Poll dry-run jobs until they all reach a terminal status.
  useEffect(() => {
    if (!dryRunJobs || dryRunJobs.every((j) => isTerminalJobStatus(j.status))) {
      return;
    }
    const timer = setInterval(async () => {
      const updated = await Promise.all(
        dryRunJobs.map(async (j) => {
          if (isTerminalJobStatus(j.status)) {
            return j;
          }
          try {
            const job = await obiconApi.getJob(j.id);
            return { ...job, nodeName: j.nodeName };
          } catch {
            return j;
          }
        })
      );
      setDryRunJobs(updated);
    }, 1000);
    return () => clearInterval(timer);
  }, [dryRunJobs]);

  const onConfirmDelete = async () => {
    if (!pendingDelete) {
      return;
    }
    await obiconApi.deleteTest(pendingDelete.id);
    setPendingDelete(null);
    await load();
  };

  const onToggle = async (t: ObiconTest) => {
    await obiconApi.toggleTest(t.id);
    await load();
  };

  const onRun = async (t: ObiconTest) => {
    await obiconApi.runTest(t.id);
    await load();
  };

  return (
    <PluginPage>
      <div data-testid={testIds.tests.container}>
        <h2>Tests</h2>

        {error && (
          <Alert severity="error" title="Could not load tests">
            {error}
          </Alert>
        )}

        {!tests && !error && <LoadingPlaceholder text="Loading tests…" />}

        {tests && (
          <>
            <Button data-testid={testIds.tests.create} onClick={openCreate} icon="plus" className={s.marginBottom}>
              New test
            </Button>
            <div data-testid={testIds.tests.table}>
              <SimpleTable
                columns={[
                  { id: 'name', header: 'Name' },
                  { id: 'type', header: 'Type' },
                  { id: 'target', header: 'Target' },
                  { id: 'frequency', header: 'Every' },
                  { id: 'active', header: 'Active' },
                  { id: 'actions', header: '' },
                ]}
                rows={tests.map((t) => [
                  <strong key={`${t.id}:name`}>{t.name}</strong>,
                  <span key={`${t.id}:type`}>{TEST_TYPE_NAMES[t.type]}</span>,
                  <span key={`${t.id}:target`}>{t.target}</span>,
                  <span key={`${t.id}:frequency`}>{t.frequency}s</span>,
                  <Badge key={`${t.id}:active`} color={t.isActive ? 'green' : 'red'} text={t.isActive ? 'Active' : 'Inactive'} />,
                  <span key={`${t.id}:actions`} className={s.actions}>
                    <Button size="sm" variant="secondary" onClick={() => onRun(t)}>
                      Run
                    </Button>
                    <Button size="sm" variant="secondary" onClick={() => openEdit(t)}>
                      Edit
                    </Button>
                    <Button size="sm" variant="secondary" onClick={() => onToggle(t)}>
                      {t.isActive ? 'Disable' : 'Enable'}
                    </Button>
                    <Button size="sm" variant="destructive" onClick={() => setPendingDelete(t)}>
                      Delete
                    </Button>
                  </span>,
                ])}
                emptyText="No tests created yet"
              />
            </div>
          </>
        )}

        <Modal
          isOpen={modalOpen}
          onDismiss={closeModal}
          title={editing ? `Edit test: ${editing.name}` : 'New test'}
          className={s.modal}
        >
          {formError && (
            <Alert severity="error" title="Error">
              {formError}
            </Alert>
          )}

          <Field label="Name" required>
            <Input value={form.name} onChange={(e) => setForm({ ...form, name: e.currentTarget.value })} />
          </Field>
          <div className={s.formRow}>
            <Field label="Type">
              <Select options={typeOptions} value={form.type} onChange={(v) => setForm({ ...form, type: v.value! })} />
            </Field>
            <Field label="Frequency">
              <Select
                options={frequencyOptions}
                value={frequencyOptions.some((f) => f.value === form.frequency) ? form.frequency : frequencies[0]}
                onChange={(v) => setForm({ ...form, frequency: v.value! })}
              />
            </Field>
            <Field label="IP version">
              <Select
                options={ipVersionOptions}
                value={form.ipVersion}
                onChange={(v) => setForm({ ...form, ipVersion: v.value! })}
              />
            </Field>
          </div>
          <Field label="Target" required description="URL/host for HTTP(S)/TCP, hostname for DNS, IP/host for ping/traceroute">
            <Input value={form.target} onChange={(e) => setForm({ ...form, target: e.currentTarget.value })} />
          </Field>
          <div className={s.formRow}>
            <Field label="Timeout (seconds)" required>
              <Input
                type="number"
                value={form.timeoutSeconds}
                onChange={(e) => setForm({ ...form, timeoutSeconds: Number(e.currentTarget.value) })}
              />
            </Field>
            <Field label="Active">
              <Switch value={form.isActive} onChange={(e) => setForm({ ...form, isActive: e.currentTarget.checked })} />
            </Field>
          </div>
          <Field label="Nodes" description="Nodes that run this test">
            <MultiSelect
              options={nodes}
              value={form.nodeIds}
              onChange={(v) => setForm({ ...form, nodeIds: v.map((x) => x.value!) })}
            />
          </Field>
          <Field label="Pools" description="All members of these pools run this test too">
            <MultiSelect
              options={poolOptions}
              value={form.poolIds}
              onChange={(v) => setForm({ ...form, poolIds: v.map((x) => x.value!) })}
            />
          </Field>

          {isHttpType && (
            <>
              <div className={s.formRow}>
                <Field label="Expected status codes" description="e.g. 200-399 or 200,301">
                  <Input
                    value={form.expectedStatusCodes}
                    onChange={(e) => setForm({ ...form, expectedStatusCodes: e.currentTarget.value })}
                  />
                </Field>
                <Field label="Body regex" description="Body must match this regular expression">
                  <Input
                    value={form.expectedBodyPattern}
                    onChange={(e) => setForm({ ...form, expectedBodyPattern: e.currentTarget.value })}
                  />
                </Field>
              </div>
              <div className={s.formRow}>
                <Field label="Proxy URL" description="http(s) proxy for this request, empty = direct">
                  <Input value={form.proxyUrl} onChange={(e) => setForm({ ...form, proxyUrl: e.currentTarget.value })} />
                </Field>
                <Field label="Cache busting">
                  <Switch
                    value={form.cacheBust}
                    onChange={(e) => setForm({ ...form, cacheBust: e.currentTarget.checked })}
                  />
                </Field>
              </div>
              <Field label="Headers (JSON)" description='Custom request headers, e.g. {"Authorization": "Bearer …"}'>
                <textarea
                  className={s.textarea}
                  rows={3}
                  value={form.headersJson}
                  onChange={(e) => setForm({ ...form, headersJson: e.target.value })}
                />
              </Field>
            </>
          )}

          {form.type === TestType.Https && (
            <Field label="Certificate expiry (days)" description="Fail when the TLS certificate expires within this many days, empty = no check">
              <Input
                type="number"
                value={form.checkCertificateExpiryDays}
                onChange={(e) => setForm({ ...form, checkCertificateExpiryDays: e.currentTarget.value })}
              />
            </Field>
          )}

          {form.type === TestType.Dns && (
            <Field label="Expected DNS result" description="The run fails unless this address is among the resolved ones, empty = any">
              <Input
                value={form.expectedDnsResult}
                onChange={(e) => setForm({ ...form, expectedDnsResult: e.currentTarget.value })}
              />
            </Field>
          )}

          <Modal.ButtonRow>
            <Button variant="secondary" onClick={runOnce} disabled={dryRunning}>
              {dryRunning ? 'Running…' : 'Dry run'}
            </Button>
            <Button variant="secondary" onClick={closeModal}>
              Cancel
            </Button>
            <Button onClick={submit} disabled={saving}>
              {saving ? 'Saving…' : editing ? 'Save' : 'Create'}
            </Button>
          </Modal.ButtonRow>

          {dryRunJobs && (
            <div className={s.dryRun}>
              <h4>Dry run results</h4>
              <SimpleTable
                columns={[
                  { id: 'node', header: 'Node' },
                  { id: 'status', header: 'Status' },
                  { id: 'duration', header: 'Duration' },
                ]}
                rows={dryRunJobs.map((j) => [
                  <span key={`${j.id}:node`}>{j.nodeName}</span>,
                  <span key={`${j.id}:status`}>
                    {JOB_STATUS_NAMES[j.status]}
                    {j.status === TestJobStatus.Completed && j.success === false ? ' (failed check)' : ''}
                  </span>,
                  <span key={`${j.id}:duration`}>{j.durationMs === null ? '—' : `${j.durationMs} ms`}</span>,
                ])}
                emptyText="No jobs were created"
              />
              {dryRunJobs
                .filter((j) => j.output || j.errorMessage)
                .map((j) => (
                  <div key={j.id} className={s.dryRunOutput}>
                    <strong>{j.nodeName}</strong>
                    {j.errorMessage && <div className={s.errorText}>{j.errorMessage}</div>}
                    {j.output && <pre className={s.pre}>{j.output}</pre>}
                  </div>
                ))}
            </div>
          )}
        </Modal>
        <ConfirmModal
          isOpen={pendingDelete !== null}
          title="Delete test"
          body={`Delete "${pendingDelete?.name}"? Its scheduled runs stop immediately.`}
          confirmText="Delete"
          onConfirm={onConfirmDelete}
          onDismiss={() => setPendingDelete(null)}
        />
      </div>
    </PluginPage>
  );
}

export default TestsPage;

const getStyles = (theme: GrafanaTheme2) => ({
  marginBottom: css`
    margin-bottom: ${theme.spacing(2)};
  `,
  actions: css`
    label: tests-actions;
    display: inline-flex;
    gap: ${theme.spacing(0.5)};
  `,
  formRow: css`
    label: tests-form-row;
    display: flex;
    gap: ${theme.spacing(2)};
  `,
  textarea: css`
    label: tests-headers-textarea;
    width: 100%;
    min-height: 60px;
    background: ${theme.colors.background.primary};
    color: ${theme.colors.text.primary};
    border: 1px solid ${theme.colors.border.strong};
    border-radius: ${theme.shape.radius.default};
    padding: ${theme.spacing(0.5)} ${theme.spacing(1)};
    font-family: ${theme.typography.fontFamilyMonospace};
    font-size: ${theme.typography.bodySmall.fontSize};
  `,
  modal: css`
    label: tests-modal;
    width: 640px;
    max-width: 90vw;
  `,
  dryRun: css`
    label: tests-dry-run;
    margin-top: ${theme.spacing(2)};
    border-top: 1px solid ${theme.colors.border.weak};
    padding-top: ${theme.spacing(2)};
  `,
  dryRunOutput: css`
    label: tests-dry-run-output;
    margin-top: ${theme.spacing(1)};
  `,
  pre: css`
    label: tests-dry-run-pre;
    white-space: pre-wrap;
    word-break: break-word;
    max-height: 200px;
    overflow: auto;
    background: ${theme.colors.background.secondary};
    padding: ${theme.spacing(1)};
    border-radius: ${theme.shape.radius.default};
    font-size: ${theme.typography.bodySmall.fontSize};
  `,
  errorText: css`
    label: tests-error-text;
    color: ${theme.colors.error.text};
  `,
});
