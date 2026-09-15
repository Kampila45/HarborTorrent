using HarborTorrent.Application.Abstractions.Runtime;
using HarborTorrent.Application.Contracts.Torrents;
using HarborTorrent.Domain.Common;
using MediatR;

namespace HarborTorrent.Application.Features.Torrents.GetTorrentPeers;

public sealed record GetTorrentPeersQuery(Guid TorrentId) : IRequest<Result<IReadOnlyCollection<PeerDto>>>;

internal sealed class GetTorrentPeersQueryHandler : IRequestHandler<GetTorrentPeersQuery, Result<IReadOnlyCollection<PeerDto>>>
{
    private readonly ITorrentRuntimeCoordinator _runtimeCoordinator;

    public GetTorrentPeersQueryHandler(ITorrentRuntimeCoordinator runtimeCoordinator)
    {
        _runtimeCoordinator = runtimeCoordinator;
    }

    public async Task<Result<IReadOnlyCollection<PeerDto>>> Handle(GetTorrentPeersQuery request, CancellationToken cancellationToken)
    {
        var snapshots = await _runtimeCoordinator.GetPeersAsync(request.TorrentId, cancellationToken);
        var dtos = snapshots.Select(s => new PeerDto(
            s.ConnectionUri,
            s.ClientApp,
            s.DownloadSpeedBytesPerSecond,
            s.UploadSpeedBytesPerSecond,
            s.IsAmChoking,
            s.IsAmInterested,
            s.IsPeerChoking,
            s.IsPeerInterested
        )).ToArray();

        return Result<IReadOnlyCollection<PeerDto>>.Success(dtos);
    }
}
