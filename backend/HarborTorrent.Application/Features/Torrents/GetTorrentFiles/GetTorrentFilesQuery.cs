using HarborTorrent.Application.Abstractions.Runtime;
using HarborTorrent.Application.Contracts.Torrents;
using MediatR;

namespace HarborTorrent.Application.Features.Torrents.GetTorrentFiles;

public sealed record GetTorrentFilesQuery(Guid TorrentId) : IRequest<IReadOnlyCollection<TorrentFileDto>>;

internal sealed class GetTorrentFilesQueryHandler : IRequestHandler<GetTorrentFilesQuery, IReadOnlyCollection<TorrentFileDto>>
{
    private readonly ITorrentRuntimeCoordinator _coordinator;

    public GetTorrentFilesQueryHandler(ITorrentRuntimeCoordinator coordinator)
    {
        _coordinator = coordinator;
    }

    public async Task<IReadOnlyCollection<TorrentFileDto>> Handle(GetTorrentFilesQuery request, CancellationToken cancellationToken)
    {
        var files = await _coordinator.GetFilesAsync(request.TorrentId, cancellationToken);

        return files.Select(f => new TorrentFileDto(
            f.Index,
            f.Path,
            f.Length,
            f.DownloadedBytes,
            f.Priority
        )).ToArray();
    }
}
