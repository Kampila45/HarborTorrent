namespace HarborTorrent.Application.Contracts.Torrents;

public enum TorrentEventType
{
    Added = 1,
    Started = 2,
    Paused = 3,
    Stopped = 4,
    ProgressUpdated = 5,
    Completed = 6
}