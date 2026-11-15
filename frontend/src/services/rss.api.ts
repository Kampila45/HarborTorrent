import { httpClient } from './http';

export interface RssFilter {
  id: string;
  regexPattern: string;
  savePath: string;
}

export interface RssFeed {
  id: string;
  name: string;
  url: string;
  lastPolledAt: string | null;
  lastError?: string | null;
  lastErrorAt?: string | null;
  isActive: boolean;
  filters: RssFilter[];
}

export async function fetchRssFeeds(): Promise<RssFeed[]> {
  const response = await httpClient.get<{ data: RssFeed[] }>('/rss');
  return response.data.data;
}

export async function addRssFeed(input: { name: string; url: string }): Promise<RssFeed> {
  const response = await httpClient.post<{ data: RssFeed }>('/rss', input);
  return response.data.data;
}

export async function deleteRssFeed(id: string): Promise<void> {
  await httpClient.delete(`/rss/${id}`);
}

export interface RssPollResult {
  feed: RssFeed;
  itemsScanned: number;
  matchedCount: number;
  addedCount: number;
  sampleTitles: string[];
  errorMessage?: string | null;
}

export async function pollRssFeed(id: string): Promise<RssPollResult> {
  const response = await httpClient.post<{ data: RssPollResult }>(`/rss/${id}/poll`);
  return response.data.data;
}

export async function addRssFilter(feedId: string, input: { regexPattern: string; savePath: string }): Promise<RssFilter> {
  const response = await httpClient.post<{ data: RssFilter }>(`/rss/${feedId}/filters`, input);
  return response.data.data;
}

export async function deleteRssFilter(filterId: string): Promise<void> {
  await httpClient.delete(`/rss/filters/${filterId}`);
}
