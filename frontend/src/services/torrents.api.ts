import type { TorrentDto, ApiResponse, PagedResultDto, DashboardMetricsDto, TorrentFileDto, FilePriorityUpdateDto, PeerDto, TrackerDto } from '@/app/types';
import { httpClient } from '@/services/http';
import { unwrapApiResponse } from '@/services/apiResponse';

export type CreateTorrentInput = {
  name?: string;
  savePath: string;
  magnetLink?: string;
  torrentFileName?: string;
  torrentFileContentBase64?: string;
};

export type FetchTorrentsParams = {
  page?: number;
  pageSize?: number;
  status?: string;
  q?: string;
};

export async function fetchTorrents(params?: FetchTorrentsParams): Promise<PagedResultDto<TorrentDto>> {
  const query = new URLSearchParams();
  if (params?.page) query.append('page', params.page.toString());
  if (params?.pageSize) query.append('pageSize', params.pageSize.toString());
  if (params?.status && params.status !== 'all') query.append('status', params.status);
  if (params?.q) query.append('q', params.q);

  const url = query.toString() ? `/torrents?${query.toString()}` : '/torrents';
  const response = await httpClient.get<ApiResponse<PagedResultDto<TorrentDto>>>(url);
  return unwrapApiResponse(response.data);
}

export async function fetchDashboardMetrics(): Promise<DashboardMetricsDto> {
  const response = await httpClient.get<ApiResponse<DashboardMetricsDto>>('/dashboard/metrics');
  return unwrapApiResponse(response.data);
}

export async function fetchTorrent(torrentId: string): Promise<TorrentDto> {
  const response = await httpClient.get<ApiResponse<TorrentDto>>(`/torrents/${torrentId}`);
  return unwrapApiResponse(response.data);
}

export async function fetchTorrentFiles(torrentId: string): Promise<TorrentFileDto[]> {
  const response = await httpClient.get<ApiResponse<TorrentFileDto[]>>(`/torrents/${torrentId}/files`);
  return unwrapApiResponse(response.data);
}

export async function updateFilePriority(torrentId: string, updates: FilePriorityUpdateDto[]): Promise<void> {
  const response = await httpClient.put<ApiResponse<void>>(`/torrents/${torrentId}/files/priority`, updates);
  return unwrapApiResponse(response.data);
}

export async function addTorrent(input: CreateTorrentInput): Promise<TorrentDto> {
  const response = await httpClient.post<ApiResponse<TorrentDto>>('/torrents', input);
  return unwrapApiResponse(response.data);
}

export async function startTorrent(torrentId: string): Promise<TorrentDto> {
  const response = await httpClient.post<ApiResponse<TorrentDto>>(`/torrents/${torrentId}/start`);
  return unwrapApiResponse(response.data);
}

export async function pauseTorrent(torrentId: string): Promise<TorrentDto> {
  const response = await httpClient.post<ApiResponse<TorrentDto>>(`/torrents/${torrentId}/pause`);
  return unwrapApiResponse(response.data);
}

export async function stopTorrent(torrentId: string): Promise<TorrentDto> {
  const response = await httpClient.post<ApiResponse<TorrentDto>>(`/torrents/${torrentId}/stop`);
  return unwrapApiResponse(response.data);
}

export async function removeTorrent(torrentId: string): Promise<string> {
  const response = await httpClient.delete<ApiResponse<{ TorrentId: string }>>(`/torrents/${torrentId}`);
  const payload = unwrapApiResponse(response.data);
  return payload.TorrentId;
}

export async function fetchTorrentPeers(torrentId: string): Promise<PeerDto[]> {
  const response = await httpClient.get<ApiResponse<PeerDto[]>>(`/torrents/${torrentId}/peers`);
  return unwrapApiResponse(response.data);
}

export async function fetchTorrentTrackers(torrentId: string): Promise<TrackerDto[]> {
  const response = await httpClient.get<ApiResponse<TrackerDto[]>>(`/torrents/${torrentId}/trackers`);
  return unwrapApiResponse(response.data);
}

export async function addTracker(torrentId: string, url: string): Promise<void> {
  const response = await httpClient.post<ApiResponse<void>>(`/torrents/${torrentId}/trackers`, { url });
  return unwrapApiResponse(response.data);
}