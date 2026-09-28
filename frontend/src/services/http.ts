import axios, {
  AxiosHeaders,
  type AxiosError,
  type AxiosInstance,
} from 'axios';
import type { ApiResponse } from '@/app/types';
import { getSessionConfig } from '@/services/session';

let requestCounter = 0;

export type ApiErrorShape = {
  message: string;
  status?: number;
  requestId?: string;
};

export function createHttpClient(): AxiosInstance {
  // Base URL and launch token are resolved from the Tauri session config after startup.
  // A placeholder client is created here; interceptors read the config at request time.
  const client = axios.create({
    timeout: 8000,
  });

  client.interceptors.request.use(async (config) => {
    const session = getSessionConfig();

    config.baseURL = session.apiBaseUrl;

    const requestId = `req_${++requestCounter}`;
    const headers = config.headers ?? new AxiosHeaders();
    headers.set('X-Request-Id', requestId);

    // Attach the per-session launch token so the backend can authenticate the request.
    if (session.launchToken) {
      headers.set('X-Launch-Token', session.launchToken);
    }

    config.headers = headers;
    return config;
  });

  client.interceptors.response.use(
    (response) => response,
    (error: AxiosError) => Promise.reject(parseApiError(error)),
  );

  return client;
}

export function parseApiError(error: AxiosError): ApiErrorShape {
  const apiError: ApiErrorShape = {
    message: error.message || 'Request failed',
  };

  const responseData = error.response?.data as ApiResponse<unknown> | undefined;
  const firstBackendError = responseData?.errors?.[0];

  if (firstBackendError?.message) {
    apiError.message = firstBackendError.message;
  }

  if (error.response?.status !== undefined) {
    apiError.status = error.response.status;
  }

  const requestId = error.config?.headers instanceof AxiosHeaders ? error.config.headers.get('X-Request-Id') : undefined;
  if (typeof requestId === 'string') {
    apiError.requestId = requestId;
  }

  return apiError;
}

export const httpClient = createHttpClient();