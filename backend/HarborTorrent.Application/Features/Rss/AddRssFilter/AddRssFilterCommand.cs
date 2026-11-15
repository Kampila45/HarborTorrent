using System.Text.RegularExpressions;
using FluentValidation;
using HarborTorrent.Application.Abstractions.Persistence;
using HarborTorrent.Application.Abstractions.Rss;
using HarborTorrent.Application.Features.Rss.Shared;
using HarborTorrent.Domain.Rss;
using MediatR;

namespace HarborTorrent.Application.Features.Rss.AddRssFilter;

public record AddRssFilterCommand(Guid RssFeedId, string RegexPattern, string? SavePath = null) : IRequest<RssFilterDto>;

public class AddRssFilterCommandValidator : AbstractValidator<AddRssFilterCommand>
{
    public AddRssFilterCommandValidator()
    {
        RuleFor(x => x.RssFeedId).NotEmpty();
        RuleFor(x => x.RegexPattern)
            .NotEmpty()
            .MaximumLength(512)
            .Must(BeValidRegex)
            .WithMessage("Regex pattern is not valid.");
        RuleFor(x => x.SavePath).MaximumLength(512);
    }

    private static bool BeValidRegex(string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return false;
        }

        try
        {
            _ = new Regex(pattern, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}

public class AddRssFilterCommandHandler : IRequestHandler<AddRssFilterCommand, RssFilterDto>
{
    private readonly IRssFeedRepository _repository;
    private readonly ISettingsRepository? _settingsRepository;
    private readonly IRssFeedPoller? _poller;

    public AddRssFilterCommandHandler(
        IRssFeedRepository repository,
        ISettingsRepository? settingsRepository = null,
        IRssFeedPoller? poller = null)
    {
        _repository = repository;
        _settingsRepository = settingsRepository;
        _poller = poller;
    }

    public async Task<RssFilterDto> Handle(AddRssFilterCommand request, CancellationToken cancellationToken)
    {
        var savePath = request.SavePath?.Trim();
        if (string.IsNullOrWhiteSpace(savePath) && _settingsRepository is not null)
        {
            var settings = await _settingsRepository.GetAsync(cancellationToken);
            savePath = settings.DefaultDownloadPath;
        }

        if (string.IsNullOrWhiteSpace(savePath))
        {
            savePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads",
                "HarborTorrent");
        }

        var filter = new RssFilter(Guid.NewGuid(), request.RssFeedId, request.RegexPattern, savePath);
        await _repository.AddFilterAsync(filter, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        _poller?.EnqueuePoll(request.RssFeedId);

        return new RssFilterDto(filter.Id, filter.RegexPattern, filter.SavePath);
    }
}
