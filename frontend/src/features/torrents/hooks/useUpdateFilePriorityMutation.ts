import { useMutation, useQueryClient } from '@tanstack/react-query';
import { updateFilePriority } from '@/services/torrents.api';
import type { FilePriorityUpdateDto } from '@/app/types';

export function useUpdateFilePriorityMutation() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ torrentId, updates }: { torrentId: string; updates: FilePriorityUpdateDto[] }) =>
      updateFilePriority(torrentId, updates),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: ['torrent-files', variables.torrentId] });
    },
  });
}
