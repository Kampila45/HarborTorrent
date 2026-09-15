import { useQuery } from '@tanstack/react-query';
import { fetchTorrents, type FetchTorrentsParams } from '@/services/torrents.api';

export function useTorrentsQuery(params?: FetchTorrentsParams) {
  return useQuery({
    queryKey: ['torrents', params],
    queryFn: () => fetchTorrents(params),
    refetchInterval: 1000,
  });
}