import { AlertCircle, CheckCircle2, List, Pause, PauseCircle, Play, PlayCircle, RefreshCw, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { Link } from 'react-router-dom';
import { PageSkeleton } from '@/components/PageSkeleton';
import { ConfirmDeleteModal } from '@/components/ConfirmDeleteModal';
import { useDashboardData } from '@/features/torrents/hooks/useDashboardData';
import { useTorrentActions } from '@/features/torrents/hooks/useTorrentActions';
import { formatBytes, formatDuration, formatSpeed, toDisplayTorrentStatus } from '@/utils/formatters';

const getTransferIcon = (status: string) => {
  if (status === 'Completed') return { icon: CheckCircle2, iconClass: 'text-[#0B6E4F]' };
  if (status === 'Downloading') return { icon: PlayCircle, iconClass: 'text-[#2383E2]' };
  if (status === 'Error') return { icon: AlertCircle, iconClass: 'text-[#D4403A]' };
  return { icon: PauseCircle, iconClass: 'text-[#5F5E5B] dark:text-[#E9E9E7]' };
};

export function DashboardPage() {
  const { data, isLoading, isError } = useDashboardData();
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
      <main className="mx-auto min-h-screen max-w-5xl bg-white dark:bg-[#111111] px-6 pb-10 pt-10">
        <PageSkeleton />
      </main>
    );
  }

  return (
    <main className="mx-auto min-h-screen max-w-5xl bg-white dark:bg-[#111111] px-4 sm:px-6 pb-10 pt-6 sm:pt-10">
      <div className="mb-8">
        <h2 className="mb-1 text-[40px] font-bold tracking-tight text-[#37352F] dark:text-[#E9E9E7]">Dashboard</h2>
        <p className="mb-6 text-[16px] text-[#5F5E5B] dark:text-[#E9E9E7]">Real-time overview of your network traffic and active nodes.</p>
        <div className="mb-6 border-b border-[#EDEDEB] dark:border-[#333333]" />
      </div>

      <section className="mb-8 grid grid-cols-1 gap-2 md:grid-cols-3 lg:grid-cols-5">
        {data.metrics.map((metric) => (
          <div key={metric.label} className="rounded-md border border-[#EDEDEB] dark:border-[#333333] bg-white dark:bg-[#111111] p-2 transition-colors hover:bg-[#F7F7F5] dark:hover:bg-[#1A1A1A]">
            <span className="mb-1 block text-[12px] font-medium text-[#5F5E5B] dark:text-[#E9E9E7]">{metric.label}</span>
            <div className="flex items-baseline gap-1">
              <span className={`text-2xl font-bold ${metric.label === 'Storage' ? 'text-[#D9730D]' : 'text-[#37352F] dark:text-[#E9E9E7]'}`}>{metric.value}</span>
            </div>
            {metric.detail ? <span className="mt-1 block text-[11px] font-medium text-[#5F5E5B] dark:text-[#E9E9E7]">{metric.detail}</span> : null}
          </div>
        ))}
      </section>

      <div>
        <div className="mb-4 flex items-center justify-between">
          <h3 className="flex items-center gap-2 text-[16px] font-semibold text-[#37352F] dark:text-[#E9E9E7]">
            <List size={18} /> Active Transfers
          </h3>
        </div>

        <div className="space-y-px border-t border-[#EDEDEB] dark:border-[#333333]">
          {data.torrents.slice(0, 5).map((torrent) => {
            const status = toDisplayTorrentStatus(torrent);
            const { icon: Icon, iconClass } = getTransferIcon(status);
            const progress = Math.max(0, Math.min(100, Math.round(torrent.progress)));
            const isDownloading = status === 'Downloading' || status === 'Seeding';

            const progressLabel =
              status === 'Seeding'
                ? `100% • Seeding (Ratio: ${torrent.ratio.toFixed(1)})`
                : status === 'Completed'
                  ? 'Completed'
                  : status === 'Error'
                    ? 'Error'
                    : `${progress}% • ${formatBytes(torrent.downloadedBytes)} / ${formatBytes(Math.max(torrent.downloadedBytes, Math.round((torrent.downloadedBytes / Math.max(progress, 1)) * 100)))} • ${formatDuration(torrent.etaSeconds)} left`;

            const speed = status === 'Seeding' ? formatSpeed(torrent.uploadSpeedBytesPerSecond) : isDownloading ? formatSpeed(torrent.downloadSpeedBytesPerSecond) : formatSpeed(torrent.uploadSpeedBytesPerSecond);
            const speedClass = isDownloading ? 'text-[#2383E2]' : 'text-[#5F5E5B] dark:text-[#E9E9E7]';

            return (
              <div key={torrent.id} className="group -mx-2 flex items-center rounded px-2 py-4 transition-colors hover:bg-[#F7F7F5] dark:hover:bg-[#1A1A1A]">
                <div className="flex w-8 justify-center">
                  <Icon size={20} className={iconClass} />
                </div>

                <div className="min-w-0 flex-1 px-4">
                  <h4 className="truncate text-[14px] font-medium text-[#37352F] dark:text-[#E9E9E7]">
                    <Link to={`/torrent/${torrent.id}`} className="hover:underline">
                      {torrent.name}
                    </Link>
                  </h4>
                  <div className="mt-1 flex items-center gap-4">
                    <div className="h-1 w-32 overflow-hidden rounded-full bg-[#EDEDEB] dark:bg-[#333333]">
                      <div className={`h-full rounded-full ${status === 'Error' ? 'bg-[#D4403A]' : status === 'Seeding' || status === 'Completed' ? 'bg-[#0B6E4F]' : 'bg-[#2383E2]'}`} style={{ width: `${progress}%` }} />
                    </div>
                    <span className="font-mono text-[12px] text-[#5F5E5B] dark:text-[#E9E9E7]">{progressLabel}</span>
                  </div>
                </div>

                <div className="flex items-center gap-6 text-right">
                  <span className={`font-mono text-[12px] ${speedClass}`}>{speed}</span>
                  <div className="inline-flex items-center justify-end gap-1">
                    {status === 'Error' ? (
                      <button type="button" className="rounded p-1 text-[#5F5E5B] dark:text-[#E9E9E7] transition-colors hover:bg-white dark:bg-[#111111]" aria-label="Retry torrent">
                        <RefreshCw size={18} />
                      </button>
                    ) : (
                      <button
                        type="button"
                        className="rounded p-1 text-[#5F5E5B] dark:text-[#E9E9E7] transition-colors hover:bg-white dark:bg-[#111111]"
                        onClick={(e) => { e.preventDefault(); isDownloading ? pauseMutation.mutate(torrent.id) : startMutation.mutate(torrent.id); }}
                        aria-label={isDownloading ? 'Pause torrent' : 'Start torrent'}
                      >
                        {isDownloading ? <Pause size={18} /> : <Play size={18} />}
                      </button>
                    )}

                    <button
                      type="button"
                      className="rounded p-1 text-[#5F5E5B] dark:text-[#E9E9E7] transition-colors hover:bg-[rgba(55,53,47,0.08)] dark:hover:bg-[#111111] hover:text-[#D4403A]"
                      onClick={(e) => { e.preventDefault(); handleDeleteClick(torrent.id, torrent.name); }}
                      aria-label="Remove torrent"
                    >
                      <Trash2 size={18} />
                    </button>
                  </div>
                </div>
              </div>
            );
          })}
          
          {data.torrents.length === 0 && (
             <div className="py-8 text-center text-[14px] text-[#5F5E5B] dark:text-[#E9E9E7]">
               No active transfers
             </div>
          )}
        </div>


      </div>
      
      <ConfirmDeleteModal
        isOpen={!!deleteTarget}
        title="Remove Torrent"
        message={`Are you sure you want to remove "${deleteTarget?.name}"?`}
        onConfirm={handleDeleteConfirm}
        onCancel={handleDeleteCancel}
      />
    </main>
  );
}
