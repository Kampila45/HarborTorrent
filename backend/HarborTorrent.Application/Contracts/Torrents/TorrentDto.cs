using HarborTorrent.Domain.Torrents;

namespace HarborTorrent.Application.Contracts.Torrents;

public sealed record TorrentDto(
    Guid Id,
    string InfoHash,
    string Name,
    TorrentStatus Status,
    decimal Progress,
    long DownloadedBytes,
    long UploadedBytes,
    long DownloadSpeedBytesPerSecond,
    long UploadSpeedBytesPerSecond,
    long? EtaSeconds,
    string SavePath,
    DateTimeOffset AddedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    decimal Ratio,
    string? ErrorMessage,
    TorrentSourceKind SourceKind,
    string? SourceFileName,
    string UserId);