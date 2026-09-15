import axios, {
  AxiosHeaders,
  type AxiosError,
  type AxiosInstance,
} from 'axios';
import type { ApiResponse } from '@/app/types';

let requestCounter = 0;

export type ApiErrorShape = {
  message: string;
  status?: number;
  requestId?: string;
};

export function createHttpClient(): AxiosInstance {
  const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://127.0.0.1:5000/api/v1';

  const client = axios.create({
    baseURL: apiBaseUrl,
    timeout: 8000,
  });

  client.interceptors.request.use(async (config) => {
    const requestId = `req_${++requestCounter}`;
    const headers = config.headers ?? new AxiosHeaders();
    headers.set('X-Request-Id', requestId);
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