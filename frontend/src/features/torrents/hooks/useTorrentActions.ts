import { useCallback, useState } from 'react';
import { type QueryClient, type QueryKey, useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { addTorrent, pauseTorrent, removeTorrent, startTorrent, stopTorrent } from '@/services/torrents.api';
import type { DashboardMetricsDto, PagedResultDto, TorrentDto, TorrentStatus } from '@/app/types';

function extractErrorMessage(error: unknown, fallback: string): string {
  if (error instanceof Error && error.message) {
    return error.message;
  }
  return fallback;
}

type OptimisticSnapshot = {
  torrentsQueries: Array<[QueryKey, PagedResultDto<TorrentDto> | undefined]>;
  singleTorrent: TorrentDto | undefined;
  metrics: DashboardMetricsDto | undefined;
};

async function applyOptimisticTorrentPatch(
  queryClient: QueryClient,
  torrentId: string,
  transformItem: (item: TorrentDto) => TorrentDto | null,
  transformMetrics?: (metrics: DashboardMetricsDto, matched?: TorrentDto) => DashboardMetricsDto
): Promise<OptimisticSnapshot> {
  await Promise.all([
    queryClient.cancelQueries({ queryKey: ['torrents'] }),
    queryClient.cancelQueries({ queryKey: ['torrent', torrentId] }),
    queryClient.cancelQueries({ queryKey: ['dashboardMetrics'] }),
  ]);

  const torrentsQueries = queryClient.getQueriesData<PagedResultDto<TorrentDto>>({ queryKey: ['torrents'] });
  const singleTorrent = queryClient.getQueryData<TorrentDto>(['torrent', torrentId]);
  const metrics = queryClient.getQueryData<DashboardMetricsDto>(['dashboardMetrics']);

  let matchedItem: TorrentDto | undefined = singleTorrent;

  queryClient.setQueriesData<PagedResultDto<TorrentDto>>({ queryKey: ['torrents'] }, (oldData) => {
    if (!oldData?.items) return oldData;

    const nextItems: TorrentDto[] = [];
    let removedCount = 0;

    for (const item of oldData.items) {
      if (item.id !== torrentId) {
        nextItems.push(item);
        continue;
      }

      matchedItem ??= item;
      const updated = transformItem(item);
      if (updated === null) {
        removedCount++;
      } else {
        nextItems.push(updated);
      }
    }

    return {
      ...oldData,
      items: nextItems,
      totalCount: Math.max(0, oldData.totalCount - removedCount),
    };
  });

  queryClient.setQueryData<TorrentDto>(['torrent', torrentId], (old) => {
    if (!old) return old;
    return transformItem(old) ?? old;
  });

  if (metrics && transformMetrics) {
    queryClient.setQueryData<DashboardMetricsDto>(['dashboardMetrics'], transformMetrics(metrics, matchedItem));
  }

  return { torrentsQueries, singleTorrent, metrics };
}

function rollbackOptimisticTorrentPatch(
  queryClient: QueryClient,
  torrentId: string,
  snapshot: OptimisticSnapshot | undefined
) {
  if (!snapshot) return;
  for (const [key, data] of snapshot.torrentsQueries) {
    queryClient.setQueryData(key, data);
  }
  queryClient.setQueryData(['torrent', torrentId], snapshot.singleTorrent);
  queryClient.setQueryData(['dashboardMetrics'], snapshot.metrics);
}

function optimisticStatusPatch(
  status: TorrentStatus | ((item: TorrentDto) => TorrentStatus),
  clearSpeeds: boolean
): (item: TorrentDto) => TorrentDto {
  return (item) => ({
    ...item,
    status: typeof status === 'function' ? status(item) : status,
    errorMessage: null,
    ...(clearSpeeds
      ? { downloadSpeedBytesPerSecond: 0, uploadSpeedBytesPerSecond: 0, etaSeconds: null }
      : {}),
  });
}

export function useTorrentActions() {
  const queryClient = useQueryClient();
  const [deletingIds, setDeletingIds] = useState<Set<string>>(() => new Set());

  const invalidateTorrentQueries = useCallback(
    async (torrentId?: string) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['torrents'] }),
        queryClient.invalidateQueries({ queryKey: ['dashboardMetrics'] }),
        torrentId
          ? queryClient.invalidateQueries({ queryKey: ['torrent', torrentId] })
          : queryClient.invalidateQueries({ queryKey: ['torrent'] }),
      ]);
    },
    [queryClient]
  );

  const addMutation = useMutation({
    mutationFn: addTorrent,
    onSuccess: (created) => invalidateTorrentQueries(created?.id),
    onError: (err) => toast.error(extractErrorMessage(err, 'Failed to add torrent.')),
  });

  const startMutation = useMutation({
    mutationFn: startTorrent,
    onMutate: (torrentId) =>
      applyOptimisticTorrentPatch(
        queryClient,
        torrentId,
        optimisticStatusPatch((item) => (item.progress >= 100 ? 'Seeding' : 'Downloading'), false)
      ),
    onError: (err, torrentId, snapshot) => {
      rollbackOptimisticTorrentPatch(queryClient, torrentId, snapshot);
      toast.error(extractErrorMessage(err, 'Failed to start torrent.'));
    },
    onSettled: (_data, _err, torrentId) => invalidateTorrentQueries(torrentId),
  });

  const pauseMutation = useMutation({
    mutationFn: pauseTorrent,
    onMutate: (torrentId) =>
      applyOptimisticTorrentPatch(queryClient, torrentId, optimisticStatusPatch('Paused', true)),
    onError: (err, torrentId, snapshot) => {
      rollbackOptimisticTorrentPatch(queryClient, torrentId, snapshot);
      toast.error(extractErrorMessage(err, 'Failed to pause torrent.'));
    },
    onSettled: (_data, _err, torrentId) => invalidateTorrentQueries(torrentId),
  });

  const stopMutation = useMutation({
    mutationFn: stopTorrent,
    onMutate: (torrentId) =>
      applyOptimisticTorrentPatch(queryClient, torrentId, optimisticStatusPatch('Stopped', true)),
    onError: (err, torrentId, snapshot) => {
      rollbackOptimisticTorrentPatch(queryClient, torrentId, snapshot);
      toast.error(extractErrorMessage(err, 'Failed to stop torrent.'));
    },
    onSettled: (_data, _err, torrentId) => invalidateTorrentQueries(torrentId),
  });

  const removeMutation = useMutation({
    mutationFn: removeTorrent,
    onMutate: async (torrentId) => {
      setDeletingIds((prev) => {
        const next = new Set(prev);
        next.add(torrentId);
        return next;
      });
      return applyOptimisticTorrentPatch(
        queryClient,
        torrentId,
        () => null,
        (metrics, matched) => ({
          ...metrics,
          totalTorrents: Math.max(0, metrics.totalTorrents - 1),
          activeDownloads:
            matched && (matched.status === 'Downloading' || matched.status === 'Seeding')
              ? Math.max(0, metrics.activeDownloads - 1)
              : metrics.activeDownloads,
          completedTorrents:
            matched && (matched.status === 'Completed' || matched.status === 'Seeding' || matched.progress >= 100)
              ? Math.max(0, metrics.completedTorrents - 1)
              : metrics.completedTorrents,
        })
      );
    },
    onError: (err, torrentId, snapshot) => {
      rollbackOptimisticTorrentPatch(queryClient, torrentId, snapshot);
      toast.error(extractErrorMessage(err, 'Failed to remove torrent.'));
    },
    onSettled: (_data, _err, torrentId) => {
      setDeletingIds((prev) => {
        if (!prev.has(torrentId)) return prev;
        const next = new Set(prev);
        next.delete(torrentId);
        return next;
      });
      return invalidateTorrentQueries(torrentId);
    },
  });

  const isDeleting = useCallback(
    (torrentId: string) => deletingIds.has(torrentId),
    [deletingIds]
  );

  return {
    addMutation,
    startMutation,
    pauseMutation,
    stopMutation,
    removeMutation,
    isDeleting,
  };
}
