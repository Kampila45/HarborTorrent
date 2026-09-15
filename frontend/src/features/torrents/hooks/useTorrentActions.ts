import { useMutation, useQueryClient } from '@tanstack/react-query';
import { addTorrent, pauseTorrent, removeTorrent, startTorrent, stopTorrent } from '@/services/torrents.api';

export function useTorrentActions() {
  const queryClient = useQueryClient();

  const invalidateTorrents = async () => {
    await queryClient.invalidateQueries({ queryKey: ['torrents'] });
  };

  const addMutation = useMutation({
    mutationFn: addTorrent,
    onSuccess: invalidateTorrents,
  });

  const startMutation = useMutation({
    mutationFn: startTorrent,
    onSuccess: invalidateTorrents,
  });

  const pauseMutation = useMutation({
    mutationFn: pauseTorrent,
    onSuccess: invalidateTorrents,
  });

  const stopMutation = useMutation({
    mutationFn: stopTorrent,
    onSuccess: invalidateTorrents,
  });

  const removeMutation = useMutation({
    mutationFn: removeTorrent,
    onSuccess: invalidateTorrents,
  });

  return {
    addMutation,
    startMutation,
    pauseMutation,
    stopMutation,
    removeMutation,
  };
}