import { useQuery } from '@tanstack/react-query';
import { fetchDashboardMetrics } from '@/services/torrents.api';
import { useTorrentsQuery } from '@/features/torrents/hooks/useTorrentsQuery';
import { formatBytes, formatSpeed } from '@/utils/formatters';

export function useDashboardData() {
  const metricsQuery = useQuery({
    queryKey: ['dashboardMetrics'],
    queryFn: fetchDashboardMetrics,
    refetchInterval: 1000,
  });

  const torrentsQuery = useTorrentsQuery({
    page: 1,
    pageSize: 5,
    status: 'Downloading',
  });

  const isLoading = metricsQuery.isLoading || torrentsQuery.isLoading;
  const isError = metricsQuery.isError || torrentsQuery.isError;

  let dashboardData = null;

  if (metricsQuery.data && torrentsQuery.data) {
    const { activeDownloads, completedTorrents, totalDownloadedBytes, totalUploadedBytes, totalDownloadSpeedBytesPerSecond, totalUploadSpeedBytesPerSecond } = metricsQuery.data;
    
    const torrents = torrentsQuery.data.items;

    const metrics = [
      { label: 'Active Downloads', value: String(activeDownloads), detail: 'Downloading torrents' },
      { label: 'Download Speed', value: formatSpeed(totalDownloadSpeedBytesPerSecond), detail: 'Live aggregate speed' },
      { label: 'Upload Speed', value: formatSpeed(totalUploadSpeedBytesPerSecond), detail: 'Live aggregate speed' },
      { label: 'Storage', value: formatBytes(totalDownloadedBytes), detail: 'Downloaded data' },
      { label: 'Completed', value: String(completedTorrents), detail: 'Finished torrents' },
    ];

    const activity = torrents.slice(0, 7).flatMap((torrent, index) => {
      const download = Math.max(12, Math.min(64, Math.round(torrent.downloadSpeedBytesPerSecond / 1_000_000) * 8 + 12 + index * 2));
      const upload = Math.max(10, Math.min(52, Math.round(torrent.uploadSpeedBytesPerSecond / 1_000_000) * 6 + 10 + index * 2));

      return [
        { label: torrent.name.slice(0, 3), download, upload },
      ];
    });

    dashboardData = {
      metrics,
      torrents,
      activity,
      totals: {
        totalDownloaded: totalDownloadedBytes,
        totalUploaded: totalUploadedBytes,
      },
    };
  }

  return {
    isLoading,
    isError,
    data: dashboardData,
  };
}