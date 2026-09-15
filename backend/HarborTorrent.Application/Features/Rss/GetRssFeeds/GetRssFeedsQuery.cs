using HarborTorrent.Application.Abstractions.Persistence;
using HarborTorrent.Application.Features.Rss.Shared;
using MediatR;

namespace HarborTorrent.Application.Features.Rss.GetRssFeeds;

public record GetRssFeedsQuery : IRequest<IReadOnlyCollection<RssFeedDto>>;

public class GetRssFeedsQueryHandler : IRequestHandler<GetRssFeedsQuery, IReadOnlyCollection<RssFeedDto>>
{
    private readonly IRssFeedRepository _repository;

    public GetRssFeedsQueryHandler(IRssFeedRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyCollection<RssFeedDto>> Handle(GetRssFeedsQuery request, CancellationToken cancellationToken)
    {
        var feeds = await _repository.GetAllAsync(cancellationToken);

        return feeds.Select(f => new RssFeedDto(
            f.Id,
            f.Name,
            f.Url,
            f.LastPolledAt,
            f.IsActive,
            f.Filters.Select(filter => new RssFilterDto(filter.Id, filter.RegexPattern, filter.SavePath)).ToList()
        )).ToList();
    }
}
