using HarborTorrent.Application.Abstractions.Persistence;
using HarborTorrent.Application.Contracts.Torrents;
using HarborTorrent.Application.Features.Torrents.Common;
using HarborTorrent.Domain.Common;
using MediatR;

namespace HarborTorrent.Application.Features.Torrents.GetTorrent;

public sealed record GetTorrentQuery(Guid TorrentId) : IRequest<Result<TorrentDto>>;

internal sealed class GetTorrentQueryHandler : IRequestHandler<GetTorrentQuery, Result<TorrentDto>>
{
    private readonly ITorrentRepository _torrentRepository;

    public GetTorrentQueryHandler(ITorrentRepository torrentRepository)
    {
        _torrentRepository = torrentRepository;
    }

    public async Task<Result<TorrentDto>> Handle(GetTorrentQuery request, CancellationToken cancellationToken)
    {
        var torrent = await _torrentRepository.GetByIdAsync(request.TorrentId, cancellationToken);

        return torrent is null
            ? Result<TorrentDto>.Failure(Error.NotFound($"Torrent '{request.TorrentId}' was not found."))
            : Result<TorrentDto>.Success(torrent.ToDto());
    }
}