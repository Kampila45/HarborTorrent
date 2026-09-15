using HarborTorrent.Application.Abstractions.Persistence;
using HarborTorrent.Application.Abstractions.Realtime;
using HarborTorrent.Application.Abstractions.Runtime;
using HarborTorrent.Application.Contracts.Torrents;
using HarborTorrent.Application.Features.Torrents.Common;
using HarborTorrent.Domain.Common;
using MediatR;

namespace HarborTorrent.Application.Features.Torrents.StartTorrent;

public sealed record StartTorrentCommand(Guid TorrentId) : IRequest<Result<TorrentDto>>;

internal sealed class StartTorrentCommandHandler : IRequestHandler<StartTorrentCommand, Result<TorrentDto>>
{
    private readonly ITorrentRepository _torrentRepository;
    private readonly ITorrentRuntimeCoordinator _runtimeCoordinator;
    private readonly ITorrentEventPublisher _eventPublisher;
    private readonly TimeProvider _timeProvider;

    public StartTorrentCommandHandler(
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

    public async Task<Result<TorrentDto>> Handle(StartTorrentCommand request, CancellationToken cancellationToken)
    {
        var torrent = await _torrentRepository.GetByIdAsync(request.TorrentId, cancellationToken);

        if (torrent is null)
        {
            return Result<TorrentDto>.Failure(Error.NotFound($"Torrent '{request.TorrentId}' was not found."));
        }

        var startResult = torrent.Start(_timeProvider.GetUtcNow());

        if (startResult.IsFailure)
        {
            return Result<TorrentDto>.Failure(startResult.Errors);
        }

        await _runtimeCoordinator.StartAsync(torrent.Id, cancellationToken);
        await _torrentRepository.SaveChangesAsync(cancellationToken);

        var dto = torrent.ToDto();
        await _eventPublisher.PublishAsync(TorrentEventType.Started, dto, cancellationToken);

        return Result<TorrentDto>.Success(dto);
    }
}