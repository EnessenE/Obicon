import React from 'react';
import { of } from 'rxjs';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { PluginType } from '@grafana/data';
import { testIds } from 'components/testIds';
import AppConfig, { AppConfigProps } from './AppConfig';

jest.mock('@grafana/runtime', () => {
  const actual = jest.requireActual('@grafana/runtime');
  return {
    ...actual,
    getBackendSrv: () => ({
      fetch: () => of({ data: {}, status: 200 }),
    }),
    // The real picker needs Grafana core services that jsdom tests don't
    // provide; a stub is enough to assert the page renders it.
    DataSourcePicker: () => <div data-testid="ds-picker-stub" />,
  };
});

describe('Components/AppConfig', () => {
  let props: AppConfigProps;

  beforeEach(() => {
    jest.resetAllMocks();

    props = {
      plugin: {
        meta: {
          id: 'enessene-obicon-app',
          name: 'Obicon',
          type: PluginType.app,
          enabled: true,
          jsonData: {},
        },
      },
      query: {},
    } as unknown as AppConfigProps;
  });

  test('renders the server fields, both datasource pickers, and the save button', () => {
    const plugin = { meta: { ...props.plugin.meta, enabled: false } };

    // @ts-ignore - We don't need to provide `addConfigPage()` and `setChannelSupport()` for these tests
    render(<AppConfig plugin={plugin} query={props.query} />);

    expect(screen.queryByTestId(testIds.appConfig.serverUrl)).toBeInTheDocument();
    expect(screen.queryByTestId(testIds.appConfig.authHeader)).toBeInTheDocument();
    expect(screen.getAllByTestId('ds-picker-stub')).toHaveLength(2);
    expect(screen.queryByRole('button', { name: /save settings/i })).toBeInTheDocument();
  });

  test('saving with missing required settings shows a validation error', async () => {
    const plugin = { meta: { ...props.plugin.meta, enabled: false } };

    // @ts-ignore - see above
    render(<AppConfig plugin={plugin} query={props.query} />);

    await userEvent.click(screen.getByRole('button', { name: /save settings/i }));

    // All three required settings are missing: the error names each of them
    expect(await screen.findByText(/server url/i, { selector: '[role=alert] *' })).toBeInTheDocument();
    expect(screen.getByRole('alert')).toHaveTextContent(/API Authorization header/i);
    expect(screen.getByRole('alert')).toHaveTextContent(/Prometheus datasource/i);
  });
});
