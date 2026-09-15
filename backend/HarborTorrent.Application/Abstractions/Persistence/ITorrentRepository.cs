using HarborTorrent.Domain.Torrents;

namespace HarborTorrent.Application.Abstractions.Persistence;

/// <summary>
/// Provides persistence operations for Harbor torrent aggregates.
/// </summary>
public interface ITorrentRepository
{
    Task AddAsync(Torrent torrent, CancellationToken cancellationToken);

    Task<Torrent?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> ExistsByInfoHashAsync(string infoHash, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Torrent>> ListAsync(CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Torrent>> ListAllUnfilteredAsync(CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Torrent>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    Task<(IReadOnlyCollection<Torrent> Items, int TotalCount)> ListPaginatedAsync(
        string? status,
        string? query,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task DeleteAsync(Torrent torrent, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}