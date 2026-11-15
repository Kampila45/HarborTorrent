namespace HarborTorrent.Application.Abstractions.Rss;

public sealed record RssPollSummary(
    int ItemsScanned,
    int MatchedCount,
    int AddedCount,
    IReadOnlyList<string> SampleTitles,
    string? ErrorMessage = null);

public interface IRssFeedPoller
{
    void EnqueuePoll(Guid feedId);

    Task<RssPollSummary> PollFeedAsync(Guid feedId, CancellationToken cancellationToken = default);
}
