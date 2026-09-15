namespace HarborTorrent.Application.Contracts.Torrents;

public sealed record PeerDto(
    string ConnectionUri,
    string ClientApp,
    long DownloadSpeedBytesPerSecond,
    long UploadSpeedBytesPerSecond,
    bool IsAmChoking,
    bool IsAmInterested,
    bool IsPeerChoking,
    bool IsPeerInterested);
