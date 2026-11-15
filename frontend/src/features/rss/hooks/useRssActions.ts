import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { addRssFeed, addRssFilter, deleteRssFeed, deleteRssFilter, fetchRssFeeds, pollRssFeed } from '@/services/rss.api';

export function useRssQueries() {
  return useQuery({
    queryKey: ['rssFeeds'],
    queryFn: fetchRssFeeds,
    refetchInterval: 5000,
  });
}

export function useRssActions() {
  const queryClient = useQueryClient();

  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: ['rssFeeds'] });
    void queryClient.invalidateQueries({ queryKey: ['torrents'] });
    void queryClient.invalidateQueries({ queryKey: ['dashboardMetrics'] });
  };

  const invalidateWithFollowUp = () => {
    invalidate();
    window.setTimeout(invalidate, 1500);
  };

  const addFeedMutation = useMutation({
    mutationFn: addRssFeed,
    onSuccess: invalidateWithFollowUp,
  });

  const deleteFeedMutation = useMutation({
    mutationFn: deleteRssFeed,
    onSuccess: invalidate,
  });

  const pollFeedMutation = useMutation({
    mutationFn: pollRssFeed,
    onSuccess: invalidate,
  });

  const addFilterMutation = useMutation({
    mutationFn: ({ feedId, input }: { feedId: string; input: { regexPattern: string; savePath: string } }) => addRssFilter(feedId, input),
    onSuccess: invalidateWithFollowUp,
  });

  const deleteFilterMutation = useMutation({
    mutationFn: deleteRssFilter,
    onSuccess: invalidate,
  });

  return { addFeedMutation, deleteFeedMutation, pollFeedMutation, addFilterMutation, deleteFilterMutation };
}
