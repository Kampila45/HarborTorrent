using HarborTorrent.Application.Abstractions.Runtime;
using HarborTorrent.Application.Contracts.Torrents;
using HarborTorrent.Domain.Common;
using MediatR;

namespace HarborTorrent.Application.Features.Torrents.GetTorrentTrackers;

public sealed record GetTorrentTrackersQuery(Guid TorrentId) : IRequest<Result<IReadOnlyCollection<TrackerDto>>>;

internal sealed class GetTorrentTrackersQueryHandler : IRequestHandler<GetTorrentTrackersQuery, Result<IReadOnlyCollection<TrackerDto>>>
{
    private readonly ITorrentRuntimeCoordinator _runtimeCoordinator;

    public GetTorrentTrackersQueryHandler(ITorrentRuntimeCoordinator runtimeCoordinator)
    {
        _runtimeCoordinator = runtimeCoordinator;
    }

    public async Task<Result<IReadOnlyCollection<TrackerDto>>> Handle(GetTorrentTrackersQuery request, CancellationToken cancellationToken)
    {
        var snapshots = await _runtimeCoordinator.GetTrackersAsync(request.TorrentId, cancellationToken);
        var dtos = snapshots.Select(s => new TrackerDto(
            s.Uri,
            s.Status,
            s.Seeders,
            s.Leechers,
            s.WarningMessage,
            s.FailureMessage,
            s.LastAnnounceTime
        )).ToArray();

        return Result<IReadOnlyCollection<TrackerDto>>.Success(dtos);
    }
}
