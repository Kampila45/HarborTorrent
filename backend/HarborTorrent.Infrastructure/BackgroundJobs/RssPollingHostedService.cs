using HarborTorrent.Application.Abstractions.Persistence;
using HarborTorrent.Application.Abstractions.Rss;
using HarborTorrent.Application.Features.Torrents.AddTorrent;
using HarborTorrent.Domain.Rss;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net;
using System.ServiceModel.Syndication;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using System.Xml;

namespace HarborTorrent.Infrastructure.BackgroundJobs;

internal sealed class RssPollingHostedService : BackgroundService, IRssFeedPoller
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);
    private static readonly Regex MagnetRegex = new(@"magnet:\?xt=urn:btih:[a-zA-Z0-9]+[^\s""'<>]*", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(1));
    private static readonly Regex TorrentUrlRegex = new(@"https?://[^\s""'<>]+\.torrent(?:\?[^\s""'<>]*)?", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(1));
    private static readonly Regex InfoHashRegex = new(@"^(?:[a-fA-F0-9]{40}|[a-zA-Z2-7]{32})$", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IRssPollStatusTracker _pollStatusTracker;
    private readonly ILogger<RssPollingHostedService> _logger;
    private readonly Channel<Guid> _pollQueue = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true });
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _feedLocks = new();

    public RssPollingHostedService(
        IServiceScopeFactory serviceScopeFactory,
        IHttpClientFactory httpClientFactory,
        IRssPollStatusTracker pollStatusTracker,
        ILogger<RssPollingHostedService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _httpClientFactory = httpClientFactory;
        _pollStatusTracker = pollStatusTracker;
        _logger = logger;
    }

    public void EnqueuePoll(Guid feedId)
    {
        _pollQueue.Writer.TryWrite(feedId);
    }

    public async Task<RssPollSummary> PollFeedAsync(Guid feedId, CancellationToken cancellationToken = default)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRssFeedRepository>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var feed = await repository.GetByIdAsync(feedId, cancellationToken);
        if (feed is null || !feed.IsActive)
        {
            return new RssPollSummary(0, 0, 0, []);
        }

        return await PollSingleFeedAsync(feed, repository, mediator, cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Execute immediately on startup
        try
        {
            await PollRssFeedsAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred during initial RSS polling.");
        }

        await Task.WhenAll(
            RunPeriodicPollLoopAsync(stoppingToken),
            RunQueuedPollLoopAsync(stoppingToken));
    }

    private async Task RunPeriodicPollLoopAsync(CancellationToken stoppingToken)
    {
        // Then poll every 15 minutes
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await PollRssFeedsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while polling RSS feeds.");
            }
        }
    }

    private async Task RunQueuedPollLoopAsync(CancellationToken stoppingToken)
    {
        await foreach (var feedId in _pollQueue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await PollFeedAsync(feedId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed immediate RSS poll for feed {FeedId}.", feedId);
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
            await PollSingleFeedAsync(feed, repository, mediator, cancellationToken);
        }
    }

    private async Task<RssPollSummary> PollSingleFeedAsync(
        RssFeed feed,
        IRssFeedRepository repository,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var feedLock = _feedLocks.GetOrAdd(feed.Id, _ => new SemaphoreSlim(1, 1));
        await feedLock.WaitAsync(cancellationToken);
        try
        {
            var summary = await ProcessFeedAsync(feed, repository, mediator, cancellationToken);

            feed.MarkAsPolled();
            repository.UpdateFeed(feed);
            await repository.SaveChangesAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(summary.ErrorMessage))
            {
                _pollStatusTracker.MarkFailure(feed.Id, summary.ErrorMessage);
            }
            else
            {
                _pollStatusTracker.MarkSuccess(feed.Id);
            }

            return summary;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _pollStatusTracker.MarkFailure(feed.Id, ex.Message);
            _logger.LogWarning(ex, "Failed to process RSS feed: {FeedName} ({FeedUrl})", feed.Name, feed.Url);
            return new RssPollSummary(0, 0, 0, [], ex.Message);
        }
        finally
        {
            feedLock.Release();
        }
    }

    private async Task<RssPollSummary> ProcessFeedAsync(RssFeed feed, IRssFeedRepository repository, IMediator mediator, CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient("Rss");
        using var response = await httpClient.GetAsync(feed.Url, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var xmlReader = XmlReader.Create(stream);
        var syndicationFeed = SyndicationFeed.Load(xmlReader);

        if (syndicationFeed == null)
        {
            return new RssPollSummary(0, 0, 0, []);
        }

        var items = syndicationFeed.Items.ToList();
        var itemsScanned = items.Count;
        var sampleTitles = items
            .Select(i => i.Title?.Text?.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Take(3)
            .Cast<string>()
            .ToList();

        if (feed.Filters.Count == 0)
        {
            return new RssPollSummary(itemsScanned, 0, 0, sampleTitles);
        }

        var matchedCount = 0;
        var addedCount = 0;
        string? firstError = null;

        foreach (var item in items)
        {
            var title = item.Title?.Text;
            var link = ResolveDownloadLink(item);

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(link))
                continue;

            var isMagnet = link.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase);
            var identifier = isMagnet ? link : title;

            foreach (var filter in feed.Filters)
            {
                if (!TryMatchFilter(title, filter.RegexPattern, out var isMatch))
                {
                    _logger.LogWarning("Skipping invalid or timed-out RSS regex filter '{Pattern}' on feed '{FeedName}'.", filter.RegexPattern, feed.Name);
                    continue;
                }

                if (!isMatch)
                    continue;

                matchedCount++;

                var alreadyProcessed = await repository.HasItemHistoryAsync(feed.Id, identifier, cancellationToken);
                if (alreadyProcessed)
                    continue;

                _logger.LogInformation("RSS Match found! Feed: {Feed}, Title: {Title}", feed.Name, title);

                var command = new AddTorrentCommand(
                    Name: isMagnet ? null : title,
                    SavePath: filter.SavePath,
                    MagnetLink: isMagnet ? link : null,
                    TorrentFileName: isMagnet ? null : title + ".torrent",
                    TorrentFileContentBase64: null
                );

                if (!isMagnet && Uri.TryCreate(link, UriKind.Absolute, out var torrentUri))
                {
                    try
                    {
                        using var torrentResponse = await httpClient.GetAsync(torrentUri, cancellationToken);
                        torrentResponse.EnsureSuccessStatusCode();
                        var contentType = torrentResponse.Content.Headers.ContentType?.MediaType;
                        var fileBytes = await torrentResponse.Content.ReadAsByteArrayAsync(cancellationToken);

                        if ((contentType is not null && contentType.Contains("html", StringComparison.OrdinalIgnoreCase)) ||
                            LooksLikeHtml(fileBytes))
                        {
                            firstError ??= $"Matched item '{title}' links to a webpage instead of a .torrent file or magnet link.";
                            _logger.LogWarning("RSS item '{Title}' resolved to an HTML webpage instead of a .torrent file: {Link}", title, link);
                            continue;
                        }

                        command = command with { TorrentFileContentBase64 = Convert.ToBase64String(fileBytes) };
                    }
                    catch (Exception ex)
                    {
                        firstError ??= $"Failed to download torrent for '{title}': {ex.Message}";
                        _logger.LogWarning(ex, "Failed to download .torrent file from RSS link: {Link}", link);
                        continue;
                    }
                }

                var result = await mediator.Send(command, cancellationToken);

                if (result.IsSuccess)
                {
                    addedCount++;
                    await repository.AddItemHistoryAsync(new RssFeedItemHistory(Guid.NewGuid(), feed.Id, filter.Id, identifier), cancellationToken);

                    // Automatically start the download so matched torrents begin immediately.
                    // Failure to start is non-fatal: the torrent is already in the library
                    // and can be started manually.
                    var addedTorrentId = result.Value!.Id;
                    try
                    {
                        var startCommand = new HarborTorrent.Application.Features.Torrents.StartTorrent.StartTorrentCommand(addedTorrentId);
                        await mediator.Send(startCommand, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "RSS-matched torrent {TorrentId} was added but could not be started automatically.", addedTorrentId);
                    }
                }
                else
                {
                    var errorText = string.Join(", ", result.Errors.Select(e => e.Message));
                    firstError ??= $"Matched '{title}' but failed to add torrent: {errorText}";
                    _logger.LogWarning("Failed to add torrent from RSS: {Errors}", errorText);
                }
            }
        }

        return new RssPollSummary(itemsScanned, matchedCount, addedCount, sampleTitles, firstError);
    }

    private static bool LooksLikeHtml(byte[] bytes)
    {
        var i = 0;
        while (i < bytes.Length && i < 64 && (bytes[i] == (byte)' ' || bytes[i] == (byte)'\t' || bytes[i] == (byte)'\r' || bytes[i] == (byte)'\n'))
        {
            i++;
        }
        return i < bytes.Length && bytes[i] == (byte)'<';
    }

    internal static string? ResolveDownloadLink(SyndicationItem item)
    {
        var links = item.Links ?? [];

        // 1. Prefer explicit magnet links anywhere in Links or Id
        var magnetLink = links
            .Select(l => l.Uri?.ToString())
            .FirstOrDefault(u => !string.IsNullOrWhiteSpace(u) && u.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(magnetLink))
        {
            return magnetLink;
        }

        if (!string.IsNullOrWhiteSpace(item.Id) && item.Id.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase))
        {
            return item.Id;
        }

        // 2. Check custom XML extensions (<torrent:magnetURI>, <magnetURI>, <infoHash>, etc.)
        var extensionMagnet = ExtractFromExtensions(item, out var extensionTorrentUrl);
        if (!string.IsNullOrWhiteSpace(extensionMagnet))
        {
            return extensionMagnet;
        }

        // 3. Check embedded magnet links inside <description> / <content>
        var bodyText = WebUtility.HtmlDecode(
            (item.Summary?.Text ?? string.Empty) + "\n" +
            ((item.Content as TextSyndicationContent)?.Text ?? string.Empty));
        var embeddedMagnet = MagnetRegex.Match(bodyText);
        if (embeddedMagnet.Success)
        {
            return embeddedMagnet.Value;
        }

        // 4. Prefer RSS <enclosure> or explicit .torrent / application/x-bittorrent links
        var enclosureLink = links.FirstOrDefault(l =>
            string.Equals(l.RelationshipType, "enclosure", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(l.MediaType, "application/x-bittorrent", StringComparison.OrdinalIgnoreCase) ||
            (l.Uri?.AbsolutePath.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase) ?? false));

        if (enclosureLink?.Uri is not null)
        {
            return enclosureLink.Uri.ToString();
        }

        // 5. Check custom XML extensions or body text for explicit .torrent URLs
        if (!string.IsNullOrWhiteSpace(extensionTorrentUrl))
        {
            return extensionTorrentUrl;
        }

        var embeddedTorrentUrl = TorrentUrlRegex.Match(bodyText);
        if (embeddedTorrentUrl.Success)
        {
            return embeddedTorrentUrl.Value;
        }

        // 6. Fallback to first link or item Id
        return links.FirstOrDefault()?.Uri?.ToString() ?? item.Id;
    }

    private static string? ExtractFromExtensions(SyndicationItem item, out string? torrentUrl)
    {
        torrentUrl = null;
        string? infoHash = null;

        foreach (var ext in item.ElementExtensions)
        {
            try
            {
                using var reader = ext.GetReader();
                var name = ext.OuterName;
                var value = reader.ReadElementContentAsString()?.Trim();
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                if (value.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("magnet", StringComparison.OrdinalIgnoreCase))
                {
                    if (value.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase))
                    {
                        return value;
                    }
                }
                else if (name.Contains("infohash", StringComparison.OrdinalIgnoreCase) && InfoHashRegex.IsMatch(value))
                {
                    infoHash ??= value;
                }
                else if (torrentUrl is null &&
                         Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                         uri.AbsolutePath.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase))
                {
                    torrentUrl = value;
                }
            }
            catch
            {
                // Ignore non-scalar XML extensions
            }
        }

        if (!string.IsNullOrWhiteSpace(infoHash))
        {
            return $"magnet:?xt=urn:btih:{infoHash}";
        }

        return null;
    }

    internal static bool TryMatchFilter(string title, string pattern, out bool isMatch)
    {
        try
        {
            isMatch = Regex.IsMatch(title, pattern, RegexOptions.IgnoreCase, RegexTimeout);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or RegexMatchTimeoutException)
        {
            isMatch = false;
            return false;
        }
    }
}
