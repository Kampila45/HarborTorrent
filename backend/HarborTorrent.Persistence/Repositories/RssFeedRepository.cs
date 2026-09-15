using HarborTorrent.Application.Abstractions.Persistence;
using HarborTorrent.Domain.Rss;
using Microsoft.EntityFrameworkCore;

namespace HarborTorrent.Persistence.Repositories;

internal sealed class RssFeedRepository : IRssFeedRepository
{
    private readonly HarborDbContext _dbContext;

    public RssFeedRepository(HarborDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<RssFeed>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.RssFeeds
            .Include(f => f.Filters)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<RssFeed?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.RssFeeds.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task AddAsync(RssFeed feed, CancellationToken cancellationToken = default)
    {
        await _dbContext.RssFeeds.AddAsync(feed, cancellationToken);
    }

    public void Remove(RssFeed feed)
    {
        _dbContext.RssFeeds.Remove(feed);
    }

    public async Task<RssFilter?> GetFilterByIdAsync(Guid filterId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.RssFilters.FindAsync(new object[] { filterId }, cancellationToken);
    }

    public async Task AddFilterAsync(RssFilter filter, CancellationToken cancellationToken = default)
    {
        await _dbContext.RssFilters.AddAsync(filter, cancellationToken);
    }

    public void RemoveFilter(RssFilter filter)
    {
        _dbContext.RssFilters.Remove(filter);
    }

    public void UpdateFeed(RssFeed feed)
    {
        _dbContext.RssFeeds.Update(feed);
    }

    public async Task<bool> HasItemHistoryAsync(Guid feedId, string itemIdentifier, CancellationToken cancellationToken = default)
    {
        return await _dbContext.RssFeedItemHistories
            .AnyAsync(h => h.RssFeedId == feedId && h.ItemIdentifier == itemIdentifier, cancellationToken);
    }

    public async Task AddItemHistoryAsync(RssFeedItemHistory history, CancellationToken cancellationToken = default)
    {
        await _dbContext.RssFeedItemHistories.AddAsync(history, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
