import type { AppPluginMeta } from '@grafana/data';

/**
 * Plugin configuration, stored in the app plugin's jsonData.
 */
export interface ObiconAppSettings {
  /** Base URL of the Obicon server API, e.g. http://obicon:5000. Required. */
  serverUrl?: string;
  /** UID of the Prometheus datasource scraping the Obicon server. Required. */
  prometheusUid?: string;
  /** UID of an optional logs datasource (e.g. Loki). When set, the Logs page shows log data. */
  logsUid?: string;
  /** Query for the optional logs datasource, e.g. {job="obicon"}. Default: {job=~".+"}. */
  logsQuery?: string;
}

export type ObiconAppMeta = AppPluginMeta<ObiconAppSettings>;

/**
 * A node as returned by GET /v1/nodes.
 */
export interface ObiconNode {
  id: string;
  name: string;
  /** Present only in update responses where regenerateToken was true. */
  authToken?: string;
  isActive: boolean;
  createdAt: string;
  lastSeenAt: string | null;
  enrollmentType: 'manual' | 'auto-enrollment';
  labels: string[];
  version: string | null;
  versionSupported: boolean | null;
  ipAddress: string | null;
  internalIpv4: string | null;
  internalIpv6: string | null;
  externalIpv4: string | null;
  externalIpv6: string | null;
  settings: Record<string, string>;
}

/**
 * Live connection status of a node, from GET /v1/nodes/status.
 */
export interface ObiconNodeStatus {
  id: string;
  name: string;
  isActive: boolean;
  isConnected: boolean;
  lastSeenAt: string | null;
}

/** Body for PUT /v1/nodes/{id}. Auto-enrolled nodes reject this with 409. */
export interface ObiconNodeUpdateRequest {
  name: string;
  labels: string[];
  /** When true, the response carries a new plain auth token (shown once). */
  regenerateToken: boolean;
}

/**
 * A node pool as returned by GET /v1/pools.
 */
export interface ObiconPool {
  id: string;
  name: string;
  description: string | null;
  nodeIds: string[];
  createdAt: string;
}

/** Test types supported by Obicon. Values match the server's TestType enum. */
export enum TestType {
  Ping = 0,
  Traceroute = 1,
  Http = 2,
  Https = 3,
  Tcp = 4,
  Dns = 5,
}

export const TEST_TYPE_NAMES: Record<TestType, string> = {
  [TestType.Ping]: 'Ping',
  [TestType.Traceroute]: 'Traceroute',
  [TestType.Http]: 'HTTP',
  [TestType.Https]: 'HTTPS',
  [TestType.Tcp]: 'TCP',
  [TestType.Dns]: 'DNS',
};

/** IP version selection for a test. Values match the server's IpVersion enum. */
export enum IpVersion {
  Any = 0,
  Ipv4 = 1,
  Ipv6 = 2,
}

export const IP_VERSION_NAMES: Record<IpVersion, string> = {
  [IpVersion.Any]: 'Any',
  [IpVersion.Ipv4]: 'IPv4',
  [IpVersion.Ipv6]: 'IPv6',
};

/** Job statuses. Values match the server's TestJobStatus enum. */
export enum TestJobStatus {
  Queued = 0,
  Assigned = 1,
  Running = 2,
  Completed = 3,
  Failed = 4,
  Timeout = 5,
  NoRun = 6,
}

export const JOB_STATUS_NAMES: Record<TestJobStatus, string> = {
  [TestJobStatus.Queued]: 'Queued',
  [TestJobStatus.Assigned]: 'Assigned',
  [TestJobStatus.Running]: 'Running',
  [TestJobStatus.Completed]: 'Completed',
  [TestJobStatus.Failed]: 'Failed',
  [TestJobStatus.Timeout]: 'Timeout',
  [TestJobStatus.NoRun]: 'NoRun',
};

/** True when the job reached a final state. */
export function isTerminalJobStatus(status: TestJobStatus): boolean {
  return (
    status === TestJobStatus.Completed ||
    status === TestJobStatus.Failed ||
    status === TestJobStatus.Timeout ||
    status === TestJobStatus.NoRun
  );
}

/**
 * A test as returned by the tests API.
 */
export interface ObiconTest {
  id: string;
  name: string;
  type: TestType;
  target: string;
  nodeIds: string[];
  poolIds: string[];
  /** Interval between runs, in seconds. Must be one of the server's FrequencyPresetsSeconds. */
  frequency: number;
  isActive: boolean;
  ipVersion: IpVersion;
  timeoutSeconds: number;
  expectedStatusCodes: string | null;
  checkCertificateExpiryDays: number | null;
  expectedDnsResult: string | null;
  expectedBodyPattern: string | null;
  headers: Record<string, string> | null;
  proxyUrl: string | null;
  cacheBust: boolean;
  createdAt: string;
  updatedAt: string | null;
}

/** Body for POST /v1/tests and PUT /v1/tests/{id}. */
export type ObiconTestRequest = Omit<ObiconTest, 'id' | 'createdAt' | 'updatedAt'>;

/** Body for POST /v1/tests/run-once (dry run). */
export interface ObiconRunOnceRequest {
  type: TestType;
  target: string;
  nodeIds: string[];
  poolIds: string[];
  timeoutSeconds: number;
  expectedStatusCodes: string | null;
  checkCertificateExpiryDays: number | null;
  expectedDnsResult: string | null;
  expectedBodyPattern: string | null;
  headers: Record<string, string> | null;
  proxyUrl: string | null;
  cacheBust: boolean;
}

/**
 * A queued test job, from the queue API. For run-once jobs testId is the empty GUID.
 */
export interface ObiconTestJob {
  id: string;
  testId: string;
  nodeId: string;
  testType: TestType;
  target: string;
  timeoutSeconds: number;
  status: TestJobStatus;
  createdAt: string;
  acknowledgedAt: string | null;
  startedAt: string | null;
  completedAt: string | null;
  success: boolean | null;
  durationMs: number | null;
  output: string | null;
  errorMessage: string | null;
  metrics: Record<string, string | number> | null;
}

/**
 * Aggregated server statistics, from GET /v1/server/stats.
 */
export interface ObiconServerStats {
  version: string;
  totalTests: number;
  activeTests: number;
  totalNodes: number;
  connectedNodes: number;
  queuedJobs: number;
  runningJobs: number;
  completedJobs: number;
  failedJobs: number;
  timedOutJobs: number;
  noRunJobs: number;
  uptime: string;
  timestamp: string;
}

/**
 * A server setting, from GET /v1/settings.
 */
export interface ObiconSetting {
  key: string;
  description: string;
  value: string;
  isForced: boolean;
  isReadOnly: boolean;
  source: string;
  group: string;
}
