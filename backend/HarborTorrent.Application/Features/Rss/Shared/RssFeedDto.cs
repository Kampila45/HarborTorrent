namespace HarborTorrent.Application.Features.Rss.Shared;

public record RssFeedDto(Guid Id, string Name, string Url, DateTime? LastPolledAt, bool IsActive, ICollection<RssFilterDto> Filters);
