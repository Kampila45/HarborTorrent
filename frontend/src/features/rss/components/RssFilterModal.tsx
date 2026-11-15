import { useEffect, useState, type FormEvent } from 'react';
import { X, Folder } from 'lucide-react';
import { useQuery } from '@tanstack/react-query';
import { useRssActions } from '../hooks/useRssActions';
import { Button } from '@/components/ui/Button';
import { toast } from 'sonner';
import { FolderBrowserModal } from '@/features/directories/components/FolderBrowserModal';
import { settingsApi } from '@/features/settings/api';

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

  const { data: settings } = useQuery({
    queryKey: ['settings'],
    queryFn: settingsApi.getSettings,
    enabled: isOpen,
  });

  useEffect(() => {
    if (!isOpen) {
      setRegexPattern('');
      setSavePath('');
      return;
    }

    if (settings?.defaultDownloadPath) {
      setSavePath((current) => current || settings.defaultDownloadPath);
    }
  }, [isOpen, settings?.defaultDownloadPath]);

  if (!isOpen) return null;

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    const resolvedSavePath = savePath.trim() || settings?.defaultDownloadPath || '';
    if (!regexPattern.trim() || !resolvedSavePath) return;

    try {
      await addFilterMutation.mutateAsync({ feedId, input: { regexPattern: regexPattern.trim(), savePath: resolvedSavePath } });
      toast.success('Filter added — polling feed now');
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
                    placeholder={settings?.defaultDownloadPath || 'Default HarborTorrent folder'}
                    className="flex-1 rounded-md border border-[#E9E9E7] dark:border-[#333333] bg-transparent px-3 py-2 text-[14px] text-[#37352F] dark:text-[#E9E9E7] placeholder:text-[rgba(55,53,47,0.4)] dark:placeholder:text-[rgba(255,255,255,0.2)] focus:border-[#2383E2] focus:outline-none focus:ring-1 focus:ring-[#2383E2]"
                  />
                  <button
                    type="button"
                    onClick={() => setIsBrowserOpen(true)}
                    className="flex shrink-0 items-center justify-center rounded-md border border-[#E9E9E7] dark:border-[#333333] bg-[#F7F7F5] dark:bg-[#1A1A1A] px-3 transition-colors hover:bg-[#E9E9E7] dark:hover:bg-[#222222]"
                    title="Browse folder"
                  >
                    <Folder size={18} className="text-[#5F5E5B] dark:text-[#E9E9E7]" />
                  </button>
                </div>
                <p className="text-[11px] text-[#5F5E5B] dark:text-[#A0A0A0]">Defaults to your HarborTorrent downloads folder. Edit or browse to choose another folder.</p>
              </div>
            </div>

            <div className="flex justify-end gap-2">
              <Button variant="ghost" onClick={onClose}>
                Cancel
              </Button>
              <Button type="submit" disabled={addFilterMutation.isPending || !regexPattern.trim() || (!savePath.trim() && !settings?.defaultDownloadPath)}>
                {addFilterMutation.isPending ? 'Adding...' : 'Add Filter'}
              </Button>
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
