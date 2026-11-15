import { useEffect } from 'react';
import { check } from '@tauri-apps/plugin-updater';
import { toast } from 'sonner';
import { useNavigate } from 'react-router-dom';

export interface UpdateCheckResult {
  available: boolean;
  version?: string;
  notes?: string | undefined;
}

export function useSystemUpdater() {
  const navigate = useNavigate();

  useEffect(() => {
    let mounted = true;

    const checkUpdates = async () => {
      if (!('__TAURI_INTERNALS__' in window)) return;
      try {
        const update = await check();
        if (update && mounted) {
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
  }, [navigate]);
}

export async function checkForUpdates(): Promise<UpdateCheckResult> {
  if (!('__TAURI_INTERNALS__' in window)) {
    throw new Error('Automatic updates are only supported in the native desktop app.');
  }

  const update = await check();
  if (!update) {
    return { available: false };
  }

  return {
    available: true,
    version: update.version,
    notes: update.body ?? undefined,
  };
}

export async function triggerManualUpdate(
  setIsUpdating: (val: boolean) => void,
  setUpdateProgress: (val: number) => void,
  setUpdateComplete: (val: boolean) => void,
  setUpdateError?: (val: string | null) => void
) {
  if (!('__TAURI_INTERNALS__' in window)) {
    toast.info('Browser Mode', {
      description: 'Automatic updates are only supported in the native desktop app.',
    });
    return;
  }

  setUpdateError?.(null);
  setUpdateProgress(0);
  setUpdateComplete(false);

  try {
    const update = await check();
    if (!update) {
      toast.info('You are up to date!', {
        description: 'No new updates are available at this time.',
      });
      return;
    }

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
            const pct = Math.min(99, Math.round((downloaded / contentLength) * 100));
            setUpdateProgress(pct);
          }
          break;
        case 'Finished':
          setUpdateProgress(100);
          break;
      }
    });

    // Only mark installation complete after downloadAndInstall resolves without throwing.
    setUpdateProgress(100);
    setUpdateComplete(true);
    setIsUpdating(false);
  } catch (err) {
    const message = err instanceof Error ? err.message : 'Update download or installation failed.';
    console.error('Update failed:', err);
    setIsUpdating(false);
    setUpdateProgress(0);
    setUpdateComplete(false);
    setUpdateError?.(message);
    toast.error('Update Failed', {
      description: message,
    });
  }
}
