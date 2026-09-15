using FluentValidation;
using HarborTorrent.Application.Abstractions.Persistence;
using HarborTorrent.Application.Features.Rss.Shared;
using HarborTorrent.Domain.Rss;
using MediatR;

namespace HarborTorrent.Application.Features.Rss.AddRssFilter;

public record AddRssFilterCommand(Guid RssFeedId, string RegexPattern, string SavePath) : IRequest<RssFilterDto>;

public class AddRssFilterCommandValidator : AbstractValidator<AddRssFilterCommand>
{
    public AddRssFilterCommandValidator()
    {
        RuleFor(x => x.RssFeedId).NotEmpty();
        RuleFor(x => x.RegexPattern).NotEmpty().MaximumLength(512);
        RuleFor(x => x.SavePath).NotEmpty().MaximumLength(512);
    }
}

public class AddRssFilterCommandHandler : IRequestHandler<AddRssFilterCommand, RssFilterDto>
{
    private readonly IRssFeedRepository _repository;

    public AddRssFilterCommandHandler(IRssFeedRepository repository)
    {
        _repository = repository;
    }

    public async Task<RssFilterDto> Handle(AddRssFilterCommand request, CancellationToken cancellationToken)
    {
        var filter = new RssFilter(Guid.NewGuid(), request.RssFeedId, request.RegexPattern, request.SavePath);
        await _repository.AddFilterAsync(filter, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return new RssFilterDto(filter.Id, filter.RegexPattern, filter.SavePath);
    }
}
