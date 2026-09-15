using HarborTorrent.Application.Abstractions.Persistence;
using HarborTorrent.Application.Features.Torrents.AddTorrent;
using HarborTorrent.Domain.Rss;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.ServiceModel.Syndication;
using System.Text.RegularExpressions;
using System.Xml;

namespace HarborTorrent.Infrastructure.BackgroundJobs;

internal sealed class RssPollingHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<RssPollingHostedService> _logger;
    private readonly HttpClient _httpClient;

    public RssPollingHostedService(IServiceScopeFactory serviceScopeFactory, ILogger<RssPollingHostedService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _httpClient = new HttpClient();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Execute immediately on startup
        try
        {
            await PollRssFeedsAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred during initial RSS polling.");
        }

        // Then poll every 15 minutes
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));
        
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await PollRssFeedsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while polling RSS feeds.");
            }
        }
    }

    private async Task PollRssFeedsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRssFeedRepository>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var allFeeds = await repository.GetAllAsync(cancellationToken);
        var activeFeeds = allFeeds.Where(f => f.IsActive);

        foreach (var feed in activeFeeds)
        {
            if (feed.Filters.Count == 0)
                continue;

            try
            {
                await ProcessFeedAsync(feed, repository, mediator, cancellationToken);
                
                feed.MarkAsPolled();
                repository.UpdateFeed(feed);
                await repository.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to process RSS feed: {FeedName} ({FeedUrl})", feed.Name, feed.Url);
            }
        }
    }

    private async Task ProcessFeedAsync(RssFeed feed, IRssFeedRepository repository, IMediator mediator, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(feed.Url, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var xmlReader = XmlReader.Create(stream);
        var syndicationFeed = SyndicationFeed.Load(xmlReader);

        if (syndicationFeed == null) return;

        foreach (var item in syndicationFeed.Items)
        {
            var title = item.Title?.Text;
            var link = item.Links?.FirstOrDefault()?.Uri?.ToString() ?? item.Id;

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(link))
                continue;

            var isMagnet = link.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase);
            var identifier = isMagnet ? link : title; // A unique identifier

            foreach (var filter in feed.Filters)
            {
                if (Regex.IsMatch(title, filter.RegexPattern, RegexOptions.IgnoreCase))
                {
                    var alreadyProcessed = await repository.HasItemHistoryAsync(feed.Id, identifier, cancellationToken);

                    if (!alreadyProcessed)
                    {
                        _logger.LogInformation("RSS Match found! Feed: {Feed}, Title: {Title}", feed.Name, title);

                        var command = new AddTorrentCommand(
                            SavePath: filter.SavePath,
                            MagnetLink: isMagnet ? link : null,
                            TorrentFileName: isMagnet ? null : title + ".torrent",
                            TorrentFileContentBase64: null,
                            Name: isMagnet ? null : title
                        );

                        if (!isMagnet && Uri.TryCreate(link, UriKind.Absolute, out var torrentUri))
                        {
                            try 
                            {
                                var fileBytes = await _httpClient.GetByteArrayAsync(torrentUri, cancellationToken);
                                command = command with { TorrentFileContentBase64 = Convert.ToBase64String(fileBytes) };
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to download .torrent file from RSS link: {Link}", link);
                                continue;
                            }
                        }

                        var result = await mediator.Send(command, cancellationToken);

                        if (result.IsSuccess)
                        {
                            await repository.AddItemHistoryAsync(new RssFeedItemHistory(Guid.NewGuid(), feed.Id, filter.Id, identifier), cancellationToken);
                        }
                        else
                        {
                            _logger.LogWarning("Failed to add torrent from RSS: {Errors}", string.Join(", ", result.Errors.Select(e => e.Message)));
                        }
                    }
                }
            }
        }
    }
}
