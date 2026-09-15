namespace HarborTorrent.Application.Features.Rss.Shared;

public record RssFilterDto(Guid Id, string RegexPattern, string SavePath);
