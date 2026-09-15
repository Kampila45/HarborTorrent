namespace HarborTorrent.Application.Contracts.Torrents;

public sealed record TrackerDto(
    string Uri,
    string Status,
    int Seeders,
    int Leechers,
    string? WarningMessage,
    string? FailureMessage,
    DateTime? LastAnnounceTime);
