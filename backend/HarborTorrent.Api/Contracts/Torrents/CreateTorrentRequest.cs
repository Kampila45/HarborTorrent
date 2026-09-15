namespace HarborTorrent.Api.Contracts.Torrents;

public sealed record CreateTorrentRequest(
    string? Name,
    string SavePath,
    string? MagnetLink,
    string? TorrentFileName,
    string? TorrentFileContentBase64);