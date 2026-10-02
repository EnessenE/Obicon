import pluginJson from './plugin.json';
import type { ObiconAppSettings } from './types';

export const PLUGIN_BASE_URL = `/a/${pluginJson.id}`;
export const PLUGIN_ID = pluginJson.id;

/** Path of the plugin configuration page, for links from the app pages. */
export const PLUGIN_CONFIG_PATH = `/plugins/${pluginJson.id}`;

/** Obicon API auth header value when none was configured (the server default). */
export const DEFAULT_AUTH_HEADER = 'uwu';

export enum ROUTES {
  Overview = 'overview',
  Nodes = 'nodes',
  Tests = 'tests',
  Results = 'results',
  Logs = 'logs',
  Server = 'server',
}

/** Default frequency presets, used until the server's own setting is fetched. */
export const DEFAULT_FREQUENCY_PRESETS = [10, 30, 60, 120, 300, 600, 3600];

/** Extracts the Obicon settings from the app plugin metadata. */
export function getObiconSettings(meta: { jsonData?: unknown } | undefined): ObiconAppSettings {
  return (meta?.jsonData as ObiconAppSettings | undefined) ?? {};
}
