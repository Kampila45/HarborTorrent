using HarborTorrent.Application.Abstractions.Persistence;
using HarborTorrent.Application.Abstractions.Runtime;
using HarborTorrent.Domain.Common;
using DomainError = HarborTorrent.Domain.Common.Error;
using HarborTorrent.Domain.Torrents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MonoTorrent;
using MonoTorrent.Client;
using MonoTorrent.PiecePicking;
using MonoTorrent.PieceWriter;
using MonoTorrent.Streaming;
using System.Net;

namespace HarborTorrent.Infrastructure.Runtime;

public sealed class MonoTorrentRuntimeCoordinator : ITorrentRuntimeCoordinator, IDisposable
{
    internal const int DefaultEngineMaxConnections = 500;
    internal const int DefaultMaxHalfOpenConnections = 60;
    internal const int DefaultDiskCacheBytes = 32 * 1024 * 1024;
    internal const int DefaultTorrentMaxConnections = 200;
    internal const int DefaultTorrentUploadSlots = 12;
    internal static readonly TimeSpan DefaultStaleRequestTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(2);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<MonoTorrentRuntimeCoordinator> _logger;
    private readonly ClientEngine _engine;
    private readonly Dictionary<Guid, TorrentManager> _managers = new();
    private readonly Dictionary<Guid, long> _sessionUploadedBaselines = new();
    private readonly string _engineStatePath;

    public MonoTorrentRuntimeCoordinator(
        IServiceScopeFactory serviceScopeFactory,
        IConfiguration configuration,
        ILogger<MonoTorrentRuntimeCoordinator> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;

        var runtimeDataPath = configuration["HarborTorrent:RuntimeDataPath"];
        var baseRuntimePath = string.IsNullOrWhiteSpace(runtimeDataPath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HarborTorrent",
                "runtime")
            : Path.GetFullPath(runtimeDataPath);
        var cacheDirectory = Path.Combine(baseRuntimePath, "torrent-cache");
        _engineStatePath = Path.Combine(baseRuntimePath, "engine-state.dat");

        Directory.CreateDirectory(baseRuntimePath);
        Directory.CreateDirectory(cacheDirectory);

        _engine = CreateOrRestoreEngine(cacheDirectory, _engineStatePath);
    }

    internal EngineSettings CurrentEngineSettings => _engine.Settings;

    internal IReadOnlyList<int> GetTrackerTierSizes(Guid torrentId) =>
        _managers.TryGetValue(torrentId, out var manager)
            ? manager.TrackerManager.Tiers.Select(t => t.Trackers.Count).ToArray()
            : [];

    public async Task UpdateEngineSettingsAsync(HarborTorrent.Domain.Settings.GlobalSettings settings, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var downloadLimit = settings.AnchorModeEnabled
                ? settings.AnchorMaxDownloadSpeedBytes
                : settings.MaxDownloadSpeedBytes;
            var uploadLimit = settings.AnchorModeEnabled
                ? settings.AnchorMaxUploadSpeedBytes
                : settings.MaxUploadSpeedBytes;

            var engineSettings = _engine.Settings;
            var builder = new EngineSettingsBuilder(engineSettings)
            {
                MaximumDownloadRate = ToEngineRate(downloadLimit),
                MaximumUploadRate = ToEngineRate(uploadLimit),
                AllowLocalPeerDiscovery = settings.EnableLpd,
                DhtEndPoint = settings.EnableDht
                    ? (engineSettings.DhtEndPoint ?? new IPEndPoint(IPAddress.Any, 0))
                    : null
            };
            ApplyPerformanceDefaults(builder);

            await _engine.UpdateSettingsAsync(builder.ToSettings());

            foreach (var manager in _managers.Values)
            {
                var managerSettings = BuildTorrentSettings(manager.Settings, settings.EnableDht, settings.EnablePex);
                await manager.UpdateSettingsAsync(managerSettings);
            }

            await SaveEngineStateAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    internal static int ToEngineRate(long bytesPerSecond)
    {
        return (int)Math.Clamp(bytesPerSecond, 0, int.MaxValue);
    }

    public async Task<Result> StartAsync(Guid torrentId, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(CancellationToken.None);

        try
        {
            var manager = await EnsureManagerAsync(torrentId, cancellationToken);

            if (manager.State is TorrentState.Stopped or TorrentState.Paused or TorrentState.HashingPaused)
            {
                await manager.StartAsync();
                await SaveEngineStateAsync();
            }

            if (manager.State == TorrentState.Error)
            {
                return Result.Failure(DomainError.Conflict("Torrent engine failed to start transfer."));
            }

            return Result.Success();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start torrent {TorrentId} in engine.", torrentId);
            return Result.Failure(DomainError.Conflict($"Torrent engine failed to start transfer: {ex.Message}"));
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<Result> PauseAsync(Guid torrentId, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(CancellationToken.None);

        try
        {
            if (!_managers.TryGetValue(torrentId, out var manager))
            {
                return Result.Success();
            }

            if (manager.State is TorrentState.Downloading or TorrentState.Seeding or TorrentState.Hashing or TorrentState.Metadata)
            {
                await manager.PauseAsync();
                await SaveEngineStateAsync();
                return Result.Success();
            }

            if (manager.State is TorrentState.Paused or TorrentState.HashingPaused or TorrentState.Stopped)
            {
                return Result.Success();
            }

            return Result.Failure(DomainError.Conflict($"Cannot pause torrent while engine state is '{manager.State}'."));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pause torrent {TorrentId} in engine.", torrentId);
            return Result.Failure(DomainError.Conflict($"Torrent engine failed to pause transfer: {ex.Message}"));
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<Result> StopAsync(Guid torrentId, CancellationToken cancellationToken)
    {
        TorrentManager? manager;
        await _gate.WaitAsync(CancellationToken.None);
        try
        {
            if (!_managers.TryGetValue(torrentId, out manager))
            {
                return Result.Success();
            }
        }
        finally
        {
            _gate.Release();
        }

        try
        {
            if (manager.State != TorrentState.Stopped)
            {
                // Cap tracker stop-announce wait and keep _gate free so unrelated actions stay responsive.
                await manager.StopAsync(StopTimeout);

                await _gate.WaitAsync(CancellationToken.None);
                try
                {
                    await SaveEngineStateAsync();
                }
                finally
                {
                    _gate.Release();
                }
            }

            return Result.Success();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop torrent {TorrentId} in engine.", torrentId);
            return Result.Failure(DomainError.Conflict($"Torrent engine failed to stop transfer: {ex.Message}"));
        }
    }

    public async Task RemoveAsync(Guid torrentId, CancellationToken cancellationToken)
    {
        TorrentManager? manager;

        // Remove the manager from the live dictionary immediately under the lock so
        // no other operation can observe it after removal is requested. The lock is
        // released before stopping the manager and removing it from the engine, because
        // tracker shutdown can take several seconds and must not starve other operations.
        await _gate.WaitAsync(CancellationToken.None);
        try
        {
            _sessionUploadedBaselines.Remove(torrentId);
            if (!_managers.Remove(torrentId, out manager))
            {
                return;
            }
        }
        finally
        {
            _gate.Release();
        }

        // Stop and remove outside the shared semaphore to avoid blocking callers.
        if (manager.State != TorrentState.Stopped)
        {
            await manager.StopAsync(StopTimeout);
        }

        await _engine.RemoveAsync(manager);

        // Re-acquire briefly only to persist engine state.
        await _gate.WaitAsync(CancellationToken.None);
        try
        {
            await SaveEngineStateAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyCollection<TorrentRuntimeSnapshot>> GetSnapshotsAsync(
        IReadOnlyCollection<Guid> torrentIds,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            return torrentIds
                .Where(torrentId => _managers.ContainsKey(torrentId))
                .Select(torrentId => CreateSnapshot(
                    torrentId,
                    _managers[torrentId],
                    _sessionUploadedBaselines.GetValueOrDefault(torrentId)))
                .ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyCollection<TorrentFileSnapshot>> GetFilesAsync(Guid torrentId, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            var manager = await EnsureManagerAsync(torrentId, cancellationToken);

            if (!manager.HasMetadata || manager.Files == null)
            {
                return Array.Empty<TorrentFileSnapshot>();
            }

            return manager.Files.Select((f, idx) => new TorrentFileSnapshot(
                idx,
                f.Path,
                f.Length,
                f.BytesDownloaded(),
                f.Priority.ToString()
            )).ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get files for torrent {TorrentId}", torrentId);
            return Array.Empty<TorrentFileSnapshot>();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task UpdateFilesPriorityAsync(Guid torrentId, IReadOnlyCollection<HarborTorrent.Application.Contracts.Torrents.FilePriorityUpdateDto> updates, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(CancellationToken.None);

        try
        {
            var manager = await EnsureManagerAsync(torrentId, cancellationToken);

            if (!manager.HasMetadata || manager.Files == null)
            {
                return;
            }

            var hasChanges = false;
            foreach (var update in updates)
            {
                if (update.FileIndex >= 0 && update.FileIndex < manager.Files.Count)
                {
                    if (Enum.TryParse<Priority>(update.Priority, true, out var newPriority))
                    {
                        await manager.SetFilePriorityAsync(manager.Files[update.FileIndex], newPriority);
                        hasChanges = true;
                    }
                }
            }

            if (hasChanges)
            {
                await SaveEngineStateAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update file priorities for torrent {TorrentId}", torrentId);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<Stream?> CreateStreamAsync(Guid torrentId, int fileIndex, CancellationToken cancellationToken)
    {
        StreamProvider? streamProvider;
        ITorrentManagerFile? targetFile;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var manager = await EnsureManagerAsync(torrentId, cancellationToken);
            if (!manager.HasMetadata || manager.Files == null || fileIndex < 0 || fileIndex >= manager.Files.Count)
            {
                return null;
            }

            if (manager.StreamProvider == null)
            {
                manager = await UpgradeToStreamingManagerAsync(torrentId, manager, cancellationToken);
            }
            else if (manager.State is TorrentState.Stopped or TorrentState.Paused or TorrentState.HashingPaused)
            {
                await manager.StartAsync();
                await SaveEngineStateAsync();
            }

            if (manager.StreamProvider == null)
            {
                return null;
            }

            streamProvider = manager.StreamProvider;
            targetFile = manager.Files![fileIndex];
        }
        finally
        {
            _gate.Release();
        }

        // Create the stream outside the shared semaphore so waiting for piece/header data
        // does not stall unrelated torrent operations.
        return await streamProvider.CreateStreamAsync(targetFile, cancellationToken);
    }

    public async Task<IReadOnlyCollection<PeerSnapshot>> GetPeersAsync(Guid torrentId, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        
        try
        {
            var manager = await EnsureManagerAsync(torrentId, cancellationToken);
            var peers = await manager.GetPeersAsync();
            
            return peers.Select(p => new PeerSnapshot(
                p.Uri.ToString(),
                p.ClientApp.Client.ToString(), // p.ClientApp is a struct (Software), Client is usually an enum or struct
                p.Monitor.DownloadRate,
                p.Monitor.UploadRate,
                p.AmChoking,
                p.AmInterested,
                p.IsChoking,
                p.IsInterested
            )).ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get peers for torrent {TorrentId}", torrentId);
            return Array.Empty<PeerSnapshot>();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyCollection<TrackerSnapshot>> GetTrackersAsync(Guid torrentId, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        
        try
        {
            var manager = await EnsureManagerAsync(torrentId, cancellationToken);
            if (manager.TrackerManager == null || manager.TrackerManager.Tiers == null)
            {
                return Array.Empty<TrackerSnapshot>();
            }

            var nowUtc = DateTime.UtcNow;
            return manager.TrackerManager.Tiers
                .SelectMany(tier => tier.Trackers)
                .Select(t => new TrackerSnapshot(
                    t.Uri.ToString(),
                    t.Status.ToString(),
                    0, 
                    0, 
                    t.WarningMessage,
                    t.FailureMessage,
                    ResolveLastAnnounceUtc(t.TimeSinceLastAnnounce, nowUtc)
                )).ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get trackers for torrent {TorrentId}", torrentId);
            return Array.Empty<TrackerSnapshot>();
        }
        finally
        {
            _gate.Release();
        }
    }

    internal static DateTime? ResolveLastAnnounceUtc(TimeSpan timeSinceLastAnnounce, DateTime utcNow)
    {
        // MonoTorrent initializes TimeSinceLastAnnounce to TimeSpan.MaxValue before a tracker's first announce.
        if (timeSinceLastAnnounce < TimeSpan.Zero || timeSinceLastAnnounce >= TimeSpan.FromDays(36500))
        {
            return null;
        }

        return utcNow.Subtract(timeSinceLastAnnounce);
    }

    public async Task AddTrackerAsync(Guid torrentId, string trackerUrl, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(CancellationToken.None);
        
        try
        {
            var manager = await EnsureManagerAsync(torrentId, cancellationToken);
            
            if (manager.TrackerManager != null)
            {
                // In MonoTorrent v3, adding a tracker can be done asynchronously on the manager
                await manager.TrackerManager.AddTrackerAsync(new Uri(trackerUrl));
                await SaveEngineStateAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to add tracker {TrackerUrl} to torrent {TorrentId}", trackerUrl, torrentId);
            throw; // Allow exception to bubble up so the API returns 500 or 400
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<TorrentManager> EnsureManagerAsync(Guid torrentId, CancellationToken cancellationToken)
    {
        if (_managers.TryGetValue(torrentId, out var existingManager))
        {
            return existingManager;
        }

        using var scope = _serviceScopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITorrentRepository>();
        var torrent = await repository.GetByIdAsync(torrentId, cancellationToken)
            ?? throw new InvalidOperationException($"Torrent '{torrentId}' could not be loaded for runtime start.");

        var restoredManager = TryAttachRestoredManager(torrent);

        if (restoredManager is not null)
        {
            // Ensure restored managers from prior app versions also use rarest-first piece picking until streamed.
            if (restoredManager.StreamProvider is not null)
            {
                await restoredManager.ChangePickerAsync(new StandardPieceRequester(new PieceRequesterSettings()));
            }

            await ApplySavedManagerSettingsAsync(restoredManager, cancellationToken);
            if (torrent.Source.Kind == TorrentSourceKind.MagnetLink)
            {
                await EnsureSeparateTrackerTiersAsync(restoredManager, torrent);
            }

            _managers[torrentId] = restoredManager;
            _sessionUploadedBaselines[torrentId] = torrent.UploadedBytes;
            return restoredManager;
        }

        var manager = await CreateManagerAsync(torrent, cancellationToken);
        _managers[torrentId] = manager;
        _sessionUploadedBaselines[torrentId] = torrent.UploadedBytes;
        await SaveEngineStateAsync();

        _logger.LogInformation("Created MonoTorrent manager for torrent {TorrentId}", torrentId);

        return manager;
    }

    private async Task ApplySavedManagerSettingsAsync(TorrentManager manager, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var settingsRepo = scope.ServiceProvider.GetRequiredService<ISettingsRepository>();
            var savedSettings = await settingsRepo.GetAsync(cancellationToken);
            var updatedSettings = BuildTorrentSettings(manager.Settings, savedSettings.EnableDht, savedSettings.EnablePex);
            await manager.UpdateSettingsAsync(updatedSettings);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not apply saved settings to restored torrent manager.");
        }
    }

    private TorrentManager? TryAttachRestoredManager(HarborTorrent.Domain.Torrents.Torrent torrent)
    {
        var torrentKey = GetTorrentKey(torrent);

        if (torrentKey is null)
        {
            return null;
        }

        var attachedManagers = _managers.Values.ToHashSet();

        return _engine.Torrents
            .Where(manager => !attachedManagers.Contains(manager))
            .FirstOrDefault(manager => string.Equals(GetManagerKey(manager), torrentKey, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<TorrentManager> CreateManagerAsync(HarborTorrent.Domain.Torrents.Torrent torrent, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(torrent.SavePath);

        // Read user preferences so per-manager settings (PEX, DHT) match what is configured.
        // These must be set at creation time because MonoTorrent applies them per-manager,
        // not globally. Fall back to safe defaults if the repository call fails.
        bool allowPex = true;
        bool allowDht = true;
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var settingsRepo = scope.ServiceProvider.GetRequiredService<HarborTorrent.Application.Abstractions.Persistence.ISettingsRepository>();
            var savedSettings = await settingsRepo.GetAsync(cancellationToken);
            allowPex = savedSettings.EnablePex;
            allowDht = savedSettings.EnableDht;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read settings for torrent manager creation; using defaults.");
        }

        var settings = BuildTorrentSettings(baseSettings: null, allowDht, allowPex);

        if (torrent.Source.Kind == TorrentSourceKind.MagnetLink)
        {
            var magnetLink = MagnetLink.Parse(torrent.Source.Value);
            // Add without trackers so each tracker can be registered in its own tier and announced in parallel.
            var bareMagnet = new MagnetLink(magnetLink.InfoHashes, magnetLink.Name);
            var manager = await _engine.AddAsync(bareMagnet, torrent.SavePath, settings);
            await EnsureSeparateTrackerTiersAsync(manager, torrent);
            return manager;
        }

        var temporaryFilePath = Path.Combine(Path.GetTempPath(), $"{torrent.Id:N}.torrent");

        try
        {
            var bytes = Convert.FromBase64String(torrent.Source.Value);
            await File.WriteAllBytesAsync(temporaryFilePath, bytes, cancellationToken);
            return await _engine.AddAsync(temporaryFilePath, torrent.SavePath, settings);
        }
        finally
        {
            if (File.Exists(temporaryFilePath))
            {
                File.Delete(temporaryFilePath);
            }
        }
    }

    private async Task<TorrentManager> UpgradeToStreamingManagerAsync(
        Guid torrentId,
        TorrentManager manager,
        CancellationToken cancellationToken)
    {
        var loadedTorrent = manager.Torrent;
        if (loadedTorrent is null)
        {
            return manager;
        }

        var priorities = manager.Files?.Select(f => f.Priority).ToArray() ?? [];
        var savePath = manager.SavePath;
        var settings = manager.Settings;
        _sessionUploadedBaselines[torrentId] =
            _sessionUploadedBaselines.GetValueOrDefault(torrentId) + manager.Monitor.DataBytesSent;

        if (manager.State != TorrentState.Stopped)
        {
            await manager.StopAsync(StopTimeout);
        }

        await _engine.RemoveAsync(manager);

        var streamingManager = await _engine.AddStreamingAsync(loadedTorrent, savePath, settings);
        _managers[torrentId] = streamingManager;

        if (streamingManager.Files is not null)
        {
            for (var i = 0; i < Math.Min(priorities.Length, streamingManager.Files.Count); i++)
            {
                if (priorities[i] != Priority.Normal)
                {
                    await streamingManager.SetFilePriorityAsync(streamingManager.Files[i], priorities[i]);
                }
            }
        }

        await streamingManager.StartAsync();
        await SaveEngineStateAsync();

        return streamingManager;
    }

    private static EngineSettingsBuilder ApplyPerformanceDefaults(EngineSettingsBuilder builder)
    {
        builder.AllowPortForwarding = true;
        builder.AllowHaveSuppression = true;
        builder.MaximumConnections = DefaultEngineMaxConnections;
        builder.MaximumHalfOpenConnections = DefaultMaxHalfOpenConnections;
        builder.DiskCacheBytes = DefaultDiskCacheBytes;
        builder.DiskCachePolicy = CachePolicy.ReadsAndWrites;
        builder.StaleRequestTimeout = DefaultStaleRequestTimeout;
        return builder;
    }

    private static TorrentSettings BuildTorrentSettings(TorrentSettings? baseSettings, bool allowDht, bool allowPex)
    {
        var builder = baseSettings is null
            ? new TorrentSettingsBuilder()
            : new TorrentSettingsBuilder(baseSettings);

        builder.CreateContainingDirectory = true;
        builder.AllowDht = allowDht;
        builder.AllowPeerExchange = allowPex;
        builder.MaximumConnections = DefaultTorrentMaxConnections;
        builder.UploadSlots = DefaultTorrentUploadSlots;

        return builder.ToSettings();
    }

    internal async Task EnsureSeparateTrackerTiersAsync(TorrentManager manager, HarborTorrent.Domain.Torrents.Torrent torrent)
    {
        if (manager.TrackerManager is null)
        {
            return;
        }

        var existingTrackers = manager.TrackerManager.Tiers
            .SelectMany(tier => tier.Trackers)
            .ToArray();
        var hasMultiTrackerTier = manager.TrackerManager.Tiers.Any(tier => tier.Trackers.Count > 1);

        var desiredUrls = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddCandidate(string? rawUrl)
        {
            if (string.IsNullOrWhiteSpace(rawUrl))
            {
                return;
            }

            var normalized = rawUrl.Trim();
            if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri) && seen.Add(uri.ToString()))
            {
                desiredUrls.Add(uri.ToString());
            }
        }

        if (torrent.Source.Kind == TorrentSourceKind.MagnetLink)
        {
            try
            {
                var parsed = MagnetLink.Parse(torrent.Source.Value);
                foreach (var url in parsed.AnnounceUrls)
                {
                    AddCandidate(url);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not parse magnet announce URLs for torrent {TorrentId}.", torrent.Id);
            }

            foreach (var defaultTracker in TorrentSource.DefaultTrackers)
            {
                AddCandidate(defaultTracker);
            }
        }

        foreach (var tracker in existingTrackers)
        {
            AddCandidate(tracker.Uri.ToString());
        }

        var existingUrls = new HashSet<string>(existingTrackers.Select(t => t.Uri.ToString()), StringComparer.OrdinalIgnoreCase);
        if (!hasMultiTrackerTier && desiredUrls.All(existingUrls.Contains))
        {
            return;
        }

        if (hasMultiTrackerTier)
        {
            // MonoTorrent only announces one tracker per tier; rebuild into 1 tier per tracker.
            foreach (var tracker in existingTrackers)
            {
                await manager.TrackerManager.RemoveTrackerAsync(tracker);
            }

            existingUrls.Clear();
        }

        foreach (var url in desiredUrls)
        {
            if (existingUrls.Contains(url))
            {
                continue;
            }

            try
            {
                await manager.TrackerManager.AddTrackerAsync(new Uri(url));
                existingUrls.Add(url);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Skipping unsupported tracker URI '{TrackerUrl}' for torrent {TorrentId}.", url, torrent.Id);
            }
        }
    }

    private static string? GetTorrentKey(HarborTorrent.Domain.Torrents.Torrent torrent)
    {
        if (torrent.Source.Kind == TorrentSourceKind.MagnetLink)
        {
            return ToInfoHashKey(MagnetLink.Parse(torrent.Source.Value).InfoHashes);
        }

        var bytes = Convert.FromBase64String(torrent.Source.Value);
        return MonoTorrent.Torrent.TryLoad(bytes, out var monoTorrent)
            ? ToInfoHashKey(monoTorrent.InfoHashes)
            : null;
    }

    private static string GetManagerKey(TorrentManager manager)
    {
        return ToInfoHashKey(manager.InfoHashes);
    }

    private static string ToInfoHashKey(InfoHashes infoHashes)
    {
        return infoHashes.V1?.ToHex()
            ?? infoHashes.V2?.ToHex()
            ?? throw new InvalidOperationException("The torrent source did not contain a supported info hash.");
    }

    private ClientEngine CreateOrRestoreEngine(string cacheDirectory, string engineStatePath)
    {
        if (File.Exists(engineStatePath))
        {
            try
            {
                return ClientEngine.RestoreStateAsync(engineStatePath).GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Failed to restore MonoTorrent engine state from {EngineStatePath}. Falling back to a new engine.", engineStatePath);
            }
        }

        var listenEndPoints = new Dictionary<string, IPEndPoint>
        {
            ["ipv4"] = new(IPAddress.Any, 0)
        };
        if (System.Net.Sockets.Socket.OSSupportsIPv6)
        {
            listenEndPoints["ipv6"] = new(IPAddress.IPv6Any, 0);
        }

        var engineBuilder = ApplyPerformanceDefaults(new EngineSettingsBuilder
        {
            AutoSaveLoadDhtCache = true,
            AutoSaveLoadFastResume = true,
            AutoSaveLoadMagnetLinkMetadata = true,
            CacheDirectory = cacheDirectory,
            DhtEndPoint = new IPEndPoint(IPAddress.Any, 0),
            ListenEndPoints = listenEndPoints
            // Note: MaxDownloadSpeed, MaxUploadSpeed, DHT, PEX, LPD will be applied 
            // separately via UpdateEngineSettingsAsync shortly after startup by the HostedService 
            // or they could be resolved here if ISettingsRepository is passed into the constructor, 
            // but fetching it in a synchronous constructor is an anti-pattern. 
            // Let the TorrentLifecycleHostedService initialize the settings on start.
        });

        return new ClientEngine(engineBuilder.ToSettings());
    }

    private async Task SaveEngineStateAsync()
    {
        var directory = Path.GetDirectoryName(_engineStatePath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await _engine.SaveStateAsync(_engineStatePath);
    }

    private static TorrentRuntimeSnapshot CreateSnapshot(Guid torrentId, TorrentManager manager, long uploadedBaseline)
    {
        var progress = decimal.Round((decimal)manager.Progress, 2);
        var downloadedBytes = manager.HasMetadata && manager.Files != null
            ? manager.Files.Sum(f => f.BytesDownloaded())
            : manager.Monitor.DataBytesReceived;
        var uploadedBytes = uploadedBaseline + manager.Monitor.DataBytesSent;
        var ratio = downloadedBytes <= 0
            ? 0m
            : decimal.Round((decimal)uploadedBytes / downloadedBytes, 3);
        var status = MapStatus(manager);
        var errorMessage = manager.State == TorrentState.Error
            ? manager.Error?.Exception?.Message ?? manager.Error?.Reason.ToString() ?? "Torrent engine reported an error."
            : null;

        return new TorrentRuntimeSnapshot(
            torrentId,
            status,
            progress,
            downloadedBytes,
            uploadedBytes,
            manager.Monitor.DownloadRate,
            manager.Monitor.UploadRate,
            ratio,
            CalculateEtaSeconds(manager, progress),
            errorMessage);
    }

    private static TorrentStatus MapStatus(TorrentManager manager)
    {
        if (manager.Complete || manager.State == TorrentState.Seeding)
        {
            return TorrentStatus.Completed;
        }

        return manager.State switch
        {
            TorrentState.Paused or TorrentState.HashingPaused => TorrentStatus.Paused,
            TorrentState.Stopped or TorrentState.Error => TorrentStatus.Stopped,
            _ => TorrentStatus.Downloading
        };
    }

    private static long? CalculateEtaSeconds(TorrentManager manager, decimal progress)
    {
        if (!manager.HasMetadata || manager.Monitor.DownloadRate <= 0 || progress >= 100m)
        {
            return progress >= 100m ? 0 : null;
        }

        var totalBytes = manager.Files.Sum(file => file.Length);

        if (totalBytes <= 0)
        {
            return null;
        }

        var remainingBytes = (long)Math.Max(0, Math.Ceiling(totalBytes * (100m - progress) / 100m));

        return remainingBytes == 0
            ? 0
            : remainingBytes / Math.Max(1, manager.Monitor.DownloadRate);
    }

    public void Dispose()
    {
        try
        {
            var stopTasks = _managers.Values
                .Where(m => m.State != TorrentState.Stopped)
                .Select(m => m.StopAsync(StopTimeout))
                .ToArray();

            if (stopTasks.Length > 0)
            {
                Task.WhenAll(stopTasks).WaitAsync(TimeSpan.FromSeconds(4)).GetAwaiter().GetResult();
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Timed out or failed while stopping torrent managers during shutdown.");
        }

        try
        {
            SaveEngineStateAsync().GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to save MonoTorrent engine state during shutdown.");
        }

        _engine.Dispose();
        _gate.Dispose();
    }
}