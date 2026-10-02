import React, { ChangeEvent, useState } from 'react';
import { lastValueFrom } from 'rxjs';
import { css } from '@emotion/css';
import {
  AppPluginMeta,
  DataSourceInstanceSettings,
  GrafanaTheme2,
  PluginConfigPageProps,
  PluginMeta,
} from '@grafana/data';
import { DataSourcePicker, getBackendSrv } from '@grafana/runtime';
import { Alert, Button, Field, FieldSet, Input, SecretInput, useStyles2 } from '@grafana/ui';
import { testIds } from '../testIds';
import type { ObiconAppSettings } from '../../types';
import { DEFAULT_AUTH_HEADER } from '../../constants';

type State = {
  // Base URL of the Obicon server API.
  serverUrl: string;
  // Value of the Authorization header the Obicon API expects (a secret).
  authHeader: string;
  // Whether the secret Authorization header was already stored previously.
  isAuthHeaderSet: boolean;
  // UID of the required Prometheus datasource.
  prometheusUid?: string;
  // UID of the optional logs datasource.
  logsUid?: string;
  // Query for the optional logs datasource.
  logsQuery: string;
};

export interface AppConfigProps extends PluginConfigPageProps<AppPluginMeta<ObiconAppSettings>> {}

const AppConfig = ({ plugin }: AppConfigProps) => {
  const s = useStyles2(getStyles);
  const { enabled, pinned, jsonData, secureJsonFields } = plugin.meta;
  const [state, setState] = useState<State>({
    serverUrl: jsonData?.serverUrl || '',
    authHeader: '',
    isAuthHeaderSet: Boolean(secureJsonFields?.authHeader),
    prometheusUid: jsonData?.prometheusUid || undefined,
    logsUid: jsonData?.logsUid || undefined,
    logsQuery: jsonData?.logsQuery || '',
  });
  const [formError, setFormError] = useState<string | null>(null);


  const onResetAuthHeader = () =>
    setState({
      ...state,
      authHeader: '',
      isAuthHeaderSet: false,
    });

  const onChange = (event: ChangeEvent<HTMLInputElement>) => {
    setState({
      ...state,
      [event.target.name]: event.target.value,
    });
  };

  const onPrometheusChange = (ds: DataSourceInstanceSettings) =>
    setState({
      ...state,
      prometheusUid: ds.uid,
    });

  const onLogsChange = (ds: DataSourceInstanceSettings) =>
    setState({
      ...state,
      logsUid: ds.uid,
    });

  const onLogsClear = () =>
    setState({
      ...state,
      logsUid: undefined,
    });

  const onSubmit = () => {
    const missing: string[] = [];
    if (!state.serverUrl) {
      missing.push('Server URL');
    } else if (!/^https?:\/\//.test(state.serverUrl)) {
      setFormError('Server URL must start with http:// or https://');
      return;
    }
    if (!state.isAuthHeaderSet && !state.authHeader) {
      missing.push('API Authorization header');
    }
    if (!state.prometheusUid) {
      missing.push('Prometheus datasource');
    }
    if (missing.length > 0) {
      setFormError(`Required before saving: ${missing.join(', ')}`);
      return;
    }
    setFormError(null);

    updatePluginAndReload(plugin.meta.id, {
      enabled,
      pinned,
      jsonData: {
        serverUrl: state.serverUrl,
        prometheusUid: state.prometheusUid,
        logsUid: state.logsUid,
        logsQuery: state.logsQuery,
      },
      // The secret cannot be queried later by the frontend, so we only send
      // it when it was (re-)entered; otherwise we keep the stored one.
      secureJsonData: state.isAuthHeaderSet
        ? undefined
        : {
            authHeader: state.authHeader,
          },
    });
  };

  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        onSubmit();
      }}
    >
      {formError && (
        <Alert severity="error" title="Cannot save settings">
          {formError}
        </Alert>
      )}

      <FieldSet label="Obicon server">
        <Field
          label="Server URL"
          description="Base URL of the Obicon server API as reachable from Grafana, e.g. http://obicon:5000"
        >
          <Input
            width={60}
            name="serverUrl"
            id="config-server-url"
            data-testid={testIds.appConfig.serverUrl}
            value={state.serverUrl}
            placeholder="http://obicon:5000"
            onChange={onChange}
          />
        </Field>

        <Field
          label="API Authorization header"
          description={`Value of the Authorization header the Obicon API expects. Server default: "${DEFAULT_AUTH_HEADER}"`}
          className={s.marginTop}
        >
          <SecretInput
            width={60}
            id="config-auth-header"
            data-testid={testIds.appConfig.authHeader}
            name="authHeader"
            value={state.authHeader}
            isConfigured={state.isAuthHeaderSet}
            placeholder={DEFAULT_AUTH_HEADER}
            onChange={onChange}
            onReset={onResetAuthHeader}
          />
        </Field>
      </FieldSet>

      <FieldSet label="Datasources">
        <Field label="Prometheus datasource" description="Scrapes the Obicon server's /metrics endpoint. Required.">
          <DataSourcePicker
            inputId="config-prometheus"
            data-testid={testIds.appConfig.prometheus}
            metrics
            current={state.prometheusUid}
            onChange={onPrometheusChange}
            width={60}
          />
        </Field>

        <Field
          label="Logs datasource (optional)"
          description="When set, the plugin can show log data on the Logs page."
          className={s.marginTop}
        >
          <DataSourcePicker
            inputId="config-logs"
            data-testid={testIds.appConfig.logs}
            logs
            current={state.logsUid}
            onChange={onLogsChange}
            onClear={onLogsClear}
            width={60}
          />
        </Field>

        {state.logsUid && (
          <Field
            label="Logs query (optional)"
            description={'Query for the logs datasource, e.g. {job="obicon"}. Shown on the Logs page.'}
            className={s.marginTop}
          >
            <Input
              width={60}
              name="logsQuery"
              id="config-logs-query"
              data-testid={testIds.appConfig.logsQuery}
              value={state.logsQuery}
              placeholder={'{job=~".+"}'}
              onChange={onChange}
            />
          </Field>
        )}
      </FieldSet>

      <Button type="submit" data-testid={testIds.appConfig.submit} className={s.marginTop}>
        Save settings
      </Button>
    </form>
  );
};

export default AppConfig;

const getStyles = (theme: GrafanaTheme2) => ({
  marginTop: css`
    margin-top: ${theme.spacing(3)};
  `,
});

const updatePluginAndReload = async (pluginId: string, data: Partial<PluginMeta<ObiconAppSettings>>) => {
  try {
    await updatePlugin(pluginId, data);

    // Reloading the page as the changes made here wouldn't be propagated to the actual plugin otherwise.
    // This is not ideal, however unfortunately currently there is no supported way for updating the plugin state.
    window.location.reload();
  } catch (e) {
    console.error('Error while updating the plugin', e);
  }
};

const updatePlugin = async (pluginId: string, data: Partial<PluginMeta>) => {
  const response = await getBackendSrv().fetch({
    url: `/api/plugins/${pluginId}/settings`,
    method: 'POST',
    data,
  });

  return lastValueFrom(response);
};
