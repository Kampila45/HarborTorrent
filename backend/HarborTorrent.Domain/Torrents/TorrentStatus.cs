namespace HarborTorrent.Domain.Torrents;

/// <summary>
/// Represents the lifecycle state of a managed torrent.
/// </summary>
public enum TorrentStatus
{
    Queued = 1,
    Downloading = 2,
    Paused = 3,
    Stopped = 4,
    Completed = 5,
    Seeding = 6
}