using HarborTorrent.Application.Abstractions.Persistence;
using HarborTorrent.Application.Abstractions.Realtime;
using HarborTorrent.Application.Abstractions.Runtime;
using HarborTorrent.Application.Contracts.Torrents;
using HarborTorrent.Application.Features.Torrents.Common;
using HarborTorrent.Domain.Common;
using MediatR;

namespace HarborTorrent.Application.Features.Torrents.StopTorrent;

public sealed record StopTorrentCommand(Guid TorrentId) : IRequest<Result<TorrentDto>>;

internal sealed class StopTorrentCommandHandler : IRequestHandler<StopTorrentCommand, Result<TorrentDto>>
{
    private readonly ITorrentRepository _torrentRepository;
    private readonly ITorrentRuntimeCoordinator _runtimeCoordinator;
    private readonly ITorrentEventPublisher _eventPublisher;
    private readonly TimeProvider _timeProvider;

    public StopTorrentCommandHandler(
        ITorrentRepository torrentRepository,
        ITorrentRuntimeCoordinator runtimeCoordinator,
        ITorrentEventPublisher eventPublisher,
        TimeProvider timeProvider)
    {
        _torrentRepository = torrentRepository;
        _runtimeCoordinator = runtimeCoordinator;
        _eventPublisher = eventPublisher;
        _timeProvider = timeProvider;
    }

    public async Task<Result<TorrentDto>> Handle(StopTorrentCommand request, CancellationToken cancellationToken)
    {
        var torrent = await _torrentRepository.GetByIdAsync(request.TorrentId, cancellationToken);

        if (torrent is null)
        {
            return Result<TorrentDto>.Failure(Error.NotFound($"Torrent '{request.TorrentId}' was not found."));
        }

        var stopResult = torrent.Stop(_timeProvider.GetUtcNow());

        if (stopResult.IsFailure)
        {
            return Result<TorrentDto>.Failure(stopResult.Errors);
        }

        await _runtimeCoordinator.StopAsync(torrent.Id, cancellationToken);
        await _torrentRepository.SaveChangesAsync(cancellationToken);

        var dto = torrent.ToDto();
        await _eventPublisher.PublishAsync(TorrentEventType.Stopped, dto, cancellationToken);

        return Result<TorrentDto>.Success(dto);
    }
}