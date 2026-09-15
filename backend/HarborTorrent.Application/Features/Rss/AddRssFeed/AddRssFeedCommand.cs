using FluentValidation;
using HarborTorrent.Application.Abstractions.Persistence;
using HarborTorrent.Application.Features.Rss.Shared;
using HarborTorrent.Domain.Rss;
using MediatR;

namespace HarborTorrent.Application.Features.Rss.AddRssFeed;

public record AddRssFeedCommand(string Name, string Url) : IRequest<RssFeedDto>;

public class AddRssFeedCommandValidator : AbstractValidator<AddRssFeedCommand>
{
    public AddRssFeedCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Url).NotEmpty().MaximumLength(2048);
    }
}

public class AddRssFeedCommandHandler : IRequestHandler<AddRssFeedCommand, RssFeedDto>
{
    private readonly IRssFeedRepository _repository;

    public AddRssFeedCommandHandler(IRssFeedRepository repository)
    {
        _repository = repository;
    }

    public async Task<RssFeedDto> Handle(AddRssFeedCommand request, CancellationToken cancellationToken)
    {
        var feed = new RssFeed(Guid.NewGuid(), request.Name, request.Url);
        await _repository.AddAsync(feed, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return new RssFeedDto(feed.Id, feed.Name, feed.Url, feed.LastPolledAt, feed.IsActive, Array.Empty<RssFilterDto>());
    }
}
