using HarborTorrent.Application.Abstractions.Search;
using HarborTorrent.Application.Contracts.Search;
using MediatR;

namespace HarborTorrent.Application.Features.Search.SearchTorrents;

public record SearchTorrentsQuery(string Query) : IRequest<IEnumerable<SearchResultDto>>;

internal sealed class SearchTorrentsQueryHandler : IRequestHandler<SearchTorrentsQuery, IEnumerable<SearchResultDto>>
{
    private readonly IEnumerable<ITorrentSearchProvider> _searchProviders;

    public SearchTorrentsQueryHandler(IEnumerable<ITorrentSearchProvider> searchProviders)
    {
        _searchProviders = searchProviders;
    }

    public async Task<IEnumerable<SearchResultDto>> Handle(SearchTorrentsQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return Enumerable.Empty<SearchResultDto>();
        }

        var searchTasks = _searchProviders.Select(p => p.SearchAsync(request.Query, cancellationToken));
        
        // Execute all registered search providers concurrently
        var results = await Task.WhenAll(searchTasks);
        
        return results
            .SelectMany(r => r)
            .OrderByDescending(r => r.Seeders)
            .ToList();
    }
}
