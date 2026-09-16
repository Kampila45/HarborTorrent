using HarborTorrent.Application.Abstractions.Persistence;
using HarborTorrent.Application.Abstractions.Runtime;
using HarborTorrent.Domain.Torrents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MonoTorrent;
using MonoTorrent.Client;
using System.Net;

namespace HarborTorrent.Infrastructure.Runtime;

public sealed class MonoTorrentRuntimeCoordinator : ITorrentRuntimeCoordinator, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<MonoTorrentRuntimeCoordinator> _logger;
    private readonly ClientEngine _engine;
    private readonly Dictionary<Guid, TorrentManager> _managers = new();
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

    public async Task UpdateEngineSettingsAsync(HarborTorrent.Domain.Settings.GlobalSettings settings, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var engineSettings = _engine.Settings;
            var builder = new EngineSettingsBuilder(engineSettings)
            {
                MaximumDownloadRate = settings.AnchorModeEnabled ? (int)settings.AnchorMaxDownloadSpeedBytes : (int)settings.MaxDownloadSpeedBytes,
                MaximumUploadRate = settings.AnchorModeEnabled ? (int)settings.AnchorMaxUploadSpeedBytes : (int)settings.MaxUploadSpeedBytes,
                AllowLocalPeerDiscovery = settings.EnableLpd,
                DhtEndPoint = settings.EnableDht ? new IPEndPoint(IPAddress.Any, 0) : null
            };

            await _engine.UpdateSettingsAsync(builder.ToSettings());
            await SaveEngineStateAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StartAsync(Guid torrentId, CancellationToken cancellationToken)
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
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task PauseAsync(Guid torrentId, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(CancellationToken.None);

        try
        {
            if (_managers.TryGetValue(torrentId, out var manager) && manager.State == TorrentState.Downloading)
            {
                await manager.PauseAsync();
                await SaveEngineStateAsync();
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StopAsync(Guid torrentId, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(CancellationToken.None);

        try
        {
            if (_managers.TryGetValue(torrentId, out var manager) && manager.State != TorrentState.Stopped)
            {
                await manager.StopAsync();
                await SaveEngineStateAsync();
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RemoveAsync(Guid torrentId, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(CancellationToken.None);

        try
        {
            if (!_managers.Remove(torrentId, out var manager))
            {
                return;
            }

            if (manager.State != TorrentState.Stopped)
            {
                await manager.StopAsync();
            }

            await _engine.RemoveAsync(manager);
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
                .Select(torrentId => CreateSnapshot(torrentId, _managers[torrentId]))
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
        await _gate.WaitAsync(cancellationToken);
        
        try
        {
            var manager = await EnsureManagerAsync(torrentId, cancellationToken);
            if (!manager.HasMetadata || manager.Files == null || fileIndex < 0 || fileIndex >= manager.Files.Count)
            {
                return null;
            }

            if (manager.State is TorrentState.Stopped or TorrentState.Paused or TorrentState.HashingPaused)
            {
                await manager.StartAsync();
                await SaveEngineStateAsync();
            }

            if (manager.StreamProvider == null)
            {
                return null;
            }

            return await manager.StreamProvider.CreateStreamAsync(manager.Files[fileIndex], cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
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

            return manager.TrackerManager.Tiers
                .SelectMany(tier => tier.Trackers)
                .Select(t => new TrackerSnapshot(
                    t.Uri.ToString(),
                    t.Status.ToString(),
                    0, 
                    0, 
                    t.WarningMessage,
                    t.FailureMessage,
                    DateTime.UtcNow - t.TimeSinceLastAnnounce // TimeSinceLastAnnounce is a TimeSpan
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
            _managers[torrentId] = restoredManager;
            return restoredManager;
        }

        var manager = await CreateManagerAsync(torrent, cancellationToken);
        _managers[torrentId] = manager;
        await SaveEngineStateAsync();

        _logger.LogInformation("Created MonoTorrent manager for torrent {TorrentId}", torrentId);

        return manager;
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

        var settings = new TorrentSettingsBuilder
        {
            CreateContainingDirectory = true,
            AllowDht = true,
            AllowPeerExchange = true
        }.ToSettings();

        if (torrent.Source.Kind == TorrentSourceKind.MagnetLink)
        {
            var magnetLink = MagnetLink.Parse(torrent.Source.Value);
            return await _engine.AddStreamingAsync(magnetLink, torrent.SavePath, settings);
        }

        var temporaryFilePath = Path.Combine(Path.GetTempPath(), $"{torrent.Id:N}.torrent");

        try
        {
            var bytes = Convert.FromBase64String(torrent.Source.Value);
            await File.WriteAllBytesAsync(temporaryFilePath, bytes, cancellationToken);
            return await _engine.AddStreamingAsync(temporaryFilePath, torrent.SavePath, settings);
        }
        finally
        {
            if (File.Exists(temporaryFilePath))
            {
                File.Delete(temporaryFilePath);
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

        return new ClientEngine(new EngineSettingsBuilder
        {
            AllowPortForwarding = true,
            MaximumConnections = 300,
            MaximumHalfOpenConnections = 20,
            AutoSaveLoadDhtCache = true,
            AutoSaveLoadFastResume = true,
            AutoSaveLoadMagnetLinkMetadata = true,
            CacheDirectory = cacheDirectory,
            DhtEndPoint = new IPEndPoint(IPAddress.Any, 0),
            ListenEndPoints = new Dictionary<string, IPEndPoint>
            {
                ["ipv4"] = new(IPAddress.Any, 0),
                ["ipv6"] = new(IPAddress.IPv6Any, 0)
            }
            // Note: MaxDownloadSpeed, MaxUploadSpeed, DHT, PEX, LPD will be applied 
            // separately via UpdateEngineSettingsAsync shortly after startup by the HostedService 
            // or they could be resolved here if ISettingsRepository is passed into the constructor, 
            // but fetching it in a synchronous constructor is an anti-pattern. 
            // Let the TorrentLifecycleHostedService initialize the settings on start.
        }.ToSettings());
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

    private static TorrentRuntimeSnapshot CreateSnapshot(Guid torrentId, TorrentManager manager)
    {
        var progress = decimal.Round((decimal)manager.Progress, 2);
        var downloadedBytes = manager.HasMetadata && manager.Files != null
            ? manager.Files.Sum(f => f.BytesDownloaded())
            : manager.Monitor.DataBytesReceived;
        var uploadedBytes = manager.Monitor.DataBytesSent;
        var ratio = downloadedBytes <= 0
            ? 0m
            : decimal.Round((decimal)uploadedBytes / downloadedBytes, 3);

        return new TorrentRuntimeSnapshot(
            torrentId,
            MapStatus(manager),
            progress,
            downloadedBytes,
            uploadedBytes,
            manager.Monitor.DownloadRate,
            manager.Monitor.UploadRate,
            ratio,
            CalculateEtaSeconds(manager, progress));
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