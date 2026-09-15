using HarborTorrent.Application.Abstractions.Persistence;
using HarborTorrent.Application.Abstractions.Realtime;
using HarborTorrent.Application.Abstractions.Runtime;
using HarborTorrent.Domain.Common;
using MediatR;

namespace HarborTorrent.Application.Features.Torrents.RemoveTorrent;

public sealed record RemoveTorrentCommand(Guid TorrentId) : IRequest<Result>;

internal sealed class RemoveTorrentCommandHandler : IRequestHandler<RemoveTorrentCommand, Result>
{
    private readonly ITorrentRepository _torrentRepository;
    private readonly ITorrentRuntimeCoordinator _runtimeCoordinator;
    private readonly ITorrentEventPublisher _eventPublisher;

    public RemoveTorrentCommandHandler(
        ITorrentRepository torrentRepository,
        ITorrentRuntimeCoordinator runtimeCoordinator,
        ITorrentEventPublisher eventPublisher)
    {
        _torrentRepository = torrentRepository;
        _runtimeCoordinator = runtimeCoordinator;
        _eventPublisher = eventPublisher;
    }

    public async Task<Result> Handle(RemoveTorrentCommand request, CancellationToken cancellationToken)
    {
        var torrent = await _torrentRepository.GetByIdAsync(request.TorrentId, cancellationToken);

        if (torrent is null)
        {
            return Result.Failure(Error.NotFound($"Torrent '{request.TorrentId}' was not found."));
        }

        await _runtimeCoordinator.RemoveAsync(torrent.Id, cancellationToken);
        await _torrentRepository.DeleteAsync(torrent, cancellationToken);
        await _torrentRepository.SaveChangesAsync(cancellationToken);
        await _eventPublisher.PublishRemovedAsync(torrent.Id, torrent.UserId, cancellationToken);

        return Result.Success();
    }
}