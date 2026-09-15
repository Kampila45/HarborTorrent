using HarborTorrent.Application.Contracts.Search;

namespace HarborTorrent.Application.Abstractions.Search;

/// <summary>
/// Defines a contract for searching public torrent trackers or APIs.
/// </summary>
public interface ITorrentSearchProvider
{
    /// <summary>
    /// The display name of the search provider.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Executes a search query and returns a collection of torrent results.
    /// </summary>
    Task<IEnumerable<SearchResultDto>> SearchAsync(string query, CancellationToken cancellationToken);
}
