import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { css } from '@emotion/css';
import { GrafanaTheme2 } from '@grafana/data';
import { Alert, Badge, Button, FieldSet, Icon, Input, LoadingPlaceholder, Tooltip, useStyles2 } from '@grafana/ui';
import { PluginPage } from '@grafana/runtime';
import { obiconApi } from '../api';
import { errText } from '../utils/errors';
import { ObiconServerStats, ObiconSetting } from '../types';
import { testIds } from '../components/testIds';
import { SimpleTable } from '../components/SimpleTable';
import { StatTile } from '../components/StatTile';

/**
 * Draft values for settings the user changed but has not saved yet, keyed by
 * setting key.
 */
type Drafts = Record<string, string>;

const SOURCE_LABELS: Record<string, { text: string; color: 'blue' | 'green' | 'red' | 'purple' }> = {
  Default: { text: 'Default', color: 'blue' },
  Database: { text: 'Database override', color: 'green' },
  'Configuration (forced)': { text: 'Forced by configuration', color: 'red' },
  Derived: { text: 'Derived (read-only)', color: 'purple' },
};

/**
 * The Server page: Obicon's runtime settings (GET/PUT /v1/settings), grouped
 * the same way as the Obicon settings UI, plus the server's stats. Settings
 * forced by configuration or derived (read-only) are shown but cannot be
 * changed here — that's the server's configuration, not a runtime setting.
 */
function ServerPage() {
  const s = useStyles2(getStyles);
  const [stats, setStats] = useState<ObiconServerStats | null>(null);
  const [settings, setSettings] = useState<ObiconSetting[] | null>(null);
  const [drafts, setDrafts] = useState<Drafts>({});
  const [saving, setSaving] = useState<string | null>(null);
  const [saved, setSaved] = useState<string | null>(null);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      const [statsRes, settingsRes] = await Promise.all([obiconApi.getServerStats(), obiconApi.getSettings()]);
      setStats(statsRes);
      setSettings(settingsRes);
      setError(null);
    } catch (e) {
      setError(errText(e));
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  const groups = useMemo(() => {
    const byGroup = new Map<string, ObiconSetting[]>();
    for (const setting of settings ?? []) {
      const group = setting.group || 'General';
      if (!byGroup.has(group)) {
        byGroup.set(group, []);
      }
      byGroup.get(group)!.push(setting);
    }
    return [...byGroup.entries()].sort(([a], [b]) => a.localeCompare(b));
  }, [settings]);

  const valueOf = (setting: ObiconSetting) => drafts[setting.key] ?? setting.value;
  const isEditable = (setting: ObiconSetting) => !setting.isForced && !setting.isReadOnly;
  const isChanged = (setting: ObiconSetting) => drafts[setting.key] !== undefined && drafts[setting.key] !== setting.value;

  const onChange = (setting: ObiconSetting, value: string) => {
    setDrafts((d) => ({ ...d, [setting.key]: value }));
    setErrors((e) => {
      const next = { ...e };
      delete next[setting.key];
      return next;
    });
  };

  const save = async (setting: ObiconSetting) => {
    setSaving(setting.key);
    try {
      await obiconApi.updateSetting(setting.key, valueOf(setting));
      setDrafts((d) => {
        const next = { ...d };
        delete next[setting.key];
        return next;
      });
      setSaved(setting.key);
      setTimeout(() => setSaved((k) => (k === setting.key ? null : k)), 2000);
      await load();
    } catch (e) {
      setErrors((prev) => ({ ...prev, [setting.key]: errText(e) }));
    } finally {
      setSaving(null);
    }
  };

  return (
    <PluginPage>
      <div data-testid={testIds.server.container}>
        <h2>Server</h2>

        {error && (
          <Alert severity="error" title="Could not load the Obicon server">
            {error}
          </Alert>
        )}

        {!settings && !error && <LoadingPlaceholder text="Loading server settings…" />}

        {stats && (
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 12, marginBottom: 24 }}>
            <StatTile label="Server version" value={stats.version} />
            <StatTile label="Uptime" value={stats.uptime} />
            <StatTile label="Connected nodes" value={`${stats.connectedNodes}/${stats.totalNodes}`} />
            <StatTile label="Active tests" value={`${stats.activeTests}/${stats.totalTests}`} />
            <StatTile label="Jobs in queue" value={String(stats.queuedJobs + stats.runningJobs)} />
          </div>
        )}

        {settings && (
          <>
            {groups.map(([group, groupSettings]) => (
              <FieldSet key={group} label={group} className={s.fieldSet}>
                <SimpleTable
                  columns={[
                    { id: 'key', header: 'Setting' },
                    { id: 'value', header: 'Value' },
                    { id: 'source', header: 'Source' },
                    { id: 'actions', header: '' },
                  ]}
                  rows={groupSettings.map((setting) => [
                    <span key={`${setting.key}:key`} className={s.settingKey}>
                      <strong>{setting.key}</strong>
                      <div className={s.description}>{setting.description}</div>
                    </span>,
                    setting.isReadOnly || setting.isForced ? (
                      <span key={`${setting.key}:value`} className={s.fixedValue}>
                        {valueOf(setting)}
                      </span>
                    ) : (
                      <span key={`${setting.key}:value`}>
                        <Input
                          value={valueOf(setting)}
                          onChange={(e) => onChange(setting, e.currentTarget.value)}
                          className={s.valueInput}
                        />
                      </span>
                    ),
                    <span key={`${setting.key}:source`}>
                      {SOURCE_LABELS[setting.source] ? (
                        <Badge
                          color={SOURCE_LABELS[setting.source].color}
                          text={SOURCE_LABELS[setting.source].text}
                        />
                      ) : (
                        <Badge color="blue" text={setting.source} />
                      )}
                    </span>,
                    <span key={`${setting.key}:actions`} className={s.actions}>
                      {errors[setting.key] && <span className={s.errorText}>{errors[setting.key]}</span>}
                      {saved === setting.key && (
                        <span className={s.savedText}>
                          <Icon name="check" /> Saved
                        </span>
                      )}
                      {isEditable(setting) && isChanged(setting) && (
                        <Button
                          size="sm"
                          onClick={() => save(setting)}
                          disabled={saving === setting.key}
                        >
                          {saving === setting.key ? 'Saving…' : 'Save'}
                        </Button>
                      )}
                      {setting.isForced && (
                        <Tooltip content="This setting is forced by the server's configuration and cannot be changed at runtime">
                          <Icon name="lock" className={s.mutedIcon} />
                        </Tooltip>
                      )}
                    </span>,
                  ])}
                />
              </FieldSet>
            ))}
          </>
        )}
      </div>
    </PluginPage>
  );
}

export default ServerPage;

const getStyles = (theme: GrafanaTheme2) => ({
  fieldSet: css`
    label: server-fieldset;
    margin-bottom: ${theme.spacing(3)};
  `,
  settingKey: css`
    label: server-setting-key;
    min-width: 260px;
    display: block;
  `,
  description: css`
    label: server-setting-description;
    color: ${theme.colors.text.secondary};
    font-size: ${theme.typography.bodySmall.fontSize};
    max-width: 360px;
  `,
  fixedValue: css`
    label: server-fixed-value;
    font-family: ${theme.typography.fontFamilyMonospace};
  `,
  valueInput: css`
    label: server-value-input;
    width: 320px;
  `,
  actions: css`
    label: server-actions;
    display: inline-flex;
    gap: ${theme.spacing(1)};
    align-items: center;
  `,
  errorText: css`
    label: server-error-text;
    color: ${theme.colors.error.text};
    font-size: ${theme.typography.bodySmall.fontSize};
  `,
  savedText: css`
    label: server-saved-text;
    color: ${theme.colors.success.text};
    font-size: ${theme.typography.bodySmall.fontSize};
    display: inline-flex;
    align-items: center;
    gap: ${theme.spacing(0.25)};
  `,
  mutedIcon: css`
    label: server-muted-icon;
    color: ${theme.colors.text.secondary};
  `,
});
