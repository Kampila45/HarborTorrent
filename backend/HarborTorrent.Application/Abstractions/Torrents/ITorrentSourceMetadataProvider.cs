using HarborTorrent.Domain.Common;
using HarborTorrent.Domain.Torrents;

namespace HarborTorrent.Application.Abstractions.Torrents;

/// <summary>
/// Contains the stable identity and metadata extracted from a torrent source.
/// </summary>
/// <param name="InfoHash">The unique SHA-1 hash identifying the torrent within the BitTorrent network.</param>
public sealed record TorrentSourceMetadata(string InfoHash);

/// <summary>
/// Extracts stable torrent identity metadata from a torrent source.
/// </summary>
public interface ITorrentSourceMetadataProvider
{
    /// <summary>
    /// Synchronously parses and extracts metadata (such as the InfoHash) from the provided torrent source.
    /// </summary>
    /// <param name="source">The source of the torrent (e.g., a magnet link or physical .torrent file content).</param>
    /// <returns>A successful result containing the <see cref="TorrentSourceMetadata"/>, or a failure result if extraction fails.</returns>
    Result<TorrentSourceMetadata> GetMetadata(TorrentSource source);
}