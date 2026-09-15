using HarborTorrent.Application.Abstractions.Persistence;
using HarborTorrent.Application.Abstractions.Realtime;
using HarborTorrent.Application.Abstractions.Runtime;
using HarborTorrent.Application.Contracts.Torrents;
using HarborTorrent.Application.Features.Torrents.Common;
using HarborTorrent.Domain.Common;
using MediatR;

namespace HarborTorrent.Application.Features.Torrents.PauseTorrent;

public sealed record PauseTorrentCommand(Guid TorrentId) : IRequest<Result<TorrentDto>>;

internal sealed class PauseTorrentCommandHandler : IRequestHandler<PauseTorrentCommand, Result<TorrentDto>>
{
    private readonly ITorrentRepository _torrentRepository;
    private readonly ITorrentRuntimeCoordinator _runtimeCoordinator;
    private readonly ITorrentEventPublisher _eventPublisher;
    private readonly TimeProvider _timeProvider;

    public PauseTorrentCommandHandler(
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

    public async Task<Result<TorrentDto>> Handle(PauseTorrentCommand request, CancellationToken cancellationToken)
    {
        var torrent = await _torrentRepository.GetByIdAsync(request.TorrentId, cancellationToken);

        if (torrent is null)
        {
            return Result<TorrentDto>.Failure(Error.NotFound($"Torrent '{request.TorrentId}' was not found."));
        }

        var pauseResult = torrent.Pause(_timeProvider.GetUtcNow());

        if (pauseResult.IsFailure)
        {
            return Result<TorrentDto>.Failure(pauseResult.Errors);
        }

        await _runtimeCoordinator.PauseAsync(torrent.Id, cancellationToken);
        await _torrentRepository.SaveChangesAsync(cancellationToken);

        var dto = torrent.ToDto();
        await _eventPublisher.PublishAsync(TorrentEventType.Paused, dto, cancellationToken);

        return Result<TorrentDto>.Success(dto);
    }
}