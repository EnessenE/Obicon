import React from 'react';
import { MemoryRouter } from 'react-router-dom';
import { of } from 'rxjs';
import { AppRootProps, PluginType } from '@grafana/data';
import { render, waitFor } from '@testing-library/react';
import App from './App';

jest.mock('@grafana/runtime', () => {
  const actual = jest.requireActual('@grafana/runtime');
  return {
    ...actual,
    getBackendSrv: () => ({
      fetch: () => of({ data: [], status: 200 }),
    }),
    getDataSourceSrv: () => ({
      get: () => Promise.resolve({ query: () => of({ data: [] }) }),
    }),
  };
});

describe('Components/App', () => {
  let props: AppRootProps;

  beforeEach(() => {
    jest.resetAllMocks();

    props = {
      basename: 'a/enessene-obicon-app',
      meta: {
        id: 'enessene-obicon-app',
        name: 'Obicon',
        type: PluginType.app,
        enabled: true,
        jsonData: {},
      },
      query: {},
      path: '',
      onNavChanged: jest.fn(),
    } as unknown as AppRootProps;
  });

  test('renders without an error', async () => {
    const { queryByText } = render(
      <MemoryRouter>
        <App {...props} />
      </MemoryRouter>
    );

    // The default route renders the Overview page, which is lazy loaded
    await waitFor(() => expect(queryByText(/overview/i)).toBeInTheDocument(), { timeout: 2000 });
  });
});
