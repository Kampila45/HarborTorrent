import { useState, type FormEvent } from 'react';
import { X } from 'lucide-react';
import { useRssActions } from '../hooks/useRssActions';
import { toast } from 'sonner';

interface RssFeedModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export function RssFeedModal({ isOpen, onClose }: RssFeedModalProps) {
  const { addFeedMutation } = useRssActions();
  const [name, setName] = useState('');
  const [url, setUrl] = useState('');

  if (!isOpen) return null;

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    if (!name.trim() || !url.trim()) return;

    try {
      await addFeedMutation.mutateAsync({ name: name.trim(), url: url.trim() });
      toast.success('RSS feed added successfully');
      setName('');
      setUrl('');
      onClose();
    } catch (error) {
      toast.error('Failed to add RSS feed');
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm">
      <div className="w-full max-w-md rounded-xl bg-white dark:bg-[#111111] shadow-2xl ring-1 ring-black/5">
        <div className="flex items-center justify-between border-b border-[#E9E9E7] dark:border-[#333333] px-6 py-4">
          <h2 className="text-[16px] font-semibold text-[#37352F] dark:text-[#E9E9E7]">Add RSS Feed</h2>
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
              <label htmlFor="feed-name" className="text-[13px] font-medium text-[#37352F] dark:text-[#E9E9E7]">Feed Name</label>
              <input
                id="feed-name"
                type="text"
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder="e.g. ShowRSS"
                required
                className="w-full rounded-md border border-[#E9E9E7] dark:border-[#333333] bg-transparent px-3 py-2 text-[14px] text-[#37352F] dark:text-[#E9E9E7] placeholder:text-[rgba(55,53,47,0.4)] dark:placeholder:text-[rgba(255,255,255,0.2)] focus:border-[#2383E2] focus:outline-none focus:ring-1 focus:ring-[#2383E2]"
              />
            </div>
            <div className="grid gap-2">
              <label htmlFor="feed-url" className="text-[13px] font-medium text-[#37352F] dark:text-[#E9E9E7]">RSS URL</label>
              <input
                id="feed-url"
                type="url"
                value={url}
                onChange={(e) => setUrl(e.target.value)}
                placeholder="https://showrss.info/user/..."
                required
                className="w-full rounded-md border border-[#E9E9E7] dark:border-[#333333] bg-transparent px-3 py-2 text-[14px] text-[#37352F] dark:text-[#E9E9E7] placeholder:text-[rgba(55,53,47,0.4)] dark:placeholder:text-[rgba(255,255,255,0.2)] focus:border-[#2383E2] focus:outline-none focus:ring-1 focus:ring-[#2383E2]"
              />
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
              disabled={addFeedMutation.isPending || !name.trim() || !url.trim()}
              className="rounded-md bg-[#2383E2] px-4 py-2 text-[14px] font-medium text-white transition-colors hover:bg-[#1C68B3] disabled:opacity-50"
            >
              {addFeedMutation.isPending ? 'Adding...' : 'Add Feed'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
