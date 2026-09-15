import { useQuery } from '@tanstack/react-query';
import { fetchTorrentFiles } from '@/services/torrents.api';

export function useTorrentFilesQuery(torrentId: string | undefined) {
  return useQuery({
    queryKey: ['torrent-files', torrentId],
    queryFn: () => {
      if (!torrentId) throw new Error('Torrent ID is required');
      return fetchTorrentFiles(torrentId);
    },
    enabled: !!torrentId,
    refetchInterval: 5000,
  });
}
