/// <reference types="vite/client" />

/**
 * Holds the per-session values retrieved from the Tauri host via invoke.
 * Both values are set once during application startup and are immutable thereafter.
 */
export type SessionConfig = {
  apiBaseUrl: string;
  launchToken: string;
};

let resolved: SessionConfig | null = null;

/**
 * Retrieves the launch token and API port from the Tauri host and constructs
 * the base URL for the HTTP client and SignalR connections.
 *
 * Falls back to the Vite dev-server defaults when running outside of a Tauri
 * context (e.g. during local development with `npm run dev`).
 */
export async function resolveSessionConfig(): Promise<SessionConfig> {
  if (resolved !== null) {
    return resolved;
  }

  if ('__TAURI_INTERNALS__' in window) {
    const { invoke } = await import('@tauri-apps/api/core');
    const [launchToken, apiPort] = await Promise.all([
      invoke<string>('get_launch_token'),
      invoke<number>('get_api_port'),
    ]);

    resolved = {
      apiBaseUrl: `http://127.0.0.1:${apiPort}/api/v1`,
      launchToken,
    };
  } else {
    // Local development fallback — use the dev token for browser testing.
    resolved = {
      apiBaseUrl: import.meta.env.VITE_API_BASE_URL ?? 'http://127.0.0.1:5000/api/v1',
      launchToken: 'dev-token-only',
    };
  }

  return resolved;
}

/**
 * Returns the already-resolved session config.
 * Throws if called before `resolveSessionConfig` has completed.
 */
export function getSessionConfig(): SessionConfig {
  if (resolved === null) {
    throw new Error('Session config has not been resolved yet. Call resolveSessionConfig() first.');
  }

  return resolved;
}
