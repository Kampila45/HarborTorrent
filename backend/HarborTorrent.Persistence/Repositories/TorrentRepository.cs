using HarborTorrent.Application.Abstractions.Persistence;
using HarborTorrent.Domain.Torrents;
using Microsoft.EntityFrameworkCore;

namespace HarborTorrent.Persistence.Repositories;

internal sealed class TorrentRepository : ITorrentRepository
{
    private readonly HarborDbContext _dbContext;

    public TorrentRepository(HarborDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddAsync(Torrent torrent, CancellationToken cancellationToken)
    {
        return _dbContext.Torrents.AddAsync(torrent, cancellationToken).AsTask();
    }

    public Task<Torrent?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _dbContext.Torrents.SingleOrDefaultAsync(torrent => torrent.Id == id, cancellationToken);
    }

    public Task<bool> ExistsByInfoHashAsync(string infoHash, CancellationToken cancellationToken)
    {
        return _dbContext.Torrents.AnyAsync(torrent => torrent.InfoHash == infoHash, cancellationToken);
    }

    public async Task<IReadOnlyCollection<Torrent>> ListAsync(CancellationToken cancellationToken)
    {
        var items = await _dbContext.Torrents.ToListAsync(cancellationToken);
        
        return items
            .OrderByDescending(torrent => torrent.AddedAtUtc)
            .ToList();
    }

    public async Task<IReadOnlyCollection<Torrent>> ListAllUnfilteredAsync(CancellationToken cancellationToken)
    {
        var items = await _dbContext.Torrents
            .IgnoreQueryFilters()
            .ToListAsync(cancellationToken);
            
        return items
            .OrderByDescending(torrent => torrent.AddedAtUtc)
            .ToList();
    }

    public async Task<(IReadOnlyCollection<Torrent> Items, int TotalCount)> ListPaginatedAsync(
        string? status, 
        string? query, 
        int pageNumber, 
        int pageSize, 
        CancellationToken cancellationToken)
    {
        var q = _dbContext.Torrents.AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase))
            {
                q = q.Where(t => t.Progress >= 100m);
            }
            else if (Enum.TryParse<TorrentStatus>(status, true, out var parsedStatus))
            {
                q = q.Where(t => t.Status == parsedStatus);
            }
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            q = q.Where(t => EF.Functions.Like(t.Name, $"%{query}%"));
        }

        // SQLite EF Core 8+ does not support OrderBy on DateTimeOffset. 
        // For a local desktop app, it's highly performant to pull the filtered records into memory.
        var allFiltered = await q.ToListAsync(cancellationToken);
        
        var items = allFiltered
            .OrderByDescending(t => t.AddedAtUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return (items, allFiltered.Count);
    }

    public async Task<IReadOnlyCollection<Torrent>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return Array.Empty<Torrent>();
        }

        return await _dbContext.Torrents
            .Where(torrent => ids.Contains(torrent.Id))
            .ToListAsync(cancellationToken);
    }

    public Task DeleteAsync(Torrent torrent, CancellationToken cancellationToken)
    {
        _dbContext.Torrents.Remove(torrent);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}