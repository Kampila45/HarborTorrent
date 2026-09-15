using HarborTorrent.Application.Contracts.Torrents;
using HarborTorrent.Domain.Torrents;

namespace HarborTorrent.Application.Features.Torrents.Common;

public static class TorrentMappings
{
    public static TorrentDto ToDto(this Torrent torrent) => new(
        torrent.Id,
    torrent.InfoHash,
        torrent.Name,
        torrent.Status,
        torrent.Progress,
    torrent.DownloadedBytes,
    torrent.UploadedBytes,
        torrent.DownloadSpeedBytesPerSecond,
        torrent.UploadSpeedBytesPerSecond,
        torrent.EtaSeconds,
        torrent.SavePath,
        torrent.AddedAtUtc,
    torrent.StartedAtUtc,
        torrent.CompletedAtUtc,
        torrent.Ratio,
    torrent.ErrorMessage,
        torrent.Source.Kind,
        torrent.Source.FileName,
        torrent.UserId);
}