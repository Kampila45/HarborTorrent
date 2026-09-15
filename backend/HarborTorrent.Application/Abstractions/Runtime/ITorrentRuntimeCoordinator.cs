namespace HarborTorrent.Application.Abstractions.Runtime;

using HarborTorrent.Domain.Torrents;

public sealed record TorrentRuntimeSnapshot(
    Guid TorrentId,
    TorrentStatus Status,
    decimal Progress,
    long DownloadedBytes,
    long UploadedBytes,
    long DownloadSpeedBytesPerSecond,
    long UploadSpeedBytesPerSecond,
    decimal Ratio,
    long? EtaSeconds);

public sealed record TorrentFileSnapshot(
    int Index,
    string Path,
    long Length,
    long DownloadedBytes,
    string Priority);

/// <summary>
/// Coordinates which torrents are actively managed by the background runtime.
/// </summary>
public interface ITorrentRuntimeCoordinator
{
    Task StartAsync(Guid torrentId, CancellationToken cancellationToken);

    Task PauseAsync(Guid torrentId, CancellationToken cancellationToken);

    Task StopAsync(Guid torrentId, CancellationToken cancellationToken);

    Task RemoveAsync(Guid torrentId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<TorrentRuntimeSnapshot>> GetSnapshotsAsync(
        IReadOnlyCollection<Guid> torrentIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<TorrentFileSnapshot>> GetFilesAsync(Guid torrentId, CancellationToken cancellationToken);

    Task UpdateFilesPriorityAsync(Guid torrentId, IReadOnlyCollection<HarborTorrent.Application.Contracts.Torrents.FilePriorityUpdateDto> updates, CancellationToken cancellationToken);
    
    Task<Stream?> CreateStreamAsync(Guid torrentId, int fileIndex, CancellationToken cancellationToken);
    
    Task UpdateEngineSettingsAsync(HarborTorrent.Domain.Settings.GlobalSettings settings, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PeerSnapshot>> GetPeersAsync(Guid torrentId, CancellationToken cancellationToken);
    
    Task<IReadOnlyCollection<TrackerSnapshot>> GetTrackersAsync(Guid torrentId, CancellationToken cancellationToken);
    
    Task AddTrackerAsync(Guid torrentId, string trackerUrl, CancellationToken cancellationToken);
}

public sealed record PeerSnapshot(
    string ConnectionUri,
    string ClientApp,
    long DownloadSpeedBytesPerSecond,
    long UploadSpeedBytesPerSecond,
    bool IsAmChoking,
    bool IsAmInterested,
    bool IsPeerChoking,
    bool IsPeerInterested);

public sealed record TrackerSnapshot(
    string Uri,
    string Status,
    int Seeders,
    int Leechers,
    string? WarningMessage,
    string? FailureMessage,
    DateTime? LastAnnounceTime);