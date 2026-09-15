using HarborTorrent.Domain.Rss;

namespace HarborTorrent.Application.Abstractions.Persistence;

public interface IRssFeedRepository
{
    Task<IReadOnlyCollection<RssFeed>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<RssFeed?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(RssFeed feed, CancellationToken cancellationToken = default);
    void Remove(RssFeed feed);

    Task<RssFilter?> GetFilterByIdAsync(Guid filterId, CancellationToken cancellationToken = default);
    Task AddFilterAsync(RssFilter filter, CancellationToken cancellationToken = default);
    void RemoveFilter(RssFilter filter);

    void UpdateFeed(RssFeed feed);

    Task<bool> HasItemHistoryAsync(Guid feedId, string itemIdentifier, CancellationToken cancellationToken = default);
    Task AddItemHistoryAsync(RssFeedItemHistory history, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
