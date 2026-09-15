import { Folder, PlayCircle, ChevronRight, ArrowLeft } from 'lucide-react';
import { useMemo } from 'react';
import { PageSkeleton } from '@/components/PageSkeleton';
import { useParams, useNavigate } from 'react-router-dom';
import { useTorrentsQuery } from '@/features/torrents/hooks/useTorrentsQuery';
import { useTorrentFilesQuery } from '@/features/torrents/hooks/useTorrentFilesQuery';
import { formatBytes } from '@/utils/formatters';

const API_BASE = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5000/api/v1';

export function StreamPage() {
  const { torrentId, fileIndex } = useParams();
  const navigate = useNavigate();
  const { data, isLoading, isError } = useTorrentsQuery();
  const { data: filesData, isLoading: isLoadingFiles } = useTorrentFilesQuery(torrentId);

  const file = useMemo(() => {
    if (!filesData || !fileIndex) return null;
    return filesData.find((f) => f.index.toString() === fileIndex) ?? null;
  }, [filesData, fileIndex]);

  const torrent = useMemo(() => {
    if (!data || !torrentId) return null;
    return data.items.find((item) => item.id === torrentId) ?? null;
  }, [data, torrentId]);

  // Desktop build: no auth token needed — API is local and unauthenticated.
  const videoUrl = torrentId && fileIndex
    ? `${API_BASE}/files/stream?torrentId=${torrentId}&fileIndex=${fileIndex}`
    : null;

  if (isLoading || isLoadingFiles || !isError && (!torrent || !file) || !videoUrl) {
    return (
      <section className="flex min-h-0 flex-1 flex-col overflow-y-auto px-6 py-6">
        <PageSkeleton />
      </section>
    );
  }

  const nextFiles = filesData?.filter(f => f.index > (file?.index ?? -1) && f.path.match(/\.(mp4|mkv|avi|webm)$/i)).slice(0, 4) || [];

  return (
    <main className="flex-1 px-4 sm:px-6 lg:px-8 py-6 max-w-[1400px] mx-auto w-full space-y-6">
      {/* Breadcrumbs & Actions */}
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
        <div className="flex items-center flex-wrap gap-2 text-[#5F5E5B] dark:text-[#E9E9E7] text-sm">
          <button onClick={() => navigate('/downloads')} className="hover:text-[#2563eb] transition-colors">Downloads</button>
          <ChevronRight size={14} />
          <button onClick={() => navigate(`/torrent/${torrentId}`)} className="hover:text-[#2563eb] transition-colors truncate max-w-[200px]">{torrent?.name}</button>
          <ChevronRight size={14} />
          <span className="text-[#37352F] dark:text-[#E9E9E7] font-bold truncate max-w-[300px]">{file?.path}</span>
        </div>
        <div className="flex gap-3">
          <button
            onClick={() => navigate(`/torrent/${torrentId}`)}
            className="flex items-center gap-2 px-4 py-2 rounded border border-[#e5e7eb] dark:border-[#333333] text-sm font-medium hover:bg-[#f9fafb] dark:hover:bg-[#1a1a1a] transition-all text-[#37352F] dark:text-[#E9E9E7]"
          >
            <ArrowLeft size={18} />
            Back to Details
          </button>
        </div>
      </div>

      {/* Player Grid Layout */}
      <div className="grid grid-cols-1 lg:grid-cols-4 gap-6">
        {/* Main Player Section */}
        <div className="lg:col-span-3 space-y-6">
          {/* Video Container */}
          <div className="relative aspect-video bg-black overflow-hidden rounded-lg group shadow-sm border border-[#e5e7eb] dark:border-[#333333]">
            <video
              className="w-full h-full"
              controls
              autoPlay
              src={videoUrl}
            />
          </div>

          {/* Media Info Card */}
          <div className="bg-white dark:bg-[#111111] border border-[#e5e7eb] dark:border-[#333333] p-6 rounded-lg">
            <div className="flex flex-wrap items-start justify-between gap-4">
              <div className="space-y-2">
                <h2 className="text-xl sm:text-2xl font-bold text-[#37352F] dark:text-[#E9E9E7] break-words">
                  {file?.path.split('/').pop()}
                </h2>
                <div className="flex flex-wrap items-center gap-4 text-[#5F5E5B] dark:text-[#E9E9E7] text-sm">
                  <span className="bg-[#f3f4f6] dark:bg-[#222222] px-2 py-0.5 rounded text-xs uppercase font-mono tracking-wider">Stream</span>
                  <span className="flex items-center gap-1"><Folder size={16} /> {formatBytes(file?.length ?? 0)}</span>
                  <span className="flex items-center gap-1">
                    <span className="w-2 h-2 bg-green-500 rounded-full animate-pulse mr-1"></span>
                    Sequential Download Active
                  </span>
                </div>
              </div>
            </div>
            
            <div className="mt-6 pt-6 border-t border-[#e5e7eb] dark:border-[#333333]">
              <p className="text-sm text-[#4B4A47] dark:text-[#E9E9E7] leading-relaxed max-w-3xl">
                Streaming powered by MonoTorrent. This file is being downloaded sequentially in the background from the torrent swarm.
              </p>
            </div>
          </div>
        </div>

        {/* Sidebar Content */}
        <div className="space-y-6">
          {/* Related / Up Next Section */}
          <div className="bg-white dark:bg-[#111111] border border-[#e5e7eb] dark:border-[#333333] rounded-lg p-5 flex flex-col h-full">
            <div className="flex justify-between items-center mb-4">
              <h3 className="text-xs font-bold uppercase tracking-widest text-[#5F5E5B] dark:text-[#E9E9E7]">
                Other Video Files
              </h3>
            </div>
            <div className="space-y-4 overflow-y-auto pr-2 max-h-[600px]">
              {nextFiles.length > 0 ? nextFiles.map(f => (
                <div
                  key={f.index}
                  className="group cursor-pointer"
                  onClick={() => navigate(`/torrent/${torrentId}/stream/${f.index}`)}
                >
                  <div className="relative aspect-video rounded-lg overflow-hidden mb-2 bg-[#f3f4f6] dark:bg-[#222222] flex items-center justify-center">
                    <PlayCircle className="text-[rgba(55,53,47,0.3)] dark:text-[rgba(255,255,255,0.3)] group-hover:scale-110 transition-transform duration-300" size={32} />
                  </div>
                  <h4 className="text-sm text-[#37352F] dark:text-[#E9E9E7] line-clamp-2 group-hover:text-[#2563eb] transition-colors">
                    {f.path.split('/').pop()}
                  </h4>
                  <p className="text-[11px] text-[#5F5E5B] dark:text-[#E9E9E7] mt-0.5">{formatBytes(f.length)}</p>
                </div>
              )) : (
                <p className="text-sm text-[#5F5E5B] dark:text-[#E9E9E7]">No other video files in this torrent.</p>
              )}
            </div>
          </div>
        </div>
      </div>
    </main>
  );
}
