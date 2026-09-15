import { useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { Play, Pause, Trash2 } from 'lucide-react';
import { useTorrentsQuery } from '@/features/torrents/hooks/useTorrentsQuery';
import { useTorrentActions } from '@/features/torrents/hooks/useTorrentActions';
import { ConfirmDeleteModal } from '@/components/ConfirmDeleteModal';
import { PageSkeleton } from '@/components/PageSkeleton';
import { Pagination } from '@/components/Pagination';
import { TorrentStatusBadge } from '@/features/torrents/components/TorrentStatusBadge';
import { formatBytes, formatSpeed, toDisplayTorrentStatus } from '@/utils/formatters';

export function CompletedPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const page = parseInt(searchParams.get('page') || '1', 10);

  const { data, isLoading, isError } = useTorrentsQuery({
    page,
    pageSize: 10,
    status: 'Completed'
  });

  const { pauseMutation, removeMutation, startMutation } = useTorrentActions();
  const [deleteTarget, setDeleteTarget] = useState<{ id: string; name: string } | null>(null);

  const handleDeleteClick = (id: string, name: string) => setDeleteTarget({ id, name });
  const handleDeleteConfirm = () => {
    if (deleteTarget) removeMutation.mutate(deleteTarget.id);
    setDeleteTarget(null);
  };
  const handleDeleteCancel = () => setDeleteTarget(null);

  if (isLoading || isError || !data) {
    return <section className="flex min-h-0 flex-1 flex-col overflow-y-auto px-6 py-6"><PageSkeleton /></section>;
  }

  const completedTorrents = data.items;

  const setPage = (newPage: number) => {
    const p = new URLSearchParams(searchParams);
    p.set('page', newPage.toString());
    setSearchParams(p);
  };

  return (
    <section className="flex min-h-0 flex-1 flex-col overflow-y-auto px-6 py-6">
      <div className="rounded-lg border border-[#E9E9E7] dark:border-[#333333] bg-white dark:bg-[#111111] p-5 shadow-sm">
        <div className="flex items-center justify-between">
          <div>
            <p className="m-0 text-[11px] font-semibold uppercase tracking-[0.12em] text-[#5F5E5B] dark:text-[#E9E9E7]">Completed</p>
            <h1 className="m-0 text-[28px] font-bold leading-tight tracking-[-0.01em] text-[#37352F] dark:text-[#E9E9E7]">Finished torrents</h1>
          </div>
          <p className="m-0 font-mono text-[14px] text-[#5F5E5B] dark:text-[#E9E9E7]">{data.totalCount} torrents</p>
        </div>

        <div className="mt-6 overflow-x-auto">
          <table className="w-full min-w-[900px] border-collapse text-left text-[14px]">
            <thead className="border-b border-[#E9E9E7] dark:border-[#333333] text-[11px] font-semibold uppercase tracking-wider text-[#5F5E5B] dark:text-[#E9E9E7]">
              <tr>
                <th className="px-3 py-3 font-semibold">Name</th>
                <th className="px-3 py-3 font-semibold">Status</th>
                <th className="px-3 py-3 font-semibold">Ratio</th>
                <th className="px-3 py-3 font-semibold">Upload Speed</th>
                <th className="px-3 py-3 font-semibold">Uploaded</th>
                <th className="px-3 py-3 font-semibold">Total Size</th>
                <th className="px-3 py-3 font-semibold text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#E9E9E7] dark:divide-[#333333] text-[14px]">
              {completedTorrents.map((item) => {
                const status = toDisplayTorrentStatus(item);
                return (
                <tr key={item.id} className="group transition-colors hover:bg-[rgba(55,53,47,0.08)] dark:hover:bg-[rgba(255,255,255,0.08)]">
                  <td className="max-w-[320px] truncate px-3 py-4 font-medium text-[#37352F] dark:text-[#E9E9E7]">
                    <Link to={`/torrent/${item.id}`} className="hover:underline">
                      {item.name}
                    </Link>
                  </td>
                  <td className="px-3 py-4"><TorrentStatusBadge status={status} /></td>
                  <td className="px-3 py-4 font-mono text-[#5F5E5B] dark:text-[#E9E9E7]">{item.ratio.toFixed(2)}</td>
                  <td className="px-3 py-4 font-mono text-[#2383E2]">{status === 'Seeding' ? formatSpeed(item.uploadSpeedBytesPerSecond) : '-'}</td>
                  <td className="px-3 py-4 text-[#5F5E5B] dark:text-[#E9E9E7]">{formatBytes(item.uploadedBytes)}</td>
                  <td className="px-3 py-4 text-[#5F5E5B] dark:text-[#E9E9E7]">{formatBytes(item.downloadedBytes)}</td>
                  <td className="px-3 py-4 text-right">
                    <div className="inline-flex items-center justify-end gap-1">
                      <button
                        type="button"
                        className="rounded p-1 text-[rgba(55,53,47,0.65)] dark:text-[#E9E9E7] transition-colors hover:bg-[rgba(55,53,47,0.08)] dark:hover:bg-[#111111]"
                        onClick={() => (status === 'Seeding' ? pauseMutation.mutate(item.id) : startMutation.mutate(item.id))}
                        aria-label={status === 'Seeding' ? 'Pause seeding' : 'Start seeding'}
                      >
                        {status === 'Seeding' ? <Pause size={18} /> : <Play size={18} />}
                      </button>
                      <button
                        type="button"
                        className="rounded p-1 text-[rgba(55,53,47,0.65)] dark:text-[#E9E9E7] transition-colors hover:bg-[rgba(55,53,47,0.08)] dark:hover:bg-[#111111] hover:text-[#D4403A]"
                        onClick={() => handleDeleteClick(item.id, item.name)}
                        aria-label="Remove torrent"
                      >
                        <Trash2 size={18} />
                      </button>
                    </div>
                  </td>
                </tr>
                );
              })}
            </tbody>
          </table>
        </div>
        <Pagination 
          currentPage={data.pageNumber} 
          totalPages={data.totalPages} 
          onPageChange={setPage} 
        />
      </div>
      <ConfirmDeleteModal
        isOpen={!!deleteTarget}
        title="Remove Torrent"
        message={`Are you sure you want to remove "${deleteTarget?.name}"?`}
        onConfirm={handleDeleteConfirm}
        onCancel={handleDeleteCancel}
      />
    </section>
  );
}