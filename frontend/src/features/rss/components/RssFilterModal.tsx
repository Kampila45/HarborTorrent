import { useState, type FormEvent } from 'react';
import { X, Folder } from 'lucide-react';
import { useRssActions } from '../hooks/useRssActions';
import { toast } from 'sonner';
import { FolderBrowserModal } from '@/features/directories/components/FolderBrowserModal';

interface RssFilterModalProps {
  isOpen: boolean;
  feedId: string;
  onClose: () => void;
}

export function RssFilterModal({ isOpen, feedId, onClose }: RssFilterModalProps) {
  const { addFilterMutation } = useRssActions();
  const [regexPattern, setRegexPattern] = useState('');
  const [savePath, setSavePath] = useState('');
  const [isBrowserOpen, setIsBrowserOpen] = useState(false);

  if (!isOpen) return null;

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    if (!regexPattern.trim() || !savePath.trim()) return;

    try {
      await addFilterMutation.mutateAsync({ feedId, input: { regexPattern: regexPattern.trim(), savePath: savePath.trim() } });
      toast.success('Filter added successfully');
      setRegexPattern('');
      setSavePath('');
      onClose();
    } catch (error) {
      toast.error('Failed to add filter');
    }
  };

  return (
    <>
      <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm">
        <div className="w-full max-w-md rounded-xl bg-white dark:bg-[#111111] shadow-2xl ring-1 ring-black/5">
          <div className="flex items-center justify-between border-b border-[#E9E9E7] dark:border-[#333333] px-6 py-4">
            <h2 className="text-[16px] font-semibold text-[#37352F] dark:text-[#E9E9E7]">Add Regex Filter</h2>
            <button
              type="button"
              onClick={onClose}
              className="rounded p-1 text-[#5F5E5B] dark:text-[#A0A0A0] transition-colors hover:bg-[#F7F7F5] dark:hover:bg-[#222222]"
            >
              <X size={18} />
            </button>
          </div>

          <form onSubmit={(e) => void handleSubmit(e)} className="p-6">
            <div className="grid gap-4 mb-6">
              <div className="grid gap-2">
                <label htmlFor="regex-pattern" className="text-[13px] font-medium text-[#37352F] dark:text-[#E9E9E7]">Regex Pattern</label>
                <input
                  id="regex-pattern"
                  type="text"
                  value={regexPattern}
                  onChange={(e) => setRegexPattern(e.target.value)}
                  placeholder="e.g. (?i)Abbott.*Elementary.*1080p"
                  required
                  className="w-full rounded-md border border-[#E9E9E7] dark:border-[#333333] font-mono bg-transparent px-3 py-2 text-[13px] text-[#37352F] dark:text-[#E9E9E7] placeholder:text-[rgba(55,53,47,0.4)] dark:placeholder:text-[rgba(255,255,255,0.2)] focus:border-[#2383E2] focus:outline-none focus:ring-1 focus:ring-[#2383E2]"
                />
                <p className="text-[11px] text-[#5F5E5B] dark:text-[#A0A0A0]">Matches against the title of the RSS item.</p>
              </div>
              <div className="grid gap-2">
                <label htmlFor="save-path" className="text-[13px] font-medium text-[#37352F] dark:text-[#E9E9E7]">Save Location</label>
                <div className="flex gap-2">
                  <input
                    id="save-path"
                    type="text"
                    value={savePath}
                    onChange={(e) => setSavePath(e.target.value)}
                    placeholder="/downloads/tv"
                    required
                    className="flex-1 rounded-md border border-[#E9E9E7] dark:border-[#333333] bg-transparent px-3 py-2 text-[14px] text-[#37352F] dark:text-[#E9E9E7] placeholder:text-[rgba(55,53,47,0.4)] dark:placeholder:text-[rgba(255,255,255,0.2)] focus:border-[#2383E2] focus:outline-none focus:ring-1 focus:ring-[#2383E2]"
                  />
                  <button
                    type="button"
                    onClick={() => setIsBrowserOpen(true)}
                    className="flex shrink-0 items-center justify-center rounded-md border border-[#E9E9E7] dark:border-[#333333] bg-[#F7F7F5] dark:bg-[#1A1A1A] px-3 transition-colors hover:bg-[#E9E9E7] dark:hover:bg-[#222222]"
                  >
                    <Folder size={18} className="text-[#5F5E5B] dark:text-[#E9E9E7]" />
                  </button>
                </div>
              </div>
            </div>

            <div className="flex justify-end gap-3">
              <button
                type="button"
                onClick={onClose}
                className="rounded-md px-4 py-2 text-[14px] font-medium text-[#5F5E5B] dark:text-[#A0A0A0] transition-colors hover:bg-[#F7F7F5] dark:hover:bg-[#222222]"
              >
                Cancel
              </button>
              <button
                type="submit"
                disabled={addFilterMutation.isPending || !regexPattern.trim() || !savePath.trim()}
                className="rounded-md bg-[#2383E2] px-4 py-2 text-[14px] font-medium text-white transition-colors hover:bg-[#1C68B3] disabled:opacity-50"
              >
                {addFilterMutation.isPending ? 'Adding...' : 'Add Filter'}
              </button>
            </div>
          </form>
        </div>
      </div>

      <FolderBrowserModal
        isOpen={isBrowserOpen}
        onClose={() => setIsBrowserOpen(false)}
        onSelect={(path) => setSavePath(path)}
      />
    </>
  );
}
