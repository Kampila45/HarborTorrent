using System.ServiceModel.Syndication;
using HarborTorrent.Api.Endpoints;
using HarborTorrent.Application.Abstractions.Persistence;
using HarborTorrent.Application.Abstractions.Rss;
using HarborTorrent.Application.Features.Rss.AddRssFeed;
using HarborTorrent.Application.Features.Rss.AddRssFilter;
using HarborTorrent.Application.Features.Rss.GetRssFeeds;
using HarborTorrent.Domain.Rss;
using HarborTorrent.Domain.Settings;
using HarborTorrent.Infrastructure.BackgroundJobs;
using Xunit;

namespace HarborTorrent.Tests;

public sealed class FilesAndRssTests
{
    [Fact]
    public void PathContainment_BlocksPrefixSiblingAndTraversal()
    {
        var root = Path.Combine(Path.GetTempPath(), "harbor-root");
        var sibling = Path.Combine(Path.GetTempPath(), "harbor-root-evil", "file.mp4");
        var traversal = Path.Combine(root, "..", "harbor-root-evil", "file.mp4");
        var valid = Path.Combine(root, "sub", "movie.mp4");

        Assert.False(FilesEndpoints.IsSubPathOf(root, sibling));
        Assert.False(FilesEndpoints.IsSubPathOf(root, traversal));
        Assert.Null(FilesEndpoints.ResolveContainedPath(root, "../secret.txt"));
        Assert.True(FilesEndpoints.IsSubPathOf(root, valid));
        Assert.NotNull(FilesEndpoints.ResolveContainedPath(root, "sub/movie.mp4"));
    }

    [Fact]
    public void MimeMapping_IncludesWebmAndCommonFormats()
    {
        Assert.Equal("video/webm", FilesEndpoints.GetMimeType("clip.webm"));
        Assert.Equal("video/mp4", FilesEndpoints.GetMimeType("clip.mp4"));
        Assert.Equal("video/x-matroska", FilesEndpoints.GetMimeType("clip.mkv"));
    }

    [Fact]
    public void RssResolver_PrefersEnclosureOrMagnetOverHtmlPageLink()
    {
        var item = new SyndicationItem
        {
            Title = new TextSyndicationContent("Ubuntu 26.04 LTS")
        };
        item.Links.Add(new SyndicationLink(new Uri("https://example.com/release-notes.html")));
        item.Links.Add(new SyndicationLink(new Uri("https://example.com/ubuntu.torrent"), "enclosure", null, "application/x-bittorrent", 0));

        var resolved = RssPollingHostedService.ResolveDownloadLink(item);
        Assert.Equal("https://example.com/ubuntu.torrent", resolved);
    }

    [Fact]
    public void RssResolver_ExtractsEmbeddedMagnetOrTorrentFromSummaryAndExtensions()
    {
        var summaryItem = new SyndicationItem
        {
            Title = new TextSyndicationContent("Ubuntu 24.04"),
            Summary = new TextSyndicationContent("Download via <a href=\"magnet:?xt=urn:btih:0123456789ABCDEF0123456789ABCDEF01234567&amp;dn=Ubuntu\">Magnet</a>")
        };
        summaryItem.Links.Add(new SyndicationLink(new Uri("https://example.com/distributions/ubuntu")));
        Assert.Equal(
            "magnet:?xt=urn:btih:0123456789ABCDEF0123456789ABCDEF01234567&dn=Ubuntu",
            RssPollingHostedService.ResolveDownloadLink(summaryItem));

        var torrentBodyItem = new SyndicationItem
        {
            Title = new TextSyndicationContent("Fedora 42"),
            Summary = new TextSyndicationContent("Get file: https://example.com/torrents/fedora-42.torrent")
        };
        torrentBodyItem.Links.Add(new SyndicationLink(new Uri("https://example.com/distributions/fedora")));
        Assert.Equal(
            "https://example.com/torrents/fedora-42.torrent",
            RssPollingHostedService.ResolveDownloadLink(torrentBodyItem));
    }

    [Fact]
    public void RssMatcher_RejectsInvalidRegexGracefully()
    {
        Assert.False(RssPollingHostedService.TryMatchFilter("Ubuntu 26.04", "[unclosed", out var isMatch));
        Assert.False(isMatch);

        var validator = new AddRssFilterCommandValidator();
        var validationResult = validator.Validate(new AddRssFilterCommand(Guid.NewGuid(), "[unclosed", "/tmp/downloads"));
        Assert.False(validationResult.IsValid);
    }

    [Fact]
    public async Task RssPollStatusTracker_SurfacesAndClearsFeedPollFailures()
    {
        var tracker = new RssPollStatusTracker(TimeProvider.System);
        var feed = new RssFeed(Guid.NewGuid(), "Linux Releases", "https://example.com/rss.xml");
        var repo = new StubRssRepository([feed]);
        var handler = new GetRssFeedsQueryHandler(repo, tracker);

        tracker.MarkFailure(feed.Id, "HTTP 503 Service Unavailable");
        var failedList = await handler.Handle(new GetRssFeedsQuery(), CancellationToken.None);
        var failedDto = Assert.Single(failedList);
        Assert.Equal("HTTP 503 Service Unavailable", failedDto.LastError);
        Assert.NotNull(failedDto.LastErrorAt);

        tracker.MarkSuccess(feed.Id);
        var recoveredList = await handler.Handle(new GetRssFeedsQuery(), CancellationToken.None);
        var recoveredDto = Assert.Single(recoveredList);
        Assert.Null(recoveredDto.LastError);
        Assert.Null(recoveredDto.LastErrorAt);
    }

    [Fact]
    public async Task AddRssFeedAndFilterHandlers_EnqueueImmediatePoll_AndDefaultSavePath()
    {
        var repo = new StubRssRepository([]);
        var settingsRepo = new StubSettingsRepository(new GlobalSettings
        {
            DefaultDownloadPath = "/home/user/Downloads/HarborTorrent"
        });
        var poller = new SpyRssFeedPoller();

        var addFeedHandler = new AddRssFeedCommandHandler(repo, poller);
        var feed = await addFeedHandler.Handle(new AddRssFeedCommand("FOSS", "https://example.com/rss.xml"), CancellationToken.None);
        Assert.Contains(feed.Id, poller.EnqueuedFeedIds);

        var addFilterHandler = new AddRssFilterCommandHandler(repo, settingsRepo, poller);
        var defaultPathFilter = await addFilterHandler.Handle(new AddRssFilterCommand(feed.Id, "Ubuntu.*Desktop"), CancellationToken.None);
        Assert.Equal("/home/user/Downloads/HarborTorrent", defaultPathFilter.SavePath);

        var customPathFilter = await addFilterHandler.Handle(new AddRssFilterCommand(feed.Id, "Debian", "/custom/iso"), CancellationToken.None);
        Assert.Equal("/custom/iso", customPathFilter.SavePath);
        Assert.Equal(3, poller.EnqueuedFeedIds.Count);
    }

    private sealed class SpyRssFeedPoller : IRssFeedPoller
    {
        public List<Guid> EnqueuedFeedIds { get; } = [];
        public void EnqueuePoll(Guid feedId) => EnqueuedFeedIds.Add(feedId);
        public Task<RssPollSummary> PollFeedAsync(Guid feedId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RssPollSummary(0, 0, 0, []));
    }

    private sealed class StubSettingsRepository : ISettingsRepository
    {
        private readonly GlobalSettings _settings;
        public StubSettingsRepository(GlobalSettings settings) => _settings = settings;
        public Task<GlobalSettings> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(_settings);
        public Task UpdateAsync(GlobalSettings settings, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubRssRepository : IRssFeedRepository
    {
        private readonly IReadOnlyCollection<RssFeed> _feeds;
        public StubRssRepository(IReadOnlyCollection<RssFeed> feeds) => _feeds = feeds;
        public Task<IReadOnlyCollection<RssFeed>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(_feeds);
        public Task<RssFeed?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(_feeds.FirstOrDefault(f => f.Id == id));
        public Task AddAsync(RssFeed feed, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddFilterAsync(RssFilter filter, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void UpdateFeed(RssFeed feed) { }
        public void Remove(RssFeed feed) { }
        public Task<RssFilter?> GetFilterByIdAsync(Guid filterId, CancellationToken cancellationToken = default) => Task.FromResult<RssFilter?>(null);
        public void RemoveFilter(RssFilter filter) { }
        public Task<bool> HasItemHistoryAsync(Guid feedId, string itemIdentifier, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task AddItemHistoryAsync(RssFeedItemHistory history, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}

