import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { addRssFeed, addRssFilter, deleteRssFeed, deleteRssFilter, fetchRssFeeds } from '@/services/rss.api';

export function useRssQueries() {
  return useQuery({
    queryKey: ['rssFeeds'],
    queryFn: fetchRssFeeds,
    refetchInterval: 5000,
  });
}

export function useRssActions() {
  const queryClient = useQueryClient();

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['rssFeeds'] });

  const addFeedMutation = useMutation({
    mutationFn: addRssFeed,
    onSuccess: invalidate,
  });

  const deleteFeedMutation = useMutation({
    mutationFn: deleteRssFeed,
    onSuccess: invalidate,
  });

  const addFilterMutation = useMutation({
    mutationFn: ({ feedId, input }: { feedId: string; input: { regexPattern: string; savePath: string } }) => addRssFilter(feedId, input),
    onSuccess: invalidate,
  });

  const deleteFilterMutation = useMutation({
    mutationFn: deleteRssFilter,
    onSuccess: invalidate,
  });

  return { addFeedMutation, deleteFeedMutation, addFilterMutation, deleteFilterMutation };
}
