using HarborTorrent.Application.Abstractions.Persistence;
using HarborTorrent.Application.Abstractions.Rss;
using HarborTorrent.Application.Features.Rss.Shared;
using MediatR;

namespace HarborTorrent.Application.Features.Rss.GetRssFeeds;

public record GetRssFeedsQuery : IRequest<IReadOnlyCollection<RssFeedDto>>;

public class GetRssFeedsQueryHandler : IRequestHandler<GetRssFeedsQuery, IReadOnlyCollection<RssFeedDto>>
{
    private readonly IRssFeedRepository _repository;
    private readonly IRssPollStatusTracker? _pollStatusTracker;

    public GetRssFeedsQueryHandler(IRssFeedRepository repository, IRssPollStatusTracker? pollStatusTracker = null)
    {
        _repository = repository;
        _pollStatusTracker = pollStatusTracker;
    }

    public async Task<IReadOnlyCollection<RssFeedDto>> Handle(GetRssFeedsQuery request, CancellationToken cancellationToken)
    {
        var feeds = await _repository.GetAllAsync(cancellationToken);

        return feeds.Select(f =>
        {
            var status = _pollStatusTracker?.GetStatus(f.Id);
            return new RssFeedDto(
                f.Id,
                f.Name,
                f.Url,
                f.LastPolledAt,
                f.IsActive,
                f.Filters.Select(filter => new RssFilterDto(filter.Id, filter.RegexPattern, filter.SavePath)).ToList(),
                status?.LastError,
                status?.LastErrorAt);
        }).ToList();
    }
}
