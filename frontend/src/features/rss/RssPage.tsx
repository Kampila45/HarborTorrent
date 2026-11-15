import { AlertCircle, Plus, RefreshCw, Rss, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { toast } from 'sonner';
import { PageSkeleton } from '@/components/PageSkeleton';
import { Button } from '@/components/ui/Button';
import { PageErrorState } from '@/components/ui/PageErrorState';
import { ConfirmDeleteModal } from '@/components/ConfirmDeleteModal';
import { useRssActions, useRssQueries } from '@/features/rss/hooks/useRssActions';
import { RssFeedModal } from './components/RssFeedModal';
import { RssFilterModal } from './components/RssFilterModal';

export function RssPage() {
  const { data: feeds, isLoading, isError, refetch } = useRssQueries();
  const { deleteFeedMutation, pollFeedMutation, deleteFilterMutation } = useRssActions();

  const [feedModalOpen, setFeedModalOpen] = useState(false);
  const [filterModalOpen, setFilterModalOpen] = useState<{ isOpen: boolean; feedId: string }>({ isOpen: false, feedId: '' });

  const [deleteFeedTarget, setDeleteFeedTarget] = useState<{ id: string; name: string } | null>(null);

  if (isLoading) {
    return (
      <section className="flex min-h-0 flex-1 flex-col overflow-y-auto px-6 py-6">
        <PageSkeleton />
      </section>
    );
  }

  if (isError) {
    return (
      <section className="flex min-h-0 flex-1 flex-col">
        <PageErrorState message="Failed to load RSS feeds" onRetry={() => refetch()} />
      </section>
    );
  }

  return (
    <section className="flex min-h-0 flex-1 flex-col overflow-hidden bg-white dark:bg-[#111111] px-4 sm:px-8 md:px-16 pb-10 pt-6 sm:pt-10">
      <div className="pb-6">
        <div className="flex items-end justify-between gap-4">
          <div>
            <h1 className="m-0 text-[28px] font-bold leading-[34px] tracking-[-0.01em] text-[#37352F] dark:text-[#E9E9E7]">Automation</h1>
            <p className="m-0 mt-1 text-[14px] leading-5 text-[#5F5E5B] dark:text-[#E9E9E7]">Manage RSS feeds and regex filters for automatic downloads</p>
          </div>
          <div>
            <Button onClick={() => setFeedModalOpen(true)}>
              <Plus size={14} />
              Add Feed
            </Button>
          </div>
        </div>
      </div>

      <div className="min-h-0 flex-1 overflow-y-auto">
        <div className="mb-12 flex flex-col gap-6 h-full">
          {!feeds || feeds.length === 0 ? (
            <div className="flex flex-col items-center justify-center py-24 text-center">
              <div className="mb-5 flex h-16 w-16 items-center justify-center rounded-lg border border-[#E9E9E7] dark:border-[#333333] bg-[#F7F7F5] dark:bg-[#1A1A1A]">
                <Rss className="text-3xl text-[#37352F] dark:text-[#E9E9E7] opacity-50" />
              </div>
              <h3 className="text-[18px] font-semibold text-[#37352F] dark:text-[#E9E9E7]">No RSS Feeds</h3>
              <p className="max-w-sm text-[14px] text-[#5F5E5B] dark:text-[#E9E9E7]">Add a feed to start automatically downloading torrents.</p>
            </div>
          ) : (
            feeds.map((feed) => (
              <div key={feed.id} className="rounded-lg border border-[#E9E9E7] dark:border-[#333333] bg-white dark:bg-[#1A1A1A] p-5 shadow-sm">
                <div className="flex items-start justify-between mb-4">
                  <div>
                    <h3 className="text-[16px] font-semibold text-[#37352F] dark:text-[#E9E9E7]">{feed.name}</h3>
                    <a href={feed.url} target="_blank" rel="noreferrer" className="text-[13px] text-[#5F5E5B] dark:text-[#A0A0A0] hover:underline truncate max-w-md block">
                      {feed.url}
                    </a>
                  </div>
                  <div className="flex items-center gap-2">
                    <span className="text-[12px] text-[#5F5E5B] dark:text-[#A0A0A0]">
                      {feed.lastPolledAt ? `Last polled: ${new Date(feed.lastPolledAt).toLocaleString()}` : 'Never polled'}
                    </span>
                    <Button
                      size="sm"
                      variant="secondary"
                      disabled={pollFeedMutation.isPending && pollFeedMutation.variables === feed.id}
                      onClick={async () => {
                        try {
                          const result = await pollFeedMutation.mutateAsync(feed.id);
                          const errorMsg = result.feed.lastError || result.errorMessage;
                          if (errorMsg) {
                            toast.error(`Poll failed: ${errorMsg}`);
                          } else if (result.addedCount > 0) {
                            toast.success(
                              `Polled ${feed.name}: queued ${result.addedCount} new torrent${result.addedCount === 1 ? '' : 's'}`
                            );
                          } else if (result.itemsScanned === 0) {
                            toast.info(`Polled ${feed.name}: feed has 0 items`);
                          } else if (result.matchedCount === 0) {
                            const sampleHint =
                              result.sampleTitles.length > 0 ? ` (e.g. "${result.sampleTitles[0]}")` : '';
                            toast.info(
                              `Polled ${feed.name}: ${result.itemsScanned} items scanned, 0 matched your filters${sampleHint}`,
                              { duration: 6000 }
                            );
                          } else {
                            toast.info(
                              `Polled ${feed.name}: ${result.matchedCount} matched (already added previously)`
                            );
                          }
                        } catch {
                          toast.error(`Failed to poll ${feed.name}`);
                        }
                      }}
                      title="Poll feed now"
                    >
                      <RefreshCw
                        size={13}
                        className={pollFeedMutation.isPending && pollFeedMutation.variables === feed.id ? 'animate-spin' : ''}
                      />
                      Poll Now
                    </Button>
                    <button
                      type="button"
                      onClick={() => setDeleteFeedTarget({ id: feed.id, name: feed.name })}
                      className="p-1.5 text-[#5F5E5B] dark:text-[#A0A0A0] hover:text-[#D4403A] rounded transition-colors hover:bg-[rgba(55,53,47,0.08)] dark:hover:bg-[rgba(255,255,255,0.08)]"
                      title="Delete Feed"
                    >
                      <Trash2 size={16} />
                    </button>
                  </div>
                </div>

                <div className="mt-4 border-t border-[#E9E9E7] dark:border-[#333333] pt-4">
                  {feed.lastError && (
                    <div className="mb-3 flex items-start gap-2 rounded border border-red-200 bg-red-50 px-3 py-2 text-[12px] text-red-700 dark:border-red-900/30 dark:bg-red-900/10 dark:text-red-400">
                      <AlertCircle size={14} className="mt-0.5 shrink-0" />
                      <span>
                        Last poll failed{feed.lastErrorAt ? ` (${new Date(feed.lastErrorAt).toLocaleString()})` : ''}: {feed.lastError}
                      </span>
                    </div>
                  )}
                  <div className="flex items-center justify-between mb-3">
                    <h4 className="text-[14px] font-medium text-[#37352F] dark:text-[#E9E9E7]">Regex Filters</h4>
                    <button
                      type="button"
                      onClick={() => setFilterModalOpen({ isOpen: true, feedId: feed.id })}
                      className="text-[12px] font-medium text-[#2383E2] hover:underline"
                    >
                      + Add Filter
                    </button>
                  </div>

                  {feed.filters.length === 0 ? (
                    <p className="text-[13px] text-[#5F5E5B] dark:text-[#A0A0A0] italic">No filters added. This feed will not download anything automatically.</p>
                  ) : (
                    <div className="grid gap-2">
                      {feed.filters.map(filter => (
                        <div key={filter.id} className="flex items-center justify-between bg-[#F7F7F5] dark:bg-[#222222] rounded p-3">
                          <div>
                            <code className="text-[13px] font-mono bg-[rgba(55,53,47,0.08)] dark:bg-[rgba(255,255,255,0.08)] px-1.5 py-0.5 rounded text-[#EB5757] dark:text-[#FF6B6B]">
                              {filter.regexPattern}
                            </code>
                            <p className="text-[12px] text-[#5F5E5B] dark:text-[#A0A0A0] mt-1 break-all">
                              Saves to: {filter.savePath}
                            </p>
                          </div>
                          <button
                            type="button"
                            onClick={() => deleteFilterMutation.mutate(filter.id)}
                            className="p-1.5 text-[#5F5E5B] dark:text-[#A0A0A0] hover:text-[#D4403A] rounded transition-colors"
                            title="Remove Filter"
                          >
                            <Trash2 size={14} />
                          </button>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              </div>
            ))
          )}
        </div>
      </div>

      <ConfirmDeleteModal
        isOpen={deleteFeedTarget !== null}
        torrentName={`RSS Feed "${deleteFeedTarget?.name}"`}
        onConfirm={() => {
          if (deleteFeedTarget) deleteFeedMutation.mutate(deleteFeedTarget.id);
          setDeleteFeedTarget(null);
        }}
        onCancel={() => setDeleteFeedTarget(null)}
      />

      <RssFeedModal isOpen={feedModalOpen} onClose={() => setFeedModalOpen(false)} />
      <RssFilterModal isOpen={filterModalOpen.isOpen} feedId={filterModalOpen.feedId} onClose={() => setFilterModalOpen({ isOpen: false, feedId: '' })} />
    </section>
  );
}
