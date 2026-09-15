using HarborTorrent.Application.Contracts.Torrents;

namespace HarborTorrent.Application.Abstractions.Realtime;

/// <summary>
/// Publishes torrent lifecycle events to real-time consumers.
/// </summary>
public interface ITorrentEventPublisher
{
    Task PublishAsync(TorrentEventType eventType, TorrentDto torrent, CancellationToken cancellationToken);

    Task PublishRemovedAsync(Guid torrentId, string userId, CancellationToken cancellationToken);
}