using HarborTorrent.Application.Abstractions.Persistence;
using HarborTorrent.Application.Abstractions.Realtime;
using HarborTorrent.Application.Abstractions.Runtime;
using HarborTorrent.Application.Contracts.Torrents;
using HarborTorrent.Application.Features.Dashboard.GetMetrics;
using HarborTorrent.Application.Features.Torrents.PauseTorrent;
using HarborTorrent.Domain.Common;
using HarborTorrent.Domain.Settings;
using HarborTorrent.Domain.Torrents;
using Xunit;

namespace HarborTorrent.Tests;

public sealed class TorrentDomainAndMetricsTests
{
    private const string SampleInfoHash = "0123456789abcdef0123456789abcdef01234567";

    [Fact]
    public void Torrent_CanPauseWhileSeeding_AndFailClearsOnRestart()
    {
        var now = DateTimeOffset.UtcNow;
        var source = TorrentSource.FromMagnetLink($"magnet:?xt=urn:btih:{SampleInfoHash}&dn=Ubuntu").Value!;
        var torrent = Torrent.Create(SampleInfoHash, "Ubuntu", "/tmp/downloads", "local", source, now).Value!;

        Assert.True(torrent.Start(now).IsSuccess);
        torrent.UpdateProgress(100m, 1024, 512, 0, 100, 0.5m, 0, now.AddSeconds(5));
        Assert.Equal(TorrentStatus.Seeding, torrent.Status);

        Assert.True(torrent.Pause(now.AddSeconds(10)).IsSuccess);
        Assert.Equal(TorrentStatus.Paused, torrent.Status);

        Assert.True(torrent.Start(now.AddSeconds(15)).IsSuccess);
        Assert.Equal(TorrentStatus.Seeding, torrent.Status);

        torrent.Fail("Disk full", now.AddSeconds(20));
        Assert.Equal(TorrentStatus.Stopped, torrent.Status);
        Assert.Equal("Disk full", torrent.ErrorMessage);

        Assert.True(torrent.Start(now.AddSeconds(25)).IsSuccess);
        Assert.Null(torrent.ErrorMessage);
    }

    [Fact]
    public async Task DashboardMetrics_AggregatesAcrossMoreThanTenTorrents()
    {
        var now = DateTimeOffset.UtcNow;
        var torrents = new List<Torrent>();

        for (var i = 0; i < 15; i++)
        {
            var hash = $"{i:x2}".PadRight(40, 'a');
            var source = TorrentSource.FromMagnetLink($"magnet:?xt=urn:btih:{hash}&dn=Item{i}").Value!;
            var torrent = Torrent.Create(hash, $"Item {i}", "/tmp/downloads", "local", source, now).Value!;
            torrent.Start(now);

            if (i < 12)
            {
                torrent.UpdateProgress(100m, 1_000, 500, 0, 10, 0.5m, 0, now.AddSeconds(1));
            }
            else
            {
                torrent.UpdateProgress(50m, 500, 100, 20, 5, 0.2m, 25, now.AddSeconds(1));
            }

            torrents.Add(torrent);
        }

        var handler = new GetDashboardMetricsQueryHandler(new StubTorrentRepository(torrents));
        var metrics = await handler.Handle(new GetDashboardMetricsQuery(), CancellationToken.None);

        Assert.Equal(15, metrics.TotalTorrents);
        Assert.Equal(15, metrics.ActiveDownloads);
        Assert.Equal(12, metrics.CompletedTorrents);
        Assert.Equal(12 * 1_000 + 3 * 500, metrics.TotalDownloadedBytes);
        Assert.Equal(12 * 500 + 3 * 100, metrics.TotalUploadedBytes);
    }

    [Fact]
    public async Task PauseTorrentHandler_DoesNotPersistOrBroadcast_WhenRuntimeTransitionFails()
    {
        var now = DateTimeOffset.UtcNow;
        var source = TorrentSource.FromMagnetLink($"magnet:?xt=urn:btih:{SampleInfoHash}&dn=Ubuntu").Value!;
        var torrent = Torrent.Create(SampleInfoHash, "Ubuntu", "/tmp/downloads", "local", source, now).Value!;
        torrent.Start(now);

        var repo = new TrackingTorrentRepository([torrent]);
        var publisher = new TrackingEventPublisher();
        var runtime = new FailingRuntimeCoordinator(Error.Conflict("Engine busy"));
        var handler = new PauseTorrentCommandHandler(repo, runtime, publisher, TimeProvider.System);

        var result = await handler.Handle(new PauseTorrentCommand(torrent.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(0, repo.SaveCount);
        Assert.Empty(publisher.PublishedEvents);
    }

    private sealed class TrackingTorrentRepository : ITorrentRepository
    {
        private readonly IReadOnlyCollection<Torrent> _torrents;
        public int SaveCount { get; private set; }
        public TrackingTorrentRepository(IReadOnlyCollection<Torrent> torrents) => _torrents = torrents;
        public Task AddAsync(Torrent torrent, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<Torrent?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(_torrents.FirstOrDefault(t => t.Id == id));
        public Task<bool> ExistsByInfoHashAsync(string infoHash, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<IReadOnlyCollection<Torrent>> ListAsync(CancellationToken cancellationToken) => Task.FromResult(_torrents);
        public Task<IReadOnlyCollection<Torrent>> ListAllUnfilteredAsync(CancellationToken cancellationToken) => Task.FromResult(_torrents);
        public Task<(IReadOnlyCollection<Torrent> Items, int TotalCount)> ListPaginatedAsync(string? status, string? query, int pageNumber, int pageSize, CancellationToken cancellationToken) =>
            Task.FromResult<(IReadOnlyCollection<Torrent>, int)>((_torrents, _torrents.Count));
        public Task<IReadOnlyCollection<Torrent>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) => Task.FromResult(_torrents);
        public Task DeleteAsync(Torrent torrent, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class TrackingEventPublisher : ITorrentEventPublisher
    {
        public List<TorrentEventType> PublishedEvents { get; } = new();
        public Task PublishAsync(TorrentEventType eventType, TorrentDto torrent, CancellationToken cancellationToken)
        {
            PublishedEvents.Add(eventType);
            return Task.CompletedTask;
        }
        public Task PublishRemovedAsync(Guid torrentId, string userId, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FailingRuntimeCoordinator : ITorrentRuntimeCoordinator
    {
        private readonly Error _error;
        public FailingRuntimeCoordinator(Error error) => _error = error;
        public Task<Result> StartAsync(Guid torrentId, CancellationToken cancellationToken) => Task.FromResult(Result.Failure(_error));
        public Task<Result> PauseAsync(Guid torrentId, CancellationToken cancellationToken) => Task.FromResult(Result.Failure(_error));
        public Task<Result> StopAsync(Guid torrentId, CancellationToken cancellationToken) => Task.FromResult(Result.Failure(_error));
        public Task RemoveAsync(Guid torrentId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyCollection<TorrentRuntimeSnapshot>> GetSnapshotsAsync(IReadOnlyCollection<Guid> torrentIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<TorrentRuntimeSnapshot>>(Array.Empty<TorrentRuntimeSnapshot>());
        public Task<IReadOnlyCollection<TorrentFileSnapshot>> GetFilesAsync(Guid torrentId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<TorrentFileSnapshot>>(Array.Empty<TorrentFileSnapshot>());
        public Task UpdateFilesPriorityAsync(Guid torrentId, IReadOnlyCollection<FilePriorityUpdateDto> updates, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<Stream?> CreateStreamAsync(Guid torrentId, int fileIndex, CancellationToken cancellationToken) => Task.FromResult<Stream?>(null);
        public Task UpdateEngineSettingsAsync(GlobalSettings settings, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyCollection<PeerSnapshot>> GetPeersAsync(Guid torrentId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<PeerSnapshot>>(Array.Empty<PeerSnapshot>());
        public Task<IReadOnlyCollection<TrackerSnapshot>> GetTrackersAsync(Guid torrentId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<TrackerSnapshot>>(Array.Empty<TrackerSnapshot>());
        public Task AddTrackerAsync(Guid torrentId, string trackerUrl, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class StubTorrentRepository : ITorrentRepository
    {
        private readonly IReadOnlyCollection<Torrent> _torrents;

        public StubTorrentRepository(IReadOnlyCollection<Torrent> torrents) => _torrents = torrents;

        public Task AddAsync(Torrent torrent, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<Torrent?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(_torrents.FirstOrDefault(t => t.Id == id));
        public Task<bool> ExistsByInfoHashAsync(string infoHash, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<IReadOnlyCollection<Torrent>> ListAsync(CancellationToken cancellationToken) => Task.FromResult(_torrents);
        public Task<IReadOnlyCollection<Torrent>> ListAllUnfilteredAsync(CancellationToken cancellationToken) => Task.FromResult(_torrents);
        public Task<(IReadOnlyCollection<Torrent> Items, int TotalCount)> ListPaginatedAsync(string? status, string? query, int pageNumber, int pageSize, CancellationToken cancellationToken) =>
            Task.FromResult<(IReadOnlyCollection<Torrent>, int)>((_torrents, _torrents.Count));
        public Task<IReadOnlyCollection<Torrent>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) => Task.FromResult(_torrents);
        public Task DeleteAsync(Torrent torrent, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

