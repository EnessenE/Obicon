import { getBackendSrv } from '@grafana/runtime';
import { lastValueFrom } from 'rxjs';
import pluginJson from './plugin.json';
import type {
  ObiconNode,
  ObiconNodeStatus,
  ObiconNodeUpdateRequest,
  ObiconPool,
  ObiconRunOnceRequest,
  ObiconServerStats,
  ObiconSetting,
  ObiconTest,
  ObiconTestJob,
  ObiconTestRequest,
} from './types';

/**
 * Base path of the Obicon server API as reached through Grafana's plugin
 * proxy for app routes (the "obicon" route in plugin.json). Requests to
 * /api/plugin-proxy/<pluginId>/obicon/... are forwarded by the Grafana
 * backend to the configured server URL with the Authorization header from
 * the plugin's secureJsonData — this works for frontend-only plugins (the
 * /resources/* path would require a plugin backend). The browser never talks
 * to the Obicon server directly, so no CORS configuration is needed.
 */
const PROXY_BASE = `/api/plugin-proxy/${pluginJson.id}/obicon`;

async function request<T>(method: 'GET' | 'POST' | 'PUT' | 'DELETE', path: string, data?: unknown): Promise<T> {
  const res = await lastValueFrom(
    getBackendSrv().fetch<T>({
      url: `${PROXY_BASE}${path}`,
      method,
      data: data as never,
    })
  );
  return res.data;
}

/**
 * Client for the Obicon server REST API (see docs/api-spec.md).
 */
export const obiconApi = {
  getNodes: () => request<ObiconNode[]>('GET', '/v1/nodes'),
  getNodeStatuses: () => request<ObiconNodeStatus[]>('GET', '/v1/nodes/status'),
  getNodePools: (id: string) => request<ObiconPool[]>('GET', `/v1/nodes/${id}/pools`),
  updateNode: (id: string, body: ObiconNodeUpdateRequest) =>
    request<ObiconNode>('PUT', `/v1/nodes/${id}`, body),
  getPools: () => request<ObiconPool[]>('GET', '/v1/pools'),
  getSettings: () => request<ObiconSetting[]>('GET', '/v1/settings'),
  updateSetting: (key: string, value: string) =>
    request<ObiconSetting>('PUT', `/v1/settings/${encodeURIComponent(key)}`, { value }),
  getServerStats: () => request<ObiconServerStats>('GET', '/v1/server/stats'),
  getQueue: () => request<ObiconTestJob[]>('GET', '/v1/queue'),

  getTests: () => request<ObiconTest[]>('GET', '/v1/tests'),
  createTest: (test: ObiconTestRequest) => request<ObiconTest>('POST', '/v1/tests', test),
  updateTest: (id: string, test: ObiconTestRequest) => request<ObiconTest>('PUT', `/v1/tests/${id}`, test),
  deleteTest: (id: string) => request<void>('DELETE', `/v1/tests/${id}`),
  toggleTest: (id: string) => request<ObiconTest>('POST', `/v1/tests/${id}/toggle`),
  runTest: (id: string) => request<{ message: string }>('POST', `/v1/tests/${id}/run`),

  runOnce: (body: ObiconRunOnceRequest) => request<ObiconTestJob[]>('POST', '/v1/tests/run-once', body),
  getJob: (id: string) => request<ObiconTestJob>('GET', `/v1/queue/${id}`),
};
