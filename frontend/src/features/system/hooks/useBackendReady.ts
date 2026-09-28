import { useState, useEffect } from 'react';
import { resolveSessionConfig } from '@/services/session';

type ReadyState = 'loading' | 'ready' | 'error';

const POLL_INTERVAL_MS = 500;
const MAX_ATTEMPTS = 30; // 15 seconds total at 500 ms per attempt

/**
 * Resolves the Tauri session config and then polls the backend health endpoint
 * until the API reports healthy or the maximum number of attempts is exceeded.
 *
 * Returns `loading` while the backend is starting, `ready` when it is healthy,
 * or `error` if it did not respond within the timeout window.
 */
export function useBackendReady(): ReadyState {
  const [state, setState] = useState<ReadyState>('loading');

  useEffect(() => {
    let cancelled = false;
    let attempts = 0;

    const poll = async () => {
      // Resolve session config first so the health URL uses the correct port.
      let healthUrl: string;
      try {
        const session = await resolveSessionConfig();
        const baseUrl = session.apiBaseUrl.replace(/\/api\/v1\/?$/, '');
        healthUrl = `${baseUrl}/health`;
      } catch {
        if (!cancelled) setState('error');
        return;
      }

      const attempt = async () => {
        if (cancelled) return;

        if (attempts >= MAX_ATTEMPTS) {
          if (!cancelled) setState('error');
          return;
        }

        attempts++;

        try {
          const response = await fetch(healthUrl, {
            // No launch token header — the health endpoint is exempt from auth.
            method: 'GET',
            cache: 'no-store',
          });

          if (response.ok) {
            if (!cancelled) setState('ready');
            return;
          }
        } catch {
          // Network error — backend not ready yet.
        }

        setTimeout(attempt, POLL_INTERVAL_MS);
      };

      attempt();
    };

    poll();

    return () => {
      cancelled = true;
    };
  }, []);

  return state;
}
