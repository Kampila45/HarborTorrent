import { useEffect } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { createTorrentHubConnection } from '@/services/signalr';
import type { TorrentDto } from '@/app/types';

export function useTorrentSignalR() {
  const queryClient = useQueryClient();

  useEffect(() => {
    const connection = createTorrentHubConnection();

    const updateTorrentInCache = (updatedTorrent: TorrentDto) => {
      // Update all queries that match the 'torrents' key
      queryClient.setQueriesData({ queryKey: ['torrents'] }, (oldData: any) => {
        if (!oldData || !oldData.items) return oldData;
        return {
          ...oldData,
          items: oldData.items.map((t: TorrentDto) => t.id === updatedTorrent.id ? { ...t, ...updatedTorrent } : t)
        };
      });
    };

    connection.on('TorrentAdded', () => {
      queryClient.invalidateQueries({ queryKey: ['torrents'] });
      queryClient.invalidateQueries({ queryKey: ['dashboardMetrics'] });
    });

    connection.on('TorrentUpdated', updateTorrentInCache);
    connection.on('TorrentProgressUpdated', updateTorrentInCache);
    
    connection.on('TorrentStarted', (t) => { updateTorrentInCache(t); queryClient.invalidateQueries({ queryKey: ['dashboardMetrics'] }); });
    connection.on('TorrentPaused', (t) => { updateTorrentInCache(t); queryClient.invalidateQueries({ queryKey: ['dashboardMetrics'] }); });
    connection.on('TorrentStopped', (t) => { updateTorrentInCache(t); queryClient.invalidateQueries({ queryKey: ['dashboardMetrics'] }); });
    connection.on('TorrentCompleted', (t) => { updateTorrentInCache(t); queryClient.invalidateQueries({ queryKey: ['dashboardMetrics'] }); });

    connection.on('TorrentRemoved', () => {
      queryClient.invalidateQueries({ queryKey: ['torrents'] });
      queryClient.invalidateQueries({ queryKey: ['dashboardMetrics'] });
    });

    connection.start().catch((err: Error) => {
      if (err.name === 'AbortError' || err.message.includes('stopped during negotiation')) {
        // Ignore intentional aborts caused by React StrictMode remounting
        return;
      }
      console.error('SignalR connection failed:', err);
    });

    return () => {
      connection.stop();
    };
  }, [queryClient]);
}
