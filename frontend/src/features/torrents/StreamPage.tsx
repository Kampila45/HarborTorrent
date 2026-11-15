import { AlertCircle, ArrowLeft, ChevronRight, Folder, PlayCircle, RefreshCw } from 'lucide-react';
import { useMemo, useState } from 'react';
import { PageSkeleton } from '@/components/PageSkeleton';
import { Button } from '@/components/ui/Button';
import { PageErrorState } from '@/components/ui/PageErrorState';
import { useParams, useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { fetchStreamToken, fetchTorrent } from '@/services/torrents.api';
import { useTorrentFilesQuery } from '@/features/torrents/hooks/useTorrentFilesQuery';
import { getSessionConfig } from '@/services/session';
import { formatBytes } from '@/utils/formatters';

export function StreamPage() {
  const { torrentId, fileIndex } = useParams();
  const navigate = useNavigate();
  const [playbackError, setPlaybackError] = useState<string | null>(null);
  const parsedFileIndex = fileIndex !== undefined ? Number.parseInt(fileIndex, 10) : Number.NaN;

  const {
    data: torrent,
    isLoading: isLoadingTorrent,
    isError: isTorrentError,
    refetch: refetchTorrent,
  } = useQuery({
    queryKey: ['torrent', torrentId],
    queryFn: () => fetchTorrent(torrentId!),
    enabled: !!torrentId,
  });

  const {
    data: filesData,
    isLoading: isLoadingFiles,
    isError: isFilesError,
    refetch: refetchFiles,
  } = useTorrentFilesQuery(torrentId);

  const file = useMemo(() => {
    if (!filesData || Number.isNaN(parsedFileIndex)) return null;
    return filesData.find((f) => f.index === parsedFileIndex) ?? null;
  }, [filesData, parsedFileIndex]);

  const {
    data: streamTokenData,
    isLoading: isLoadingToken,
    isError: isTokenError,
    refetch: refetchToken,
  } = useQuery({
    queryKey: ['streamToken', torrentId, parsedFileIndex],
    queryFn: () => fetchStreamToken(torrentId!, parsedFileIndex),
    enabled: !!torrentId && !Number.isNaN(parsedFileIndex) && parsedFileIndex >= 0 && !!file,
    staleTime: 60 * 60 * 1000, // Token is valid for 4 hours; refresh after 1 hour
  });

  const videoUrl = useMemo(() => {
    if (!torrentId || Number.isNaN(parsedFileIndex) || !streamTokenData?.token) {
      return null;
    }
    const { apiBaseUrl } = getSessionConfig();
    const params = new URLSearchParams({
      torrentId,
      fileIndex: parsedFileIndex.toString(),
      mediaToken: streamTokenData.token,
    });
    return `${apiBaseUrl}/files/stream?${params.toString()}`;
  }, [torrentId, parsedFileIndex, streamTokenData?.token]);

  if (!torrentId || Number.isNaN(parsedFileIndex) || parsedFileIndex < 0) {
    return (
      <section className="flex min-h-0 flex-1 flex-col">
        <PageErrorState
          message="Invalid torrent or file specified for streaming."
          actions={
            <Button variant="outline" onClick={() => navigate('/downloads')}>
              Back to Downloads
            </Button>
          }
        />
      </section>
    );
  }

  if (isLoadingTorrent || isLoadingFiles || (file && isLoadingToken)) {
    return (
      <section className="flex min-h-0 flex-1 flex-col overflow-y-auto px-6 py-6">
        <PageSkeleton />
      </section>
    );
  }

  if (isTorrentError || isFilesError || isTokenError) {
    return (
      <section className="flex min-h-0 flex-1 flex-col">
        <PageErrorState
          message="Unable to load stream"
          description="Could not initialize the authenticated media stream for this file."
          actions={
            <>
              <Button
                onClick={() => {
                  setPlaybackError(null);
                  refetchTorrent();
                  refetchFiles();
                  refetchToken();
                }}
              >
                <RefreshCw size={14} />
                Retry
              </Button>
              <Button variant="outline" onClick={() => navigate(`/torrent/${torrentId}`)}>
                <ArrowLeft size={14} />
                Back to Details
              </Button>
            </>
          }
        />
      </section>
    );
  }

  if (!torrent || !file || !videoUrl) {
    return (
      <section className="flex min-h-0 flex-1 flex-col">
        <PageErrorState
          message="File unavailable. The torrent or selected file could not be found."
          tone="muted"
          actions={
            <Button variant="outline" onClick={() => navigate(`/torrent/${torrentId}`)}>
              <ArrowLeft size={14} />
              Back to Details
            </Button>
          }
        />
      </section>
    );
  }

  const nextFiles = filesData?.filter(f => f.index !== file.index && f.path.match(/\.(mp4|mkv|avi|webm|mov|m4v|ogg)$/i)).slice(0, 4) || [];

  return (
    <main className="flex-1 px-4 sm:px-6 lg:px-8 py-6 max-w-[1400px] mx-auto w-full space-y-6">
      {/* Breadcrumbs & Actions */}
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
        <div className="flex items-center flex-wrap gap-2 text-[#5F5E5B] dark:text-[#E9E9E7] text-sm">
          <button onClick={() => navigate('/downloads')} className="hover:text-[#2563eb] transition-colors">Downloads</button>
          <ChevronRight size={14} />
          <button onClick={() => navigate(`/torrent/${torrentId}`)} className="hover:text-[#2563eb] transition-colors truncate max-w-[200px]">{torrent.name}</button>
          <ChevronRight size={14} />
          <span className="text-[#37352F] dark:text-[#E9E9E7] font-bold truncate max-w-[300px]">{file.path}</span>
        </div>
        <div className="flex gap-3">
          <Button variant="outline" onClick={() => navigate(`/torrent/${torrentId}`)}>
            <ArrowLeft size={14} />
            Back to Details
          </Button>
        </div>
      </div>

      {/* Player Grid Layout */}
      <div className="grid grid-cols-1 lg:grid-cols-4 gap-6">
        {/* Main Player Section */}
        <div className="lg:col-span-3 space-y-6">
          {/* Video Container */}
          <div className="relative aspect-video bg-black overflow-hidden rounded-lg group shadow-sm border border-[#e5e7eb] dark:border-[#333333]">
            {playbackError ? (
              <div className="flex h-full w-full flex-col items-center justify-center gap-3 p-6 text-center text-white">
                <AlertCircle size={36} className="text-[#D4403A]" />
                <p className="text-sm font-medium">{playbackError}</p>
                <Button
                  onClick={() => {
                    setPlaybackError(null);
                    refetchToken();
                  }}
                >
                  <RefreshCw size={14} />
                  Reload Stream
                </Button>
              </div>
            ) : (
              <video
                key={videoUrl}
                className="w-full h-full"
                controls
                autoPlay
                src={videoUrl}
                onError={() =>
                  setPlaybackError(
                    'Playback failed. The media format may not be supported by this WebView, or the stream is still buffering metadata.'
                  )
                }
              />
            )}
          </div>

          {/* Media Info Card */}
          <div className="bg-white dark:bg-[#111111] border border-[#e5e7eb] dark:border-[#333333] p-6 rounded-lg">
            <div className="flex flex-wrap items-start justify-between gap-4">
              <div className="space-y-2">
                <h2 className="text-xl sm:text-2xl font-bold text-[#37352F] dark:text-[#E9E9E7] break-words">
                  {file.path.split(/[\\/]/).pop()}
                </h2>
                <div className="flex flex-wrap items-center gap-4 text-[#5F5E5B] dark:text-[#E9E9E7] text-sm">
                  <span className="bg-[#f3f4f6] dark:bg-[#222222] px-2 py-0.5 rounded text-xs uppercase font-mono tracking-wider">Stream</span>
                  <span className="flex items-center gap-1"><Folder size={16} /> {formatBytes(file.length)}</span>
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
                  onClick={() => {
                    setPlaybackError(null);
                    navigate(`/torrent/${torrentId}/stream/${f.index}`);
                  }}
                >
                  <div className="relative aspect-video rounded-lg overflow-hidden mb-2 bg-[#f3f4f6] dark:bg-[#222222] flex items-center justify-center">
                    <PlayCircle className="text-[rgba(55,53,47,0.3)] dark:text-[rgba(255,255,255,0.3)] group-hover:scale-110 transition-transform duration-300" size={32} />
                  </div>
                  <h4 className="text-sm text-[#37352F] dark:text-[#E9E9E7] line-clamp-2 group-hover:text-[#2563eb] transition-colors">
                    {f.path.split(/[\\/]/).pop()}
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
