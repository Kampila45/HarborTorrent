import { httpClient } from '@/services/http';

export interface SearchResultDto {
  title: string;
  infoHash: string;
  sizeBytes: number;
  seeders: number;
  leechers: number;
  providerName: string;
  magnetUri: string;
}

export const searchApi = {
  searchTorrents: async (query: string): Promise<SearchResultDto[]> => {
    const response = await httpClient.get<SearchResultDto[]>('/search', { params: { q: query } });
    return response.data;
  },
};
