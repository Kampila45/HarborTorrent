import { useState, useEffect, useCallback } from 'react';
import { resolveSessionConfig } from '@/services/session';

type ReadyState = 'loading' | 'ready' | 'error';

const POLL_INTERVAL_MS = 500;
const REQUEST_TIMEOUT_MS = 2000;
const MAX_ATTEMPTS = 30; // ~15 seconds across polling intervals

export type BackendReadyResult = {
  state: ReadyState;
  errorReason: string | null;
  retry: () => void;
};

/**
 * Resolves the Tauri session config and polls the backend health endpoint
 * with bounded per-request timeouts until the API reports healthy or the
 * maximum number of attempts is exceeded.
 */
export function useBackendReady(): BackendReadyResult {
  const [state, setState] = useState<ReadyState>('loading');
  const [errorReason, setErrorReason] = useState<string | null>(null);
  const [attemptEpoch, setAttemptEpoch] = useState(0);

  const retry = useCallback(() => {
    setState('loading');
    setErrorReason(null);
    setAttemptEpoch((n) => n + 1);
  }, []);

  useEffect(() => {
    let cancelled = false;
    let attempts = 0;
    let timerId: number | undefined;

    const poll = async () => {
      let healthUrl: string;
      try {
        const session = await resolveSessionConfig();
        const baseUrl = session.apiBaseUrl.replace(/\/api\/v1\/?$/, '');
        healthUrl = `${baseUrl}/health`;
      } catch {
        if (!cancelled) {
          setErrorReason('Could not resolve desktop session configuration.');
          setState('error');
        }
        return;
      }

      const attempt = async () => {
        if (cancelled) return;

        if (attempts >= MAX_ATTEMPTS) {
          if (!cancelled) {
            setErrorReason('The background download engine did not respond within the startup timeout window.');
            setState('error');
          }
          return;
        }

        attempts++;
        const controller = new AbortController();
        const timeoutId = window.setTimeout(() => controller.abort(), REQUEST_TIMEOUT_MS);

        try {
          const response = await fetch(healthUrl, {
            method: 'GET',
            cache: 'no-store',
            signal: controller.signal,
          });

          if (response.ok) {
            if (!cancelled) {
              setErrorReason(null);
              setState('ready');
            }
            return;
          }
        } catch {
          // Network error or timeout — backend not ready yet.
        } finally {
          window.clearTimeout(timeoutId);
        }

        if (!cancelled) {
          timerId = window.setTimeout(attempt, POLL_INTERVAL_MS);
        }
      };

      attempt();
    };

    poll();

    return () => {
      cancelled = true;
      if (timerId !== undefined) {
        window.clearTimeout(timerId);
      }
    };
  }, [attemptEpoch]);

  return { state, errorReason, retry };
}
