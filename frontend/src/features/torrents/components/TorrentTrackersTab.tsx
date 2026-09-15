import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { fetchTorrentTrackers, addTracker } from '@/services/torrents.api';
import { Plus } from 'lucide-react';
import { Pagination } from '@/components/Pagination';

type TorrentTrackersTabProps = {
  torrentId: string;
};

export function TorrentTrackersTab({ torrentId }: TorrentTrackersTabProps) {
  const queryClient = useQueryClient();
  const [newTrackerUrl, setNewTrackerUrl] = useState('');
  const [currentPage, setCurrentPage] = useState(1);
  const itemsPerPage = 15;
  
  const { data: trackers, isLoading } = useQuery({
    queryKey: ['torrentTrackers', torrentId],
    queryFn: () => fetchTorrentTrackers(torrentId),
    refetchInterval: 10000, // Poll every 10 seconds for trackers
  });

  const { mutate: addTrackerMutation, isPending } = useMutation({
    mutationFn: (url: string) => addTracker(torrentId, url),
    onSuccess: () => {
      setNewTrackerUrl('');
      queryClient.invalidateQueries({ queryKey: ['torrentTrackers', torrentId] });
    },
    onError: (error) => {
      alert(`Failed to add tracker: ${error.message}`);
    }
  });

  const handleAdd = (e: React.FormEvent) => {
    e.preventDefault();
    if (newTrackerUrl.trim()) {
      addTrackerMutation(newTrackerUrl.trim());
    }
  };

  const totalPages = trackers ? Math.ceil(trackers.length / itemsPerPage) : 0;
  const paginatedTrackers = trackers?.slice((currentPage - 1) * itemsPerPage, currentPage * itemsPerPage);

  return (
    <div className="flex flex-col">
      <div className="border-b border-[#e5e7eb] dark:border-[#333333] p-4 bg-[#f9fafb] dark:bg-[#1a1a1a]">
        <form onSubmit={handleAdd} className="flex items-center gap-2">
          <input
            type="url"
            placeholder="udp://tracker.opentrackr.org:1337/announce"
            className="flex-1 rounded border border-[#e5e7eb] dark:border-[#444444] bg-white dark:bg-[#2A2A2A] px-3 py-1.5 text-sm outline-none focus:border-[#2563eb]"
            value={newTrackerUrl}
            onChange={(e) => setNewTrackerUrl(e.target.value)}
            disabled={isPending}
            required
          />
          <button
            type="submit"
            disabled={isPending || !newTrackerUrl.trim()}
            className="flex items-center gap-1 rounded bg-[#2563eb] px-3 py-1.5 text-sm font-medium text-white transition-colors hover:bg-[#1d4ed8] disabled:opacity-50"
          >
            <Plus size={16} />
            Add Tracker
          </button>
        </form>
      </div>

      {isLoading ? (
        <div className="p-4 text-center text-sm text-[#5F5E5B] dark:text-[#E9E9E7]">Loading trackers...</div>
      ) : !trackers || trackers.length === 0 ? (
        <div className="p-4 text-center text-sm text-[#5F5E5B] dark:text-[#E9E9E7]">No trackers configured.</div>
      ) : (
        <div className="flex flex-col">
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm text-[#4B4A47] dark:text-[#E9E9E7]">
              <thead className="bg-[#f9fafb] text-[12px] uppercase text-[#5F5E5B] dark:bg-[#1a1a1a] dark:text-[#C4C4C4]">
                <tr>
                  <th className="px-4 py-3 font-medium">URL</th>
                  <th className="px-4 py-3 font-medium">Status</th>
                  <th className="px-4 py-3 font-medium">Last Announce</th>
                  <th className="px-4 py-3 font-medium">Message</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[#e5e7eb] dark:divide-[#333333]">
                {paginatedTrackers?.map((tracker, idx) => (
                  <tr key={idx} className="transition-colors hover:bg-[#f9fafb] dark:hover:bg-[#1a1a1a]">
                    <td className="whitespace-nowrap px-4 py-3 font-medium">{tracker.uri}</td>
                    <td className="whitespace-nowrap px-4 py-3">
                      <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-[11px] font-medium ${
                        tracker.status === 'Ok' ? 'bg-[#dcfce7] text-[#166534]' : 
                        tracker.status === 'Offline' ? 'bg-[#fee2e2] text-[#991b1b]' : 
                        'bg-[#f3f4f6] text-[#6b7280]'
                      }`}>
                        {tracker.status}
                      </span>
                    </td>
                    <td className="whitespace-nowrap px-4 py-3 text-[#5F5E5B] dark:text-[#C4C4C4]">
                      {tracker.lastAnnounceTime ? 'Recently' : 'Never'}
                    </td>
                    <td className="px-4 py-3 text-[12px] text-red-600 dark:text-red-400 max-w-[200px] truncate" title={tracker.failureMessage || tracker.warningMessage || ''}>
                      {tracker.failureMessage || tracker.warningMessage || '-'}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {trackers.length > itemsPerPage && (
            <Pagination
              currentPage={currentPage}
              totalPages={totalPages}
              onPageChange={setCurrentPage}
            />
          )}
        </div>
      )}
    </div>
  );
}
