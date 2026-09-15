using HarborTorrent.Application.Abstractions.Runtime;
using HarborTorrent.Application.Contracts.Torrents;
using MediatR;
using HarborTorrent.Domain.Common;

namespace HarborTorrent.Application.Features.Torrents.UpdateFilesPriority;

public sealed record UpdateTorrentFilesPriorityCommand(Guid TorrentId, IReadOnlyCollection<FilePriorityUpdateDto> Updates) : IRequest<Result>;

internal sealed class UpdateTorrentFilesPriorityCommandHandler : IRequestHandler<UpdateTorrentFilesPriorityCommand, Result>
{
    private readonly ITorrentRuntimeCoordinator _coordinator;

    public UpdateTorrentFilesPriorityCommandHandler(ITorrentRuntimeCoordinator coordinator)
    {
        _coordinator = coordinator;
    }

    public async Task<Result> Handle(UpdateTorrentFilesPriorityCommand request, CancellationToken cancellationToken)
    {
        await _coordinator.UpdateFilesPriorityAsync(request.TorrentId, request.Updates, cancellationToken);
        return Result.Success();
    }
}
