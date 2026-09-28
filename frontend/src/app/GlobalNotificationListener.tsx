import { useEffect, useRef } from 'react';
import { toast } from 'sonner';
import { useQuery } from '@tanstack/react-query';
import { fetchTorrents } from '@/services/torrents.api';
import { useSound } from '@/hooks/useSound';

import { sendNotification, isPermissionGranted, requestPermission } from '@tauri-apps/plugin-notification';

export function GlobalNotificationListener() {
  const { playChime } = useSound();
  const previousStatusRef = useRef<Record<string, string>>({});

  // Request notification permission on mount
  useEffect(() => {
    async function setupNotifications() {
      const granted = await isPermissionGranted();
      if (!granted) {
        await requestPermission();
      }
    }
    setupNotifications();
  }, []);

  // Poll all torrents every 2 seconds to detect status changes
  const { data } = useQuery({
    queryKey: ['globalTorrentsPolling'],
    queryFn: () => fetchTorrents({ pageSize: 100 }), // Fetch enough to cover active downloads
    refetchInterval: 2000,
  });

  useEffect(() => {
    if (!data?.items) return;

    const currentStatuses: Record<string, string> = {};

    data.items.forEach(async (torrent) => {
      currentStatuses[torrent.id] = torrent.status;

      const previousStatus = previousStatusRef.current[torrent.id];

      // Detect transition from Downloading to Seeding (or Completed)
      if (previousStatus === 'Downloading' && (torrent.status === 'Seeding' || torrent.status === 'Completed')) {
        playChime();
        toast.success(`Download Complete`, {
          description: torrent.name,
          duration: 5000,
        });

        const title = 'Download Complete';
        const body = torrent.name;

        // Try to trigger a native desktop notification or browser fallback
        try {
          if ('__TAURI_INTERNALS__' in window) {
            // Tauri Native Notification
            const granted = await isPermissionGranted();
            if (granted) {
              sendNotification({
                title,
                body,
                icon: 'icons/32x32.png',
              });
            }
          } else if ('Notification' in window) {
            // HTML5 Browser Notification
            if (Notification.permission === 'granted') {
              new Notification(title, { body, icon: '/logo.png' });
            }
          }
        } catch (e) {
          console.error('Failed to send desktop notification', e);
        }
      }
    });

    previousStatusRef.current = currentStatuses;
  }, [data, playChime]);

  return null;
}
