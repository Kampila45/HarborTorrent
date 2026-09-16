import { useEffect } from 'react';
import { check } from '@tauri-apps/plugin-updater';
import { toast } from 'sonner';
import { useNavigate } from 'react-router-dom';

export function useSystemUpdater() {
  const navigate = useNavigate();

  useEffect(() => {
    let mounted = true;

    const checkUpdates = async () => {
      try {
        const update = await check();
        if (update && mounted) {
          // Notify the user an update is available on startup
          toast('Update Available', {
            description: `Version ${update.version} is ready to install.`,
            action: {
              label: 'View',
              onClick: () => navigate('/settings'),
            },
            duration: 10000,
          });
        }
      } catch (err) {
        console.error('Failed to check for updates:', err);
      }
    };

    checkUpdates();

    return () => {
      mounted = false;
    };
  }, []);
}

// Helper to manually trigger an update
export async function triggerManualUpdate(
  setIsUpdating: (val: boolean) => void,
  setUpdateProgress: (val: number) => void,
  setUpdateComplete: (val: boolean) => void
) {
  try {
    const update = await check();
    if (update) {
      setIsUpdating(true);
      let downloaded = 0;
      let contentLength = 0;

      await update.downloadAndInstall((event) => {
        switch (event.event) {
          case 'Started':
            contentLength = event.data.contentLength || 0;
            break;
          case 'Progress':
            downloaded += event.data.chunkLength;
            if (contentLength > 0) {
              const pct = Math.round((downloaded / contentLength) * 100);
              setUpdateProgress(pct);
            }
            break;
          case 'Finished':
            setUpdateProgress(100);
            setUpdateComplete(true);
            break;
        }
      });
    } else {
      toast.info('You are up to date!', {
        description: 'No new updates are available at this time.',
      });
    }
  } catch (err) {
    console.error('Update failed:', err);
    setIsUpdating(false);
  }
}
