import React from 'react';
import { Route, Routes } from 'react-router-dom';
import { AppRootProps } from '@grafana/data';
import { ROUTES } from '../../constants';

const OverviewPage = React.lazy(() => import('../../pages/OverviewPage'));
const NodesPage = React.lazy(() => import('../../pages/NodesPage'));
const TestsPage = React.lazy(() => import('../../pages/TestsPage'));
const ResultsPage = React.lazy(() => import('../../pages/ResultsPage'));
const LogsPage = React.lazy(() => import('../../pages/LogsPage'));
const ServerPage = React.lazy(() => import('../../pages/ServerPage'));

function App(props: AppRootProps) {
  return (
    <Routes>
      <Route path={ROUTES.Nodes} element={<NodesPage meta={props.meta} />} />
      <Route path={ROUTES.Tests} element={<TestsPage meta={props.meta} />} />
      <Route path={ROUTES.Results} element={<ResultsPage />} />
      <Route path={ROUTES.Logs} element={<LogsPage meta={props.meta} />} />
      <Route path={ROUTES.Server} element={<ServerPage />} />

      {/* Default page */}
      <Route path="*" element={<OverviewPage meta={props.meta} />} />
    </Routes>
  );
}

export default App;
