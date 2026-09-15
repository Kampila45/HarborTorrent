namespace HarborTorrent.Domain.Torrents;

/// <summary>
/// Identifies the source format used to add a torrent to Harbor.
/// </summary>
public enum TorrentSourceKind
{
    MagnetLink = 1,
    TorrentFile = 2
}