import type { ApiResponse } from '@/app/types';
import { httpClient } from '@/services/http';
import { unwrapApiResponse } from '@/services/apiResponse';

export interface DirectoryItemDto {
  name: string;
  path: string;
}

export async function fetchDirectories(path?: string): Promise<DirectoryItemDto[]> {
  const query = new URLSearchParams();
  if (path) {
    query.append('path', path);
  }

  const url = query.toString() ? `/directories?${query.toString()}` : '/directories';
  const response = await httpClient.get<ApiResponse<DirectoryItemDto[]>>(url);
  return unwrapApiResponse(response.data);
}
