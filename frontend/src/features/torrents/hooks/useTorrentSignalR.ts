import { useEffect } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { createTorrentHubConnection } from '@/services/signalr';
import { useSystemStore } from '@/store/systemStore';
import type { TorrentDto } from '@/app/types';

export function useTorrentSignalR() {
  const queryClient = useQueryClient();
  const setSignalrStatus = useSystemStore((state) => state.setSignalrStatus);

  useEffect(() => {
    let active = true;
    const connection = createTorrentHubConnection();
    setSignalrStatus('connecting');

    const updateTorrentInCache = (updatedTorrent: TorrentDto) => {
      // Update all list queries that match the 'torrents' key
      queryClient.setQueriesData({ queryKey: ['torrents'] }, (oldData: any) => {
        if (!oldData || !oldData.items) return oldData;
        return {
          ...oldData,
          items: oldData.items.map((t: TorrentDto) =>
            t.id === updatedTorrent.id ? { ...t, ...updatedTorrent } : t
          ),
        };
      });

      // Keep single-torrent details query synchronized in real time
      queryClient.setQueryData<TorrentDto>(['torrent', updatedTorrent.id], (old) =>
        old ? { ...old, ...updatedTorrent } : updatedTorrent
      );
    };

    const invalidateAll = () => {
      queryClient.invalidateQueries({ queryKey: ['torrents'] });
      queryClient.invalidateQueries({ queryKey: ['torrent'] });
      queryClient.invalidateQueries({ queryKey: ['dashboardMetrics'] });
    };

    connection.on('TorrentAdded', invalidateAll);
    connection.on('TorrentUpdated', updateTorrentInCache);
    connection.on('TorrentProgressUpdated', updateTorrentInCache);

    connection.on('TorrentStarted', (t) => {
      updateTorrentInCache(t);
      queryClient.invalidateQueries({ queryKey: ['dashboardMetrics'] });
    });
    connection.on('TorrentPaused', (t) => {
      updateTorrentInCache(t);
      queryClient.invalidateQueries({ queryKey: ['dashboardMetrics'] });
    });
    connection.on('TorrentStopped', (t) => {
      updateTorrentInCache(t);
      queryClient.invalidateQueries({ queryKey: ['dashboardMetrics'] });
    });
    connection.on('TorrentCompleted', (t) => {
      updateTorrentInCache(t);
      queryClient.invalidateQueries({ queryKey: ['dashboardMetrics'] });
    });

    connection.on('TorrentRemoved', invalidateAll);

    connection.onreconnecting(() => {
      if (active) setSignalrStatus('reconnecting');
    });

    connection.onreconnected(() => {
      if (active) setSignalrStatus('connected');
      invalidateAll();
    });

    connection.onclose(() => {
      if (active) setSignalrStatus('disconnected');
    });

    connection
      .start()
      .then(() => {
        if (active) setSignalrStatus('connected');
      })
      .catch((err: Error) => {
        if (err.name === 'AbortError' || err.message.includes('stopped during negotiation')) {
          // Ignore intentional aborts caused by React StrictMode remounting
          return;
        }
        if (active) setSignalrStatus('disconnected');
        console.error('SignalR connection failed:', err.message);
      });

    return () => {
      active = false;
      connection.stop();
    };
  }, [queryClient, setSignalrStatus]);
}
