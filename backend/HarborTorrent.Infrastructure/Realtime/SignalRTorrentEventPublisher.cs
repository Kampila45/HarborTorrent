using HarborTorrent.Application.Abstractions.Realtime;
using HarborTorrent.Application.Contracts.Torrents;
using Microsoft.AspNetCore.SignalR;

namespace HarborTorrent.Infrastructure.Realtime;

internal sealed class SignalRTorrentEventPublisher : ITorrentEventPublisher
{
    private readonly IHubContext<TorrentHub> _hubContext;

    public SignalRTorrentEventPublisher(IHubContext<TorrentHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task PublishAsync(TorrentEventType eventType, TorrentDto torrent, CancellationToken cancellationToken)
    {
        return _hubContext.Clients.All.SendAsync(GetEventName(eventType), torrent, cancellationToken);
    }

    public Task PublishRemovedAsync(Guid torrentId, string userId, CancellationToken cancellationToken)
    {
        return _hubContext.Clients.All.SendAsync("TorrentRemoved", new { TorrentId = torrentId }, cancellationToken);
    }

    private static string GetEventName(TorrentEventType eventType) => eventType switch
    {
        TorrentEventType.Added => "TorrentAdded",
        TorrentEventType.Started => "TorrentStarted",
        TorrentEventType.Paused => "TorrentPaused",
        TorrentEventType.Stopped => "TorrentStopped",
        TorrentEventType.ProgressUpdated => "TorrentProgressUpdated",
        TorrentEventType.Completed => "TorrentCompleted",
        _ => "TorrentUpdated"
    };
}