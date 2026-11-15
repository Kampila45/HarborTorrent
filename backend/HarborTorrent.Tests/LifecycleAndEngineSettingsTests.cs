using HarborTorrent.Application.Abstractions.Persistence;
using HarborTorrent.Application.Abstractions.Realtime;
using HarborTorrent.Application.Abstractions.Runtime;
using HarborTorrent.Application.Contracts.Torrents;
using HarborTorrent.Application.Features.Settings.UpdateSettings;
using HarborTorrent.Domain.Common;
using HarborTorrent.Domain.Settings;
using HarborTorrent.Domain.Torrents;
using HarborTorrent.Infrastructure.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HarborTorrent.Tests;

public sealed class LifecycleAndEngineSettingsTests
{
    private static Torrent CreateTorrent(string suffix, Action<Torrent>? configure = null)
    {
        var hash = suffix.PadRight(40, '0');
        var now = DateTimeOffset.UtcNow;
        var source = TorrentSource.FromMagnetLink($"magnet:?xt=urn:btih:{hash}&dn=Item{suffix}").Value!;
        var torrent = Torrent.Create(hash, $"Item {suffix}", "/tmp/downloads", "local", source, now).Value!;
        configure?.Invoke(torrent);
        return torrent;
    }

    [Fact]
    public async Task RestoreActiveTorrentsAsync_AppliesSavedSettings_AndRestoresDownloadingSeedingPausedOnly()
    {
        var now = DateTimeOffset.UtcNow;
        var downloading = CreateTorrent("01", t => t.Start(now));
        var seeding = CreateTorrent("02", t =>
        {
            t.Start(now);
            t.UpdateProgress(100m, 1000, 100, 0, 10, 0.1m, 0, now.AddSeconds(1));
        });
        var paused = CreateTorrent("03", t =>
        {
            t.Start(now);
            t.Pause(now.AddSeconds(1));
        });
        var stopped = CreateTorrent("04");
        var completed = CreateTorrent("05", t =>
        {
            t.Start(now);
            t.UpdateProgress(100m, 1000, 1000, 0, 10, 1.0m, 0, now.AddSeconds(1));
            t.Complete(now.AddSeconds(2));
        });

        var savedSettings = new GlobalSettings
        {
            EnableDht = false,
            EnablePex = false,
            EnableLpd = true,
            DefaultDownloadPath = "/tmp/downloads"
        };

        var torrentRepo = new InMemoryTorrentRepository([downloading, seeding, paused, stopped, completed]);
        var settingsRepo = new InMemorySettingsRepository(savedSettings);
        var coordinator = new RecordingRuntimeCoordinator();
        var publisher = new RecordingEventPublisher();

        var services = new ServiceCollection();
        services.AddScoped<ITorrentRepository>(_ => torrentRepo);
        services.AddScoped<ISettingsRepository>(_ => settingsRepo);
        services.AddScoped<ITorrentEventPublisher>(_ => publisher);
        using var provider = services.BuildServiceProvider();

        var hostedService = new TorrentLifecycleHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            coordinator,
            TimeProvider.System,
            NullLogger<TorrentLifecycleHostedService>.Instance,
            new ConfigurationBuilder().Build());

        await hostedService.RestoreActiveTorrentsAsync(CancellationToken.None);

        Assert.Single(coordinator.AppliedSettings);
        Assert.False(coordinator.AppliedSettings[0].EnableDht);
        Assert.False(coordinator.AppliedSettings[0].EnablePex);
        Assert.True(coordinator.AppliedSettings[0].EnableLpd);

        Assert.Contains(downloading.Id, coordinator.StartedIds);
        Assert.Contains(seeding.Id, coordinator.StartedIds);
        Assert.Contains(paused.Id, coordinator.StartedIds);
        Assert.DoesNotContain(stopped.Id, coordinator.StartedIds);
        Assert.DoesNotContain(completed.Id, coordinator.StartedIds);

        Assert.Equal([paused.Id], coordinator.PausedIds);
    }

    [Fact]
    public async Task AdvanceActiveTorrentsAsync_ReconcilesErrorStoppedAndTargetSeedRatio()
    {
        var now = DateTimeOffset.UtcNow;
        var errored = CreateTorrent("11", t => t.Start(now));
        var runtimeStopped = CreateTorrent("12", t => t.Start(now));
        var seedTargetMet = CreateTorrent("13", t =>
        {
            t.Start(now);
            t.UpdateProgress(100m, 1000, 500, 0, 10, 0.5m, 0, now.AddSeconds(1));
        });

        var torrentRepo = new InMemoryTorrentRepository([errored, runtimeStopped, seedTargetMet]);
        var coordinator = new RecordingRuntimeCoordinator();
        coordinator.Snapshots[errored.Id] = new TorrentRuntimeSnapshot(
            errored.Id, TorrentStatus.Downloading, 10m, 100, 0, 0, 0, 0m, null, ErrorMessage: "Disk full (ENOSPC)");
        coordinator.Snapshots[runtimeStopped.Id] = new TorrentRuntimeSnapshot(
            runtimeStopped.Id, TorrentStatus.Stopped, 25m, 250, 10, 0, 0, 0.04m, null);
        coordinator.Snapshots[seedTargetMet.Id] = new TorrentRuntimeSnapshot(
            seedTargetMet.Id, TorrentStatus.Seeding, 100m, 1000, 1500, 0, 100, 1.5m, 0);

        var publisher = new RecordingEventPublisher();
        var services = new ServiceCollection();
        services.AddScoped<ITorrentRepository>(_ => torrentRepo);
        services.AddScoped<ITorrentEventPublisher>(_ => publisher);
        using var provider = services.BuildServiceProvider();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TorrentSettings:TargetSeedRatio"] = "1.0"
            })
            .Build();

        var hostedService = new TorrentLifecycleHostedService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            coordinator,
            TimeProvider.System,
            NullLogger<TorrentLifecycleHostedService>.Instance,
            config);

        await hostedService.AdvanceActiveTorrentsAsync(CancellationToken.None);

        Assert.Equal(TorrentStatus.Stopped, errored.Status);
        Assert.Equal("Disk full (ENOSPC)", errored.ErrorMessage);

        Assert.Equal(TorrentStatus.Stopped, runtimeStopped.Status);

        Assert.Equal(TorrentStatus.Completed, seedTargetMet.Status);
        Assert.Contains(seedTargetMet.Id, coordinator.StoppedIds);

        Assert.Contains(publisher.Events, e => e.EventType == TorrentEventType.Stopped && e.Torrent.Id == errored.Id);
        Assert.Contains(publisher.Events, e => e.EventType == TorrentEventType.Stopped && e.Torrent.Id == runtimeStopped.Id);
        Assert.Contains(publisher.Events, e => e.EventType == TorrentEventType.Completed && e.Torrent.Id == seedTargetMet.Id);
        Assert.Equal(1, torrentRepo.SaveCount);
    }

    [Fact]
    public async Task UpdateSettingsAndMonoTorrentCoordinator_ApplyEngineDhtLpdAndPexFlags()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"harbor-coord-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var settingsRepo = new InMemorySettingsRepository(new GlobalSettings
            {
                DefaultDownloadPath = tempDir
            });
            var services = new ServiceCollection();
            services.AddScoped<ISettingsRepository>(_ => settingsRepo);
            using var provider = services.BuildServiceProvider();

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["HarborTorrent:RuntimeDataPath"] = tempDir
                })
                .Build();

            using var coordinator = new MonoTorrentRuntimeCoordinator(
                provider.GetRequiredService<IServiceScopeFactory>(),
                config,
                NullLogger<MonoTorrentRuntimeCoordinator>.Instance);

            var handler = new UpdateSettingsCommandHandler(settingsRepo, coordinator);

            await handler.Handle(new UpdateSettingsCommand(
                MaxDownloadSpeedBytes: 512_000,
                MaxUploadSpeedBytes: 128_000,
                DefaultDownloadPath: tempDir,
                EnableDht: false,
                EnablePex: false,
                EnableLpd: false,
                AnchorModeEnabled: false,
                AnchorMaxDownloadSpeedBytes: 50_000,
                AnchorMaxUploadSpeedBytes: 10_000), CancellationToken.None);

            var updated = await settingsRepo.GetAsync(CancellationToken.None);
            Assert.False(updated.EnableDht);
            Assert.False(updated.EnablePex);
            Assert.False(updated.EnableLpd);
            Assert.Null(coordinator.CurrentEngineSettings.DhtEndPoint);
            Assert.False(coordinator.CurrentEngineSettings.AllowLocalPeerDiscovery);
            Assert.Equal(512_000, coordinator.CurrentEngineSettings.MaximumDownloadRate);
            Assert.Equal(128_000, coordinator.CurrentEngineSettings.MaximumUploadRate);

            // Toggling Anchor Mode switches active engine limits without dropping DHT/PEX
            await handler.Handle(new UpdateSettingsCommand(
                MaxDownloadSpeedBytes: 512_000,
                MaxUploadSpeedBytes: 128_000,
                DefaultDownloadPath: tempDir,
                EnableDht: true,
                EnablePex: false,
                EnableLpd: true,
                AnchorModeEnabled: true,
                AnchorMaxDownloadSpeedBytes: 50_000,
                AnchorMaxUploadSpeedBytes: 10_000), CancellationToken.None);

            Assert.NotNull(coordinator.CurrentEngineSettings.DhtEndPoint);
            Assert.True(coordinator.CurrentEngineSettings.AllowLocalPeerDiscovery);
            Assert.Equal(50_000, coordinator.CurrentEngineSettings.MaximumDownloadRate);
            Assert.Equal(10_000, coordinator.CurrentEngineSettings.MaximumUploadRate);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }
    }

    [Fact]
    public void TorrentSource_BuildMagnetUriAndFromMagnetLink_IncludeLiveUdpAndHttpTrackersWithoutDefunctTrackers()
    {
        const string infoHash = "0123456789abcdef0123456789abcdef01234567";
        var built = TorrentSource.BuildMagnetUri(infoHash, "Serum v1.11b3 UPDATE + Cymatics packs");

        Assert.Contains($"xt=urn:btih:{infoHash}", built);
        Assert.Contains(Uri.EscapeDataString("udp://tracker.opentrackr.org:1337/announce"), built);
        Assert.Contains(Uri.EscapeDataString("https://tracker.opentrackr.org:443/announce"), built);
        Assert.Contains(Uri.EscapeDataString("http://tracker.opentrackr.org:1337/announce"), built);
        Assert.DoesNotContain("coppersurfer.tk", built, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("glotorrents.pw", built, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("9.rarbg.com", built, StringComparison.OrdinalIgnoreCase);

        var parsed = TorrentSource.FromMagnetLink(built);
        Assert.True(parsed.IsSuccess);
        // Existing trackers in built magnet should not be duplicated
        var openTrackrCount = parsed.Value!.Value.Split(Uri.EscapeDataString("udp://tracker.opentrackr.org:1337/announce"), StringSplitOptions.None).Length - 1;
        Assert.Equal(1, openTrackrCount);
    }

    [Fact]
    public async Task GetTrackersAsync_ReturnsConfiguredTrackers_WhenFirstAnnounceHasNotCompleted()
    {
        var nowUtc = new DateTime(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);
        Assert.Null(MonoTorrentRuntimeCoordinator.ResolveLastAnnounceUtc(TimeSpan.MaxValue, nowUtc));
        Assert.Null(MonoTorrentRuntimeCoordinator.ResolveLastAnnounceUtc(TimeSpan.FromSeconds(-1), nowUtc));
        Assert.Equal(nowUtc.AddSeconds(-30), MonoTorrentRuntimeCoordinator.ResolveLastAnnounceUtc(TimeSpan.FromSeconds(30), nowUtc));

        var tempDir = Path.Combine(Path.GetTempPath(), $"harbor-tracker-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            const string infoHash = "fedcba9876543210fedcba9876543210fedcba98";
            var source = TorrentSource.FromMagnetLink($"magnet:?xt=urn:btih:{infoHash}&dn=MagnetProbe").Value!;
            var torrent = Torrent.Create(infoHash, "MagnetProbe", Path.Combine(tempDir, "dl"), "local", source, DateTimeOffset.UtcNow).Value!;

            var torrentRepo = new InMemoryTorrentRepository([torrent]);
            var settingsRepo = new InMemorySettingsRepository(new GlobalSettings { DefaultDownloadPath = tempDir });

            var services = new ServiceCollection();
            services.AddScoped<ITorrentRepository>(_ => torrentRepo);
            services.AddScoped<ISettingsRepository>(_ => settingsRepo);
            using var provider = services.BuildServiceProvider();

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["HarborTorrent:RuntimeDataPath"] = Path.Combine(tempDir, "runtime")
                })
                .Build();

            using var coordinator = new MonoTorrentRuntimeCoordinator(
                provider.GetRequiredService<IServiceScopeFactory>(),
                config,
                NullLogger<MonoTorrentRuntimeCoordinator>.Instance);

            var trackers = await coordinator.GetTrackersAsync(torrent.Id, CancellationToken.None);

            Assert.NotEmpty(trackers);
            Assert.Contains(trackers, t => t.Uri.Contains("tracker.opentrackr.org", StringComparison.OrdinalIgnoreCase));

            // Every magnet tracker must live in its own tier so MonoTorrent announces to all of them concurrently.
            var tierSizes = coordinator.GetTrackerTierSizes(torrent.Id);
            Assert.True(tierSizes.Count >= TorrentSource.DefaultTrackers.Count);
            Assert.All(tierSizes, count => Assert.Equal(1, count));

            // Performance defaults applied on creation and preserved through engine settings updates
            Assert.Equal(MonoTorrentRuntimeCoordinator.DefaultDiskCacheBytes, coordinator.CurrentEngineSettings.DiskCacheBytes);
            Assert.Equal(MonoTorrent.PieceWriter.CachePolicy.ReadsAndWrites, coordinator.CurrentEngineSettings.DiskCachePolicy);
            Assert.True(coordinator.CurrentEngineSettings.AllowHaveSuppression);
            Assert.Equal(MonoTorrentRuntimeCoordinator.DefaultEngineMaxConnections, coordinator.CurrentEngineSettings.MaximumConnections);
            Assert.Equal(MonoTorrentRuntimeCoordinator.DefaultMaxHalfOpenConnections, coordinator.CurrentEngineSettings.MaximumHalfOpenConnections);
            Assert.Equal(MonoTorrentRuntimeCoordinator.DefaultStaleRequestTimeout, coordinator.CurrentEngineSettings.StaleRequestTimeout);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }
    }

    [Fact]
    public async Task TorrentFile_AddsWithStandardPicker_AndUpgradesToStreamingOnDemand()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"harbor-torrent-stream-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var payloadDir = Path.Combine(tempDir, "payload");
            Directory.CreateDirectory(payloadDir);
            var payloadFile = Path.Combine(payloadDir, "sample.bin");
            var payloadBytes = new byte[64 * 1024];
            Random.Shared.NextBytes(payloadBytes);
            await File.WriteAllBytesAsync(payloadFile, payloadBytes);

            var creator = new MonoTorrent.TorrentCreator { PieceLength = 16384 };
            var encodedTorrent = (await creator.CreateAsync(new MonoTorrent.TorrentFileSource(payloadFile))).Encode();
            var parsedTorrent = MonoTorrent.Torrent.Load(encodedTorrent);

            var source = TorrentSource.FromTorrentFile("sample.torrent", Convert.ToBase64String(encodedTorrent)).Value!;
            var dlPath = Path.Combine(tempDir, "dl");
            var torrent = Torrent.Create(
                parsedTorrent.InfoHashes.V1!.ToHex(),
                "sample",
                dlPath,
                "local",
                source,
                DateTimeOffset.UtcNow).Value!;

            var torrentRepo = new InMemoryTorrentRepository([torrent]);
            var settingsRepo = new InMemorySettingsRepository(new GlobalSettings { DefaultDownloadPath = dlPath });

            var services = new ServiceCollection();
            services.AddScoped<ITorrentRepository>(_ => torrentRepo);
            services.AddScoped<ISettingsRepository>(_ => settingsRepo);
            using var provider = services.BuildServiceProvider();

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["HarborTorrent:RuntimeDataPath"] = Path.Combine(tempDir, "runtime")
                })
                .Build();

            using var coordinator = new MonoTorrentRuntimeCoordinator(
                provider.GetRequiredService<IServiceScopeFactory>(),
                config,
                NullLogger<MonoTorrentRuntimeCoordinator>.Instance);

            var startResult = await coordinator.StartAsync(torrent.Id, CancellationToken.None);
            Assert.True(startResult.IsSuccess);

            var files = await coordinator.GetFilesAsync(torrent.Id, CancellationToken.None);
            Assert.Single(files);
            Assert.Equal(payloadBytes.Length, files.First().Length);

            // Place the downloaded file where MonoTorrent stores single-file torrents, then open a stream on demand
            await coordinator.StopAsync(torrent.Id, CancellationToken.None);
            Directory.CreateDirectory(dlPath);
            await File.WriteAllBytesAsync(Path.Combine(dlPath, "sample.bin"), payloadBytes);

            using var stream = await coordinator.CreateStreamAsync(torrent.Id, 0, CancellationToken.None);
            Assert.NotNull(stream);
            var readBuffer = new byte[512];
            var readCount = await stream!.ReadAsync(readBuffer);
            Assert.Equal(512, readCount);
            Assert.True(readBuffer.AsSpan().SequenceEqual(payloadBytes.AsSpan(0, 512)));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }
    }

    private sealed class InMemoryTorrentRepository : ITorrentRepository
    {
        private readonly IReadOnlyCollection<Torrent> _torrents;
        public int SaveCount { get; private set; }
        public InMemoryTorrentRepository(IReadOnlyCollection<Torrent> torrents) => _torrents = torrents;
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

    private sealed class InMemorySettingsRepository : ISettingsRepository
    {
        private GlobalSettings _settings;
        public InMemorySettingsRepository(GlobalSettings settings) => _settings = settings;
        public Task<GlobalSettings> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(_settings);
        public Task UpdateAsync(GlobalSettings settings, CancellationToken cancellationToken = default)
        {
            _settings = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingEventPublisher : ITorrentEventPublisher
    {
        public List<(TorrentEventType EventType, TorrentDto Torrent)> Events { get; } = new();
        public Task PublishAsync(TorrentEventType eventType, TorrentDto torrent, CancellationToken cancellationToken)
        {
            Events.Add((eventType, torrent));
            return Task.CompletedTask;
        }
        public Task PublishRemovedAsync(Guid torrentId, string userId, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class RecordingRuntimeCoordinator : ITorrentRuntimeCoordinator
    {
        public List<Guid> StartedIds { get; } = new();
        public List<Guid> PausedIds { get; } = new();
        public List<Guid> StoppedIds { get; } = new();
        public List<GlobalSettings> AppliedSettings { get; } = new();
        public Dictionary<Guid, TorrentRuntimeSnapshot> Snapshots { get; } = new();

        public Task<Result> StartAsync(Guid torrentId, CancellationToken cancellationToken)
        {
            StartedIds.Add(torrentId);
            return Task.FromResult(Result.Success());
        }

        public Task<Result> PauseAsync(Guid torrentId, CancellationToken cancellationToken)
        {
            PausedIds.Add(torrentId);
            return Task.FromResult(Result.Success());
        }

        public Task<Result> StopAsync(Guid torrentId, CancellationToken cancellationToken)
        {
            StoppedIds.Add(torrentId);
            return Task.FromResult(Result.Success());
        }

        public Task RemoveAsync(Guid torrentId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyCollection<TorrentRuntimeSnapshot>> GetSnapshotsAsync(IReadOnlyCollection<Guid> torrentIds, CancellationToken cancellationToken)
        {
            var matched = torrentIds
                .Where(id => Snapshots.ContainsKey(id))
                .Select(id => Snapshots[id])
                .ToArray();
            return Task.FromResult<IReadOnlyCollection<TorrentRuntimeSnapshot>>(matched);
        }

        public Task<IReadOnlyCollection<TorrentFileSnapshot>> GetFilesAsync(Guid torrentId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<TorrentFileSnapshot>>(Array.Empty<TorrentFileSnapshot>());
        public Task UpdateFilesPriorityAsync(Guid torrentId, IReadOnlyCollection<FilePriorityUpdateDto> updates, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<Stream?> CreateStreamAsync(Guid torrentId, int fileIndex, CancellationToken cancellationToken) => Task.FromResult<Stream?>(null);
        public Task UpdateEngineSettingsAsync(GlobalSettings settings, CancellationToken cancellationToken)
        {
            AppliedSettings.Add(settings);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyCollection<PeerSnapshot>> GetPeersAsync(Guid torrentId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<PeerSnapshot>>(Array.Empty<PeerSnapshot>());
        public Task<IReadOnlyCollection<TrackerSnapshot>> GetTrackersAsync(Guid torrentId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<TrackerSnapshot>>(Array.Empty<TrackerSnapshot>());
        public Task AddTrackerAsync(Guid torrentId, string trackerUrl, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}

