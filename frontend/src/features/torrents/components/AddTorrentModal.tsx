import { useEffect, useState, type ChangeEvent } from 'react';
import { UploadCloud, X, AlertCircle } from 'lucide-react';
import { useTorrentActions } from '@/features/torrents/hooks/useTorrentActions';
import { useUiStore } from '@/store/uiStore';
import { FolderBrowserModal } from '@/features/directories/components/FolderBrowserModal';
import { httpClient } from '@/services/http';
import { toast } from 'sonner';
import { useSound } from '@/hooks/useSound';

type TorrentFormState = {
  fileName: string;
  magnetLink: string;
  location: string;
  startImmediately: boolean;
};

const initialState: TorrentFormState = {
  fileName: '',
  magnetLink: '',
  location: '/home/Kampila45/Downloads',
  startImmediately: true,
};

async function fileToBase64(file: File): Promise<string> {
  const arrayBuffer = await file.arrayBuffer();
  const bytes = new Uint8Array(arrayBuffer);
  let binary = '';

  bytes.forEach((byte) => {
    binary += String.fromCharCode(byte);
  });

  return window.btoa(binary);
}

export function AddTorrentModal() {
  const { addTorrentOpen, closeAddTorrent, addTorrentInitialValue, addTorrentInitialFile } = useUiStore();
  const { addMutation, startMutation } = useTorrentActions();
  const [form, setForm] = useState<TorrentFormState>(initialState);
  const [file, setFile] = useState<File | null>(null);
  const [isBrowserOpen, setIsBrowserOpen] = useState(false);
  const { playPop, playErrorTone } = useSound();

  useEffect(() => {
    if (addTorrentOpen) {
      httpClient.get<{defaultDownloadPath: string}>('/settings').then(response => {
        setForm(current => ({
          ...current,
          magnetLink: addTorrentInitialValue || current.magnetLink,
          fileName: addTorrentInitialFile?.name || current.fileName,
          location: response.data.defaultDownloadPath || current.location
        }));
        
        // Cannot easily create a File object from a base64 string without overhead.
        // Therefore, track the pre-filled base64 in the component state if it exists.
      }).catch(console.error);
    } else {
      setForm(initialState);
      setFile(null);
    }
  }, [addTorrentOpen]);

  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape' && addTorrentOpen) {
        closeAddTorrent();
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [addTorrentOpen, closeAddTorrent]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!form.location) return;

    let torrentFileContentBase64 = '';
    if (file) {
      torrentFileContentBase64 = await fileToBase64(file);
    } else if (addTorrentInitialFile) {
      torrentFileContentBase64 = addTorrentInitialFile.base64;
    }

    const input = {
      savePath: form.location,
      ...(form.magnetLink.trim() ? { magnetLink: form.magnetLink.trim() } : {}),
      ...(torrentFileContentBase64
        ? {
            torrentFileName: file?.name || addTorrentInitialFile?.name || 'unknown.torrent',
            torrentFileContentBase64,
          }
        : {}),
    };

    try {
      const response = await addMutation.mutateAsync(input);

      if (form.startImmediately) {
        await startMutation.mutateAsync(response.id);
      }

      playPop();
      toast.success('Torrent added successfully');
      closeAddTorrent();
    } catch (error) {
      playErrorTone();
      console.error('Failed to add torrent:', error);
    }
  };

  const handleFileChange = (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    setFile(file ?? null);
    setForm((current) => ({
      ...current,
      fileName: file?.name ?? current.fileName,
    }));
  };

  if (!addTorrentOpen) {
    return null;
  }

  return (
    <div className="fixed inset-0 z-20 grid place-items-center bg-[rgba(55,53,47,0.22)] p-6 backdrop-blur-md" role="presentation" onClick={closeAddTorrent}>
      <div
        className="w-full max-w-[680px] rounded-[8px] border border-[var(--border-strong)] bg-white dark:bg-[#111111] p-5 shadow-[var(--shadow)]"
        role="dialog"
        aria-modal="true"
        aria-labelledby="add-torrent-title"
        onClick={(event) => event.stopPropagation()}
      >
        <div className="mb-4 flex items-center justify-between gap-4">
          <div>
            <p className="m-0 text-[12px] font-semibold uppercase tracking-[0.08em] text-[var(--muted)]">Add torrent</p>
            <h2 id="add-torrent-title" className="m-0 text-[18px] font-semibold leading-[26px] text-[var(--text)]">Create a new transfer</h2>
          </div>
          <button type="button" className="inline-grid h-9 w-9 place-items-center rounded-[4px] border border-[var(--border)] bg-white dark:bg-[#111111] text-[var(--muted)] transition-colors hover:bg-[var(--surface-3)]" onClick={closeAddTorrent} aria-label="Close add torrent dialog">
            <X size={18} />
          </button>
        </div>

        {addMutation.isError && (
          <div className="mb-4 flex items-center gap-2 rounded-md border border-red-200 bg-red-50 p-3 text-[13px] text-red-600 dark:border-red-900/30 dark:bg-red-900/10">
            <AlertCircle size={16} className="shrink-0" />
            <span>
              {(addMutation.error as any)?.response?.data?.title || 
               (addMutation.error as Error)?.message || 
               'Failed to add torrent. It may already be in your library.'}
            </span>
          </div>
        )}

        <form className="grid gap-4" onSubmit={(event) => void handleSubmit(event)}>
          <div className="grid gap-2 text-[var(--text)]">
            <span className="text-sm text-[var(--muted)] dark:text-[#E9E9E7]">Upload torrent file</span>
            <label className="group relative flex cursor-pointer flex-col items-center justify-center rounded-[4px] border-2 border-dashed border-[var(--border-strong)] bg-[rgba(255,255,255,0.03)] py-8 transition-colors hover:border-[var(--accent)] hover:bg-[rgba(55,53,47,0.02)] dark:hover:bg-[rgba(255,255,255,0.02)]">
              <input type="file" accept=".torrent" onChange={handleFileChange} className="absolute inset-0 h-full w-full cursor-pointer opacity-0" />
              <div className="flex flex-col items-center justify-center text-center">
                <UploadCloud size={28} className="mb-2 text-[var(--muted)] transition-colors group-hover:text-[var(--accent)]" />
                <p className="text-[14px] font-medium text-[var(--text)]">Click to browse or drag and drop</p>
                <p className="mt-1 text-[12px] text-[var(--muted)] dark:text-[#E9E9E7]">.torrent files only</p>
              </div>
            </label>
            {form.fileName && (
              <p className="text-sm font-medium text-[var(--success)]">Selected: {form.fileName}</p>
            )}
          </div>

          <label className="grid gap-2 text-[var(--text)]">
            <span className="text-sm text-[var(--muted)] dark:text-[#E9E9E7]">Magnet link</span>
            <textarea
              value={form.magnetLink}
              onChange={(event) => setForm((current) => ({ ...current, magnetLink: event.target.value }))}
              placeholder="magnet:?xt=urn:btih:..."
              rows={3}
              className="w-full rounded-[4px] border border-[var(--border)] bg-[rgba(255,255,255,0.03)] px-3 py-3 outline-none focus:border-[var(--accent-strong)] focus:ring-2 focus:ring-[rgba(173,198,255,0.16)]"
            />
          </label>

          <div className="grid gap-2 text-[var(--text)]">
            <span className="text-sm text-[var(--muted)] dark:text-[#E9E9E7]">Download location</span>
            <div className="flex gap-2">
              <input
                type="text"
                value={form.location}
                onChange={(event) => setForm((current) => ({ ...current, location: event.target.value }))}
                className="w-full rounded-[4px] border border-[var(--border)] bg-[rgba(255,255,255,0.03)] px-3 py-3 outline-none focus:border-[var(--accent-strong)] focus:ring-2 focus:ring-[rgba(173,198,255,0.16)]"
              />
              <button
                type="button"
                onClick={() => setIsBrowserOpen(true)}
                className="shrink-0 rounded-[4px] border border-[var(--border-strong)] bg-[#F7F7F5] dark:bg-[#2A2A2A] px-4 py-2 text-sm font-medium text-[#37352F] dark:text-[#E9E9E7] shadow-sm transition-colors hover:bg-[#EDEDEB] dark:hover:bg-[#333333] active:scale-[0.98]"
              >
                Browse
              </button>
            </div>
          </div>

          <label className="grid grid-flow-col justify-start gap-2 text-[var(--text)]">
            <input
              type="checkbox"
              checked={form.startImmediately}
              onChange={(event) => setForm((current) => ({ ...current, startImmediately: event.target.checked }))}
              className="h-4 w-4"
            />
            <span className="text-sm text-[var(--muted)] dark:text-[#E9E9E7]">Start immediately</span>
          </label>

          <div className="mt-2 flex justify-end gap-2">
            <button type="button" className="rounded-[4px] border border-[var(--border-strong)] bg-[#F7F7F5] dark:bg-[#2A2A2A] px-4 py-2 text-sm font-medium text-[#37352F] dark:text-[#E9E9E7] shadow-sm transition-colors hover:bg-[#EDEDEB] dark:hover:bg-[#333333] active:scale-[0.98]" onClick={closeAddTorrent}>
              Cancel
            </button>
            <button type="submit" className="inline-flex items-center justify-center gap-2 rounded-[6px] border border-transparent bg-[#37352F] dark:bg-[#E9E9E7] px-4 py-2 text-sm font-semibold text-white dark:text-[#111111] shadow-[var(--shadow)] transition-colors hover:bg-[#2f2d28] dark:hover:bg-white disabled:cursor-not-allowed disabled:opacity-60" disabled={addMutation.isPending || startMutation.isPending}>
              {addMutation.isPending || startMutation.isPending ? 'Adding...' : 'Add Torrent'}
            </button>
          </div>
        </form>
      </div>

      <FolderBrowserModal
        isOpen={isBrowserOpen}
        onClose={() => setIsBrowserOpen(false)}
        onSelect={(path) => setForm((current) => ({ ...current, location: path }))}
        initialPath={form.location}
      />
    </div>
  );
}