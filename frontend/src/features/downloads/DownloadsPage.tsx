import { Pause, Play, RefreshCw, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { PageSkeleton } from '@/components/PageSkeleton';
import { Pagination } from '@/components/Pagination';
import { ConfirmDeleteModal } from '@/components/ConfirmDeleteModal';
import { useTorrentsQuery } from '@/features/torrents/hooks/useTorrentsQuery';
import { useTorrentActions } from '@/features/torrents/hooks/useTorrentActions';
import { fetchDashboardMetrics } from '@/services/torrents.api';
import { useQuery } from '@tanstack/react-query';
import { formatBytes, formatDuration, formatSpeed, toDisplayTorrentStatus } from '@/utils/formatters';

const statusBadgeClass: Record<string, string> = {
  Downloading: 'bg-[#D3E5EF] text-[#18445B]',
  Completed: 'bg-[#DBEDDB] text-[#1C4D2D]',
  Paused: 'bg-[#EBECD0] text-[#37352F]',
  Queued: 'bg-[#EBECD0] text-[#37352F]',
  Error: 'bg-[#FFE2DD] text-[#5D1715]',
  Stopped: 'bg-[#EBECD0] text-[#37352F]',
  Seeding: 'bg-[#DBEDDB] text-[#1C4D2D]',
};

const progressBarClass: Record<string, string> = {
  Downloading: 'bg-[#2383E2]',
  Completed: 'bg-[#448361]',
  Paused: 'bg-[rgba(120,119,116,0.7)]',
  Queued: 'bg-[rgba(120,119,116,0.7)]',
  Error: 'bg-[#D4403A]',
  Stopped: 'bg-[rgba(120,119,116,0.7)]',
  Seeding: 'bg-[#448361]',
};

export function DownloadsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const page = parseInt(searchParams.get('page') || '1', 10);
  const statusFilter = searchParams.get('status') ?? 'all';
  const searchQuery = searchParams.get('q') ?? '';

  const { data, isLoading, isError } = useTorrentsQuery({
    page,
    pageSize: 10,
    status: statusFilter,
    q: searchQuery
  });

  const metricsQuery = useQuery({
    queryKey: ['dashboardMetrics'],
    queryFn: fetchDashboardMetrics,
    refetchInterval: 1000,
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
    return (
      <section className="flex min-h-0 flex-1 flex-col overflow-y-auto px-6 py-6">
        <PageSkeleton />
      </section>
    );
  }

  let visibleTorrents = data.items;

  const setPage = (newPage: number) => {
    const p = new URLSearchParams(searchParams);
    p.set('page', newPage.toString());
    setSearchParams(p);
  };

  const setStatus = (newStatus: string) => {
    const p = new URLSearchParams(searchParams);
    if (newStatus === 'all') p.delete('status');
    else p.set('status', newStatus);
    p.set('page', '1');
    setSearchParams(p);
  };

  return (
    <section className="flex min-h-0 flex-1 flex-col overflow-hidden bg-white dark:bg-[#111111] px-4 sm:px-8 md:px-16 pb-10 pt-6 sm:pt-10">
      <div className="pb-6">
        <div className="flex items-end justify-between gap-4">
        <div>
            <h1 className="m-0 text-[28px] font-bold leading-[34px] tracking-[-0.01em] text-[#37352F] dark:text-[#E9E9E7]">Downloads</h1>
            <p className="m-0 mt-1 text-[14px] leading-5 text-[#5F5E5B] dark:text-[#E9E9E7]">Manage your active and queued torrents</p>
          </div>
          <div className="flex items-center gap-5">
            <div className="flex items-center gap-1 text-[14px] font-medium text-[#5F5E5B] dark:text-[#E9E9E7]">
              <button 
                type="button" 
                onClick={() => setStatus('all')}
                className={`rounded px-3 py-1 transition-colors hover:bg-[#F7F7F5] dark:hover:bg-[#1A1A1A] ${statusFilter === 'all' ? 'text-[#37352F] dark:text-[#E9E9E7] bg-[rgba(55,53,47,0.08)] dark:bg-[rgba(255,255,255,0.08)]' : ''}`}
              >
                All
              </button>
              <button 
                type="button" 
                onClick={() => setStatus('Downloading')}
                className={`rounded px-3 py-1 transition-colors hover:bg-[#F7F7F5] dark:hover:bg-[#1A1A1A] ${statusFilter === 'Downloading' ? 'text-[#37352F] dark:text-[#E9E9E7] bg-[rgba(55,53,47,0.08)] dark:bg-[rgba(255,255,255,0.08)]' : ''}`}
              >
                Downloading
              </button>
              <button 
                type="button" 
                onClick={() => setStatus('Completed')}
                className={`rounded px-3 py-1 transition-colors hover:bg-[#F7F7F5] dark:hover:bg-[#1A1A1A] ${statusFilter === 'Completed' ? 'text-[#37352F] dark:text-[#E9E9E7] bg-[rgba(55,53,47,0.08)] dark:bg-[rgba(255,255,255,0.08)]' : ''}`}
              >
                Seeding
              </button>
            </div>
          </div>
        </div>
      </div>

      <div className="min-h-0 flex-1 overflow-y-auto">
        <div className="mb-12 flex flex-col h-full">

          {/* ── Mobile card list (hidden on md+) ── */}
          <div className="md:hidden divide-y divide-[#E9E9E7] dark:divide-[#333333] border-t border-[#E9E9E7] dark:border-[#333333]">
            {visibleTorrents.map((item) => {
              const status = toDisplayTorrentStatus(item);
              const progress = Math.max(0, Math.min(100, Math.round(item.progress)));
              const isDownloading = status === 'Downloading' || status === 'Seeding';
              const statusLabel = status;

              return (
                <div key={item.id} className="py-4">
                  <div className="flex items-start justify-between gap-2 mb-2">
                    <Link to={`/torrent/${item.id}`} className="text-[14px] font-medium text-[#37352F] dark:text-[#E9E9E7] hover:underline truncate flex-1">
                      {item.name}
                    </Link>
                    <span className={`shrink-0 rounded px-2 py-0.5 text-[11px] font-medium ${statusBadgeClass[status] ?? statusBadgeClass.Queued}`}>
                      {statusLabel}
                    </span>
                  </div>
                  <div className="flex items-center gap-3 mb-3">
                    <div className="h-[5px] flex-1 overflow-hidden rounded-full bg-[#E9E9E7] dark:bg-[#333333]">
                      <div className={`h-full ${progressBarClass[status] ?? progressBarClass.Queued}`} style={{ width: `${progress}%` }} />
                    </div>
                    <span className="font-mono text-[12px] text-[#5F5E5B] dark:text-[#E9E9E7]">{progress}%</span>
                  </div>
                  <div className="flex items-center justify-between">
                    <span className={`font-mono text-[12px] ${isDownloading ? 'text-[#2383E2]' : 'text-[#5F5E5B] dark:text-[#E9E9E7]'}`}>
                      {status === 'Seeding' ? formatSpeed(item.uploadSpeedBytesPerSecond) : formatSpeed(item.downloadSpeedBytesPerSecond)} · {formatBytes(item.downloadedBytes)}
                    </span>
                    <div className="inline-flex items-center gap-1">
                      {status === 'Error' ? (
                        <button type="button" className="rounded p-1 text-[#5F5E5B] dark:text-[#E9E9E7]" aria-label="Retry">
                          <RefreshCw size={16} />
                        </button>
                      ) : (
                        <button type="button" className="rounded p-1 text-[#5F5E5B] dark:text-[#E9E9E7]"
                          onClick={() => (isDownloading ? pauseMutation.mutate(item.id) : startMutation.mutate(item.id))}
                          aria-label={isDownloading ? 'Pause' : 'Start'}>
                          {isDownloading ? <Pause size={16} /> : <Play size={16} />}
                        </button>
                      )}
                      <button type="button" className="rounded p-1 text-[#5F5E5B] dark:text-[#E9E9E7] hover:text-[#D4403A]"
                        onClick={() => handleDeleteClick(item.id, item.name)} aria-label="Remove">
                        <Trash2 size={16} />
                      </button>
                    </div>
                  </div>
                </div>
              );
            })}
          </div>

          {/* ── Desktop table (hidden on mobile) ── */}
          <div className="hidden md:block overflow-x-auto flex-1">
            <table className="min-w-[980px] w-full border-collapse text-left">
              <thead className="border-b border-[#E9E9E7] dark:border-[#333333] text-[11px] font-semibold uppercase tracking-wider text-[#5F5E5B] dark:text-[#E9E9E7]">
              <tr>
                  <th className="px-3 py-3 font-semibold">Name</th>
                  <th className="px-3 py-3 font-semibold">Status</th>
                  <th className="w-48 px-3 py-3 font-semibold">Progress</th>
                  <th className="px-3 py-3 font-semibold">Speed</th>
                  <th className="px-3 py-3 font-semibold">Size</th>
                  <th className="px-3 py-3 font-semibold">ETA</th>
                  <th className="px-3 py-3 text-right font-semibold">Actions</th>
              </tr>
              </thead>
              <tbody className="divide-y divide-[#E9E9E7] text-[14px]">
                {visibleTorrents.map((item) => {
                  const status = toDisplayTorrentStatus(item);
                  const progress = Math.max(0, Math.min(100, Math.round(item.progress)));
                  const isDownloading = status === 'Downloading' || status === 'Seeding';
                  const statusLabel = status;

                  return (
                    <tr key={item.id} className="group transition-colors hover:bg-[rgba(55,53,47,0.08)] dark:hover:bg-[rgba(255,255,255,0.08)]">
                      <td className="max-w-xs truncate px-3 py-5 font-medium text-[#37352F] dark:text-[#E9E9E7]">
                        <Link to={`/torrent/${item.id}`} className="hover:underline">
                          {item.name}
                        </Link>
                      </td>
                      <td className="px-3 py-5">
                        <span className={`rounded px-2 py-0.5 text-[12px] font-medium ${statusBadgeClass[status] ?? statusBadgeClass.Queued}`}>
                          {statusLabel}
                        </span>
                      </td>
                      <td className="px-3 py-5">
                        <div className="flex items-center gap-3">
                          <div className="h-[6px] flex-1 overflow-hidden rounded-full bg-[#E9E9E7]">
                            <div className={`h-full ${progressBarClass[status] ?? progressBarClass.Queued}`} style={{ width: `${progress}%` }} />
                          </div>
                          <span className={`w-10 text-right font-mono text-[12px] ${status === 'Error' ? 'text-[#D4403A]' : 'text-[#37352F] dark:text-[#E9E9E7]'}`}>{progress}%</span>
                        </div>
                      </td>
                      <td className="px-3 py-5 font-mono text-[12px]">
                        <span className={isDownloading ? 'font-medium text-[#2383E2]' : 'text-[#5F5E5B] dark:text-[#E9E9E7]'}>{status === 'Seeding' ? formatSpeed(item.uploadSpeedBytesPerSecond) : formatSpeed(item.downloadSpeedBytesPerSecond)}</span>
                      </td>
                      <td className="px-3 py-5 text-[#5F5E5B] dark:text-[#E9E9E7]">{formatBytes(item.downloadedBytes)}</td>
                      <td className="px-3 py-5 text-[#5F5E5B] dark:text-[#E9E9E7]">{status === 'Paused' ? '∞' : formatDuration(item.etaSeconds)}</td>
                      <td className="px-3 py-5 text-right">
                        <div className="inline-flex items-center justify-end gap-1">
                          {status === 'Error' ? (
                            <button type="button" className="rounded p-1 text-[#5F5E5B] dark:text-[#E9E9E7] transition-colors hover:bg-white dark:bg-[#111111]" aria-label="Retry torrent">
                              <RefreshCw size={18} />
                            </button>
                          ) : (
                            <button
                              type="button"
                              className="rounded p-1 text-[#5F5E5B] dark:text-[#E9E9E7] transition-colors hover:bg-white dark:bg-[#111111]"
                              onClick={() => (isDownloading ? pauseMutation.mutate(item.id) : startMutation.mutate(item.id))}
                              aria-label={isDownloading ? 'Pause torrent' : 'Start torrent'}
                            >
                              {isDownloading ? <Pause size={18} /> : <Play size={18} />}
                            </button>
                          )}
                          <button
                            type="button"
                            className="rounded p-1 text-[#5F5E5B] dark:text-[#E9E9E7] transition-colors hover:bg-white dark:bg-[#111111] hover:text-[#D4403A]"
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

          {visibleTorrents.length === 0 ? (
            <div className="flex flex-col items-center justify-center py-24 text-center">
              <div className="mb-5 flex h-16 w-16 items-center justify-center rounded-lg border border-[#E9E9E7] dark:border-[#333333] bg-[#F7F7F5] dark:bg-[#1A1A1A]">
                <span className="text-3xl text-[#37352F] dark:text-[#E9E9E7] opacity-50">☁</span>
              </div>
              <h3 className="text-[18px] font-semibold text-[#37352F] dark:text-[#E9E9E7]">No results found</h3>
              <p className="max-w-sm text-[14px] text-[#5F5E5B] dark:text-[#E9E9E7]">Try adjusting your search or filters.</p>
            </div>
          ) : null}

          <div className="flex items-center justify-between px-3 py-4 text-[14px] text-[#5F5E5B] dark:text-[#E9E9E7]">
            <span>{data.totalCount} torrents</span>
            <div className="flex items-center gap-8">
              <span className="text-[12px]">
                Download: <span className="font-semibold text-[#37352F] dark:text-[#E9E9E7]">{formatSpeed(metricsQuery.data?.totalDownloadSpeedBytesPerSecond ?? 0)}</span>
              </span>
              <span className="text-[12px]">
                Upload: <span className="font-semibold text-[#37352F] dark:text-[#E9E9E7]">{formatSpeed(metricsQuery.data?.totalUploadSpeedBytesPerSecond ?? 0)}</span>
              </span>
            </div>
          </div>
        </div>
      </div>

      <ConfirmDeleteModal
        isOpen={deleteTarget !== null}
        torrentName={deleteTarget?.name ?? ''}
        onConfirm={handleDeleteConfirm}
        onCancel={handleDeleteCancel}
      />
    </section>
  );
}