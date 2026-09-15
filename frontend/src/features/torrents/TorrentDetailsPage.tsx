import { Activity, CheckCircle2, Folder, Gauge, List, PlayCircle } from 'lucide-react';
import { useMemo, useState } from 'react';
import { PageSkeleton } from '@/components/PageSkeleton';
import { useParams, useNavigate } from 'react-router-dom';
import { useTorrentsQuery } from '@/features/torrents/hooks/useTorrentsQuery';
import { useTorrentFilesQuery } from '@/features/torrents/hooks/useTorrentFilesQuery';
import { useUpdateFilePriorityMutation } from '@/features/torrents/hooks/useUpdateFilePriorityMutation';
import { formatBytes, formatDuration, formatSpeed, toDisplayTorrentStatus } from '@/utils/formatters';
import { TorrentPeersTab } from '@/features/torrents/components/TorrentPeersTab';
import { TorrentTrackersTab } from '@/features/torrents/components/TorrentTrackersTab';

type TabView = 'files' | 'peers' | 'trackers';

export function TorrentDetailsPage() {
  const { torrentId } = useParams();
  const navigate = useNavigate();
  const [activeTab, setActiveTab] = useState<TabView>('files');
  const { data, isLoading, isError } = useTorrentsQuery();
  const { data: filesData, isLoading: isLoadingFiles } = useTorrentFilesQuery(torrentId);
  const { mutate: updatePriority } = useUpdateFilePriorityMutation();

  const torrent = useMemo(() => {
    if (!data || data.items.length === 0) {
      return null;
    }

    if (!torrentId) {
      return data.items[0];
    }

    return data.items.find((item) => item.id === torrentId) ?? data.items[0];
  }, [data, torrentId]);

  if (isLoading || isError || !torrent) {
    return (
      <section className="flex min-h-0 flex-1 flex-col overflow-y-auto px-6 py-6">
        <PageSkeleton />
      </section>
    );
  }

  const status = toDisplayTorrentStatus(torrent);
  const progress = Math.max(0, Math.min(100, torrent.progress));

  // Total size: sum of all file lengths when metadata is available,
  // otherwise derive from progress (downloadedBytes / progress * 100)
  const totalBytes = filesData && filesData.length > 0
    ? filesData.reduce((sum, f) => sum + f.length, 0)
    : progress > 0
      ? Math.round((torrent.downloadedBytes / progress) * 100)
      : torrent.downloadedBytes;

  const statusPillClass =
    status === 'Downloading'
      ? 'bg-[#dcfce7] text-[#166534]'
      : status === 'Completed'
        ? 'bg-[#dcfce7] text-[#166534]'
        : status === 'Error'
          ? 'bg-[#fee2e2] text-[#991b1b]'
          : 'bg-[#f3f4f6] text-[#6b7280]';

  return (
    <section className="mx-auto w-full max-w-[900px] space-y-10 px-4 sm:px-8 pb-14 pt-6 sm:pt-10">
      <div>
        <h1 className="text-[28px] sm:text-[40px] font-bold leading-tight tracking-tight text-[#37352F] dark:text-[#E9E9E7] break-words">{torrent.name}</h1>
      </div>

      <div className="grid grid-cols-1 gap-x-12 gap-y-2 border-b border-[#e5e7eb] pb-10 md:grid-cols-2">
        <div className="flex items-center gap-4 py-1">
          <div className="flex w-32 items-center gap-2 text-sm text-[#5F5E5B] dark:text-[#E9E9E7]">
            <CheckCircle2 size={18} />
            Status
          </div>
          <span className={`inline-flex items-center gap-1 rounded-full px-2 py-[2px] text-[12px] font-medium ${statusPillClass}`}>
            <span className="h-[6px] w-[6px] animate-pulse rounded-full bg-current" />
            {status === 'Completed' ? 'Seeding' : status}
          </span>
        </div>

        <div className="flex items-center gap-4 py-1">
          <div className="flex w-32 items-center gap-2 text-sm text-[#5F5E5B] dark:text-[#E9E9E7]">
            <Folder size={18} />
            Location
          </div>
          <span className="cursor-pointer text-sm underline decoration-[#e5e7eb] underline-offset-4 hover:decoration-[#6b7280]">{torrent.savePath}</span>
        </div>

        <div className="flex items-center gap-4 py-1">
          <div className="flex w-32 items-center gap-2 text-sm text-[#5F5E5B] dark:text-[#E9E9E7]">
            <List size={18} />
            Size
          </div>
          <span className="text-sm">{formatBytes(totalBytes)}</span>
        </div>

        <div className="flex items-center gap-4 py-1">
          <div className="flex w-32 items-center gap-2 text-sm text-[#5F5E5B] dark:text-[#E9E9E7]">
            <Activity size={18} />
            ETA
          </div>
          <span className="text-sm">{formatDuration(torrent.etaSeconds)}</span>
        </div>

        <div className="flex items-center gap-4 py-1">
          <div className="flex w-32 items-center gap-2 text-sm text-[#5F5E5B] dark:text-[#E9E9E7]">
            <Gauge size={18} />
            Down / Up
          </div>
          <div className="flex items-center gap-2 text-sm">
            <span className="font-medium text-[#22c55e]">{formatSpeed(torrent.downloadSpeedBytesPerSecond)}</span>
            <span className="text-[#5F5E5B] dark:text-[#E9E9E7]">/</span>
            <span className="font-medium text-[#2563eb]">{formatSpeed(torrent.uploadSpeedBytesPerSecond)}</span>
          </div>
        </div>

        <div className="flex items-center gap-4 py-1">
          <div className="flex w-32 items-center gap-2 text-sm text-[#5F5E5B] dark:text-[#E9E9E7]">
            <Activity size={18} />
            Ratio
          </div>
          <span className="text-sm">{torrent.ratio.toFixed(2)}</span>
        </div>
      </div>

      <div className="space-y-2">
        <div className="flex items-end justify-between">
          <h3 className="text-sm font-semibold uppercase tracking-wider text-[#4B4A47] dark:text-[#E9E9E7]">Overall Progress</h3>
          <span className="text-sm font-medium text-[#37352F] dark:text-[#E9E9E7]">
            {progress.toFixed(1)}% <span className="font-normal text-[#5F5E5B] dark:text-[#E9E9E7]">({formatBytes(torrent.downloadedBytes)} / {formatBytes(Math.max(torrent.downloadedBytes, Math.round((torrent.downloadedBytes / Math.max(progress, 1)) * 100)))})</span>
          </span>
        </div>
        <div className="h-3 overflow-hidden rounded-full bg-[#f3f4f6]">
          <div className="h-full bg-[#2563eb] transition-all duration-500" style={{ width: `${progress}%` }} />
        </div>
      </div>

      <div className="mt-10">
        <div className="mb-4 flex items-center justify-between border-b border-[#e5e7eb] dark:border-[#333333]">
          <div className="flex gap-6">
            <button
              onClick={() => setActiveTab('files')}
              className={`flex items-center gap-2 border-b-2 py-3 text-sm font-semibold uppercase tracking-wider transition-colors ${activeTab === 'files' ? 'border-[#2563eb] text-[#2563eb]' : 'border-transparent text-[#5F5E5B] hover:text-[#37352F] dark:text-[#C4C4C4] dark:hover:text-[#E9E9E7]'}`}
            >
              <Folder size={18} /> Files
            </button>
            <button
              onClick={() => setActiveTab('peers')}
              className={`flex items-center gap-2 border-b-2 py-3 text-sm font-semibold uppercase tracking-wider transition-colors ${activeTab === 'peers' ? 'border-[#2563eb] text-[#2563eb]' : 'border-transparent text-[#5F5E5B] hover:text-[#37352F] dark:text-[#C4C4C4] dark:hover:text-[#E9E9E7]'}`}
            >
              <Activity size={18} /> Peers
            </button>
            <button
              onClick={() => setActiveTab('trackers')}
              className={`flex items-center gap-2 border-b-2 py-3 text-sm font-semibold uppercase tracking-wider transition-colors ${activeTab === 'trackers' ? 'border-[#2563eb] text-[#2563eb]' : 'border-transparent text-[#5F5E5B] hover:text-[#37352F] dark:text-[#C4C4C4] dark:hover:text-[#E9E9E7]'}`}
            >
              <List size={18} /> Trackers
            </button>
          </div>
        </div>

        <div className="rounded-lg border border-[#e5e7eb] bg-white dark:border-[#333333] dark:bg-[#111111]">
          {activeTab === 'files' && (
            <>
              <div className="flex items-center justify-between border-b border-[#e5e7eb] px-4 py-3 dark:border-[#333333]">
                <div className="flex items-center gap-2 font-medium text-[#37352F] dark:text-[#E9E9E7]">
                  <Folder size={18} className="text-[#f59e0b]" />
                  {torrent.name}
                </div>
                <div className="flex items-center gap-6 text-sm text-[#5F5E5B] dark:text-[#E9E9E7]">
                  <span className="w-16 text-right">{formatBytes(Math.max(torrent.downloadedBytes, 1))}</span>
                  <span className="w-8 text-right font-medium text-[#37352F] dark:text-[#E9E9E7]">{progress.toFixed(0)}%</span>
                </div>
              </div>
              
              <div className="max-h-[400px] overflow-y-auto">
                {isLoadingFiles ? (
                  <div className="p-4 text-center text-sm text-[#5F5E5B] dark:text-[#E9E9E7]">Loading files...</div>
                ) : filesData && filesData.length > 0 ? (
                  <ul className="divide-y divide-[#e5e7eb] dark:divide-[#333333]">
                    {filesData.map((file, idx) => {
                      const fileProgress = file.length > 0 ? (file.downloadedBytes / file.length) * 100 : 0;
                      const priorityClass = file.priority === 'High' ? 'bg-[#22c55e] text-white' : file.priority === 'DoNotDownload' ? 'bg-[#fee2e2] text-[#991b1b] border-none' : 'border border-[#e5e7eb] text-[#5F5E5B] dark:border-[#333333] dark:text-[#E9E9E7] bg-transparent';
                      return (
                        <li key={idx} className={`flex items-center justify-between px-4 py-3 text-sm transition-colors hover:bg-[#f9fafb] dark:hover:bg-[#1a1a1a] ${file.priority === 'DoNotDownload' ? 'opacity-50' : ''}`}>
                          <div className="flex flex-col gap-1 overflow-hidden pr-4">
                            <span className="truncate text-[#4B4A47] dark:text-[#E9E9E7] pl-6">{file.path}</span>
                            <div className="h-[2px] w-8 rounded-full bg-[#e5e7eb] dark:bg-[#333333] ml-6">
                               <div className="h-full rounded-full bg-[#22c55e]" style={{ width: `${fileProgress}%` }} />
                            </div>
                          </div>
                          <div className="flex flex-shrink-0 items-center gap-2 sm:gap-6">
                            <select 
                              className={`rounded-sm px-1.5 py-1 text-[10px] font-medium uppercase tracking-wide cursor-pointer appearance-none text-center outline-none ${priorityClass}`}
                              value={file.priority}
                              onChange={(e) => {
                                if (torrentId) {
                                  updatePriority({ torrentId, updates: [{ fileIndex: file.index, priority: e.target.value }] });
                                }
                              }}
                            >
                              <option value="DoNotDownload" className="text-black bg-white">Skip</option>
                              <option value="Lowest" className="text-black bg-white">Lowest</option>
                              <option value="Low" className="text-black bg-white">Low</option>
                              <option value="Normal" className="text-black bg-white">Normal</option>
                              <option value="High" className="text-black bg-white">High</option>
                              <option value="Highest" className="text-black bg-white">Highest</option>
                              <option value="Immediate" className="text-black bg-white">Immediate</option>
                            </select>
                            {file.path.match(/\.(mp4|mkv|avi|webm)$/i) && (
                              <button
                                onClick={() => navigate(`/torrent/${torrentId}/stream/${file.index}`)}
                                className="flex items-center gap-1 rounded-sm bg-[#2563eb] px-2 py-1 text-[10px] font-medium uppercase tracking-wide text-white transition-colors hover:bg-[#1d4ed8]"
                                title="Stream Video"
                              >
                                <PlayCircle size={12} />
                                Stream
                              </button>
                            )}
                            <span className="hidden sm:inline w-16 text-right text-[#5F5E5B] dark:text-[#E9E9E7]">{formatBytes(file.length)}</span>
                            <span className="w-8 text-right font-medium text-[#37352F] dark:text-[#E9E9E7]">{fileProgress.toFixed(0)}%</span>
                          </div>
                        </li>
                      );
                    })}
                  </ul>
                ) : (
                   <div className="p-4 text-center text-sm text-[#5F5E5B] dark:text-[#E9E9E7]">No files available yet. Metadata might still be downloading.</div>
                )}
              </div>
            </>
          )}

          {activeTab === 'peers' && torrentId && (
            <div className="max-h-[400px] overflow-y-auto">
              <TorrentPeersTab torrentId={torrentId} />
            </div>
          )}

          {activeTab === 'trackers' && torrentId && (
            <div className="max-h-[400px] overflow-y-auto">
              <TorrentTrackersTab torrentId={torrentId} />
            </div>
          )}
        </div>
      </div>

    </section>
  );
}