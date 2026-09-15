using HarborTorrent.Application.Abstractions.Persistence;
using HarborTorrent.Application.Contracts.Common;
using HarborTorrent.Application.Contracts.Torrents;
using HarborTorrent.Application.Features.Torrents.Common;
using MediatR;

namespace HarborTorrent.Application.Features.Torrents.ListTorrents;

public sealed record ListTorrentsQuery(int PageNumber, int PageSize, string? Status, string? Query) : IRequest<PagedResultDto<TorrentDto>>;

internal sealed class ListTorrentsQueryHandler : IRequestHandler<ListTorrentsQuery, PagedResultDto<TorrentDto>>
{
    private readonly ITorrentRepository _torrentRepository;

    public ListTorrentsQueryHandler(ITorrentRepository torrentRepository)
    {
        _torrentRepository = torrentRepository;
    }

    public async Task<PagedResultDto<TorrentDto>> Handle(ListTorrentsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _torrentRepository.ListPaginatedAsync(
            request.Status, 
            request.Query, 
            request.PageNumber, 
            request.PageSize, 
            cancellationToken);

        return new PagedResultDto<TorrentDto>
        {
            Items = items.Select(t => t.ToDto()).ToArray(),
            TotalCount = totalCount,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }
}