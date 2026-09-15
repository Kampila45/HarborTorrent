import type { ApiResponse } from '@/app/types';

export function unwrapApiResponse<T>(response: ApiResponse<T>): T {
  if (!response.success) {
    const message = response.errors[0]?.message ?? 'Request failed';
    throw new Error(message);
  }

  if (response.data === null) {
    throw new Error('Response payload was empty');
  }

  return response.data;
}