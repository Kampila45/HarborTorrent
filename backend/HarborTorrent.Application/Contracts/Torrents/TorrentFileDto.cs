namespace HarborTorrent.Application.Contracts.Torrents;

public sealed record TorrentFileDto(
    int Index,
    string Path,
    long Length,
    long DownloadedBytes,
    string Priority
);
