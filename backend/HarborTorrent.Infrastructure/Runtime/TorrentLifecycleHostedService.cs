using HarborTorrent.Application.Abstractions.Persistence;
using HarborTorrent.Application.Abstractions.Realtime;
using HarborTorrent.Application.Abstractions.Runtime;
using HarborTorrent.Application.Contracts.Torrents;
using HarborTorrent.Application.Features.Torrents.Common;
using HarborTorrent.Domain.Torrents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HarborTorrent.Infrastructure.Runtime;

internal sealed class TorrentLifecycleHostedService : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);

    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ITorrentRuntimeCoordinator _runtimeCoordinator;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TorrentLifecycleHostedService> _logger;
    private readonly IConfiguration _configuration;
    private bool _runtimeRestored;

    public TorrentLifecycleHostedService(
        IServiceScopeFactory serviceScopeFactory,
        ITorrentRuntimeCoordinator runtimeCoordinator,
        TimeProvider timeProvider,
        ILogger<TorrentLifecycleHostedService> logger,
        IConfiguration configuration)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _runtimeCoordinator = runtimeCoordinator;
        _timeProvider = timeProvider;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_runtimeRestored)
                {
                    await RestoreActiveTorrentsAsync(stoppingToken);
                    _runtimeRestored = true;
                }

                await AdvanceActiveTorrentsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to advance active torrents.");
            }

            await Task.Delay(TickInterval, stoppingToken);
        }
    }

    internal async Task AdvanceActiveTorrentsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITorrentRepository>();
        var eventPublisher = scope.ServiceProvider.GetRequiredService<ITorrentEventPublisher>();
        var activeTorrents = await repository.ListAllUnfilteredAsync(cancellationToken);
        var runningTorrents = activeTorrents
            .Where(torrent => torrent.Status == TorrentStatus.Downloading || torrent.Status == TorrentStatus.Seeding)
            .ToArray();

        if (runningTorrents.Length == 0)
        {
            return;
        }

        var snapshots = await _runtimeCoordinator.GetSnapshotsAsync(
            runningTorrents.Select(torrent => torrent.Id).ToArray(),
            cancellationToken);

        if (snapshots.Count == 0)
        {
            return;
        }

        var snapshotsById = snapshots.ToDictionary(snapshot => snapshot.TorrentId);
        var targetSeedRatio = _configuration.GetValue<decimal>("TorrentSettings:TargetSeedRatio", 1.0m);

        foreach (var torrent in runningTorrents)
        {
            if (!snapshotsById.TryGetValue(torrent.Id, out var snapshot))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(snapshot.ErrorMessage))
            {
                torrent.Fail(snapshot.ErrorMessage, _timeProvider.GetUtcNow());
                await eventPublisher.PublishAsync(TorrentEventType.Stopped, torrent.ToDto(), cancellationToken);
                continue;
            }

            if (snapshot.Status == TorrentStatus.Stopped)
            {
                var stopResult = torrent.Stop(_timeProvider.GetUtcNow());
                if (stopResult.IsSuccess)
                {
                    await eventPublisher.PublishAsync(TorrentEventType.Stopped, torrent.ToDto(), cancellationToken);
                }
                continue;
            }

            var wasSeeding = torrent.Status == TorrentStatus.Seeding;

            torrent.UpdateProgress(
                snapshot.Progress,
                snapshot.DownloadedBytes,
                snapshot.UploadedBytes,
                snapshot.DownloadSpeedBytesPerSecond,
                snapshot.UploadSpeedBytesPerSecond,
                snapshot.Ratio,
                snapshot.EtaSeconds,
                updatedAtUtc: _timeProvider.GetUtcNow());

            if (torrent.Status == TorrentStatus.Seeding && torrent.Ratio >= targetSeedRatio)
            {
                await _runtimeCoordinator.StopAsync(torrent.Id, cancellationToken);
                
                var completeResult = torrent.Complete(_timeProvider.GetUtcNow());
                if (completeResult.IsSuccess)
                {
                    await eventPublisher.PublishAsync(TorrentEventType.Completed, torrent.ToDto(), cancellationToken);
                }
                continue;
            }

            if (!wasSeeding && torrent.Status == TorrentStatus.Seeding)
            {
                // Just transitioned to Seeding, notify UI
                await eventPublisher.PublishAsync(TorrentEventType.ProgressUpdated, torrent.ToDto(), cancellationToken);
                continue;
            }

            // Always broadcast while running so the UI can show live download/upload speeds
            await eventPublisher.PublishAsync(TorrentEventType.ProgressUpdated, torrent.ToDto(), cancellationToken);
        }

        await repository.SaveChangesAsync(cancellationToken);
    }

    internal async Task RestoreActiveTorrentsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITorrentRepository>();
        var settingsRepository = scope.ServiceProvider.GetRequiredService<ISettingsRepository>();

        // Initialize MonoTorrent engine settings with the user's saved preferences
        try
        {
            var settings = await settingsRepository.GetAsync(cancellationToken);
            await _runtimeCoordinator.UpdateEngineSettingsAsync(settings, cancellationToken);
            _logger.LogInformation("Initialized MonoTorrent engine settings from DB.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize MonoTorrent engine settings on startup.");
        }

        var torrents = await repository.ListAllUnfilteredAsync(cancellationToken);

        foreach (var torrent in torrents.Where(torrent =>
            torrent.Status is TorrentStatus.Downloading or TorrentStatus.Seeding or TorrentStatus.Paused))
        {
            try
            {
                var startResult = await _runtimeCoordinator.StartAsync(torrent.Id, cancellationToken);
                if (startResult.IsFailure)
                {
                    _logger.LogWarning(
                        "Failed to restore torrent {TorrentId}: {Errors}",
                        torrent.Id,
                        string.Join("; ", startResult.Errors.Select(e => e.Message)));
                    continue;
                }

                // Paused torrents must be re-registered with the engine (via StartAsync),
                // then immediately paused again to restore their intended state.
                if (torrent.Status == TorrentStatus.Paused)
                {
                    var pauseResult = await _runtimeCoordinator.PauseAsync(torrent.Id, cancellationToken);
                    if (pauseResult.IsFailure)
                    {
                        _logger.LogWarning(
                            "Failed to pause restored torrent {TorrentId}: {Errors}",
                            torrent.Id,
                            string.Join("; ", pauseResult.Errors.Select(e => e.Message)));
                    }
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to restore runtime state for torrent {TorrentId}", torrent.Id);
            }
        }
    }
}