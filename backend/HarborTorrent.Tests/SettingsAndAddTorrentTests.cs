using HarborTorrent.Application.Abstractions.Persistence;
using HarborTorrent.Application.Abstractions.Realtime;
using HarborTorrent.Application.Abstractions.Torrents;
using HarborTorrent.Application.Contracts.Torrents;
using HarborTorrent.Application.Features.Settings.UpdateSettings;
using HarborTorrent.Application.Features.Torrents.AddTorrent;
using HarborTorrent.Domain.Common;
using HarborTorrent.Domain.Settings;
using HarborTorrent.Domain.Torrents;
using HarborTorrent.Infrastructure.Runtime;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace HarborTorrent.Tests;

public sealed class SettingsAndAddTorrentTests
{
    [Fact]
    public void UpdateSettingsValidator_RejectsOutOfRangeSpeedsAndEmptyPath()
    {
        var validator = new UpdateSettingsCommandValidator();

        var invalid = new UpdateSettingsCommand(
            MaxDownloadSpeedBytes: -1,
            MaxUploadSpeedBytes: (long)int.MaxValue + 1,
            DefaultDownloadPath: "",
            EnableDht: true,
            EnablePex: true,
            EnableLpd: true,
            AnchorModeEnabled: false,
            AnchorMaxDownloadSpeedBytes: 1024,
            AnchorMaxUploadSpeedBytes: 1024);

        var result = validator.Validate(invalid);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateSettingsCommand.MaxDownloadSpeedBytes));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateSettingsCommand.MaxUploadSpeedBytes));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateSettingsCommand.DefaultDownloadPath));
    }

    [Fact]
    public void ToEngineRate_ClampsLargeAndNegativeValuesSafely()
    {
        Assert.Equal(0, MonoTorrentRuntimeCoordinator.ToEngineRate(-500));
        Assert.Equal(1024, MonoTorrentRuntimeCoordinator.ToEngineRate(1024));
        Assert.Equal(int.MaxValue, MonoTorrentRuntimeCoordinator.ToEngineRate((long)int.MaxValue + 999_999));
    }

    [Fact]
    public async Task AddTorrentHandler_UsesSavedDefaultDownloadPath_WhenSavePathOmitted()
    {
        var savedSettings = new GlobalSettings
        {
            DefaultDownloadPath = "/custom/saved/downloads"
        };

        Torrent? added = null;
        var torrentRepo = new CaptureTorrentRepository(t => added = t);
        var settingsRepo = new StubSettingsRepository(savedSettings);
        var metadataProvider = new StubMetadataProvider();
        var config = new ConfigurationBuilder().Build();

        var handler = new AddTorrentCommandHandler(
            torrentRepo,
            settingsRepo,
            new NoopEventPublisher(),
            metadataProvider,
            TimeProvider.System,
            config);

        var result = await handler.Handle(
            new AddTorrentCommand(
                Name: "Fedora",
                SavePath: null,
                MagnetLink: "magnet:?xt=urn:btih:0123456789abcdef0123456789abcdef01234567&dn=Fedora",
                TorrentFileName: null,
                TorrentFileContentBase64: null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(added);
        Assert.Equal("/custom/saved/downloads", added!.SavePath);
    }

    private sealed class StubSettingsRepository : ISettingsRepository
    {
        private readonly GlobalSettings _settings;
        public StubSettingsRepository(GlobalSettings settings) => _settings = settings;
        public Task<GlobalSettings> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(_settings);
        public Task UpdateAsync(GlobalSettings settings, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubMetadataProvider : ITorrentSourceMetadataProvider
    {
        public Result<TorrentSourceMetadata> GetMetadata(TorrentSource source) =>
            Result<TorrentSourceMetadata>.Success(new TorrentSourceMetadata("0123456789abcdef0123456789abcdef01234567"));
    }

    private sealed class NoopEventPublisher : ITorrentEventPublisher
    {
        public Task PublishAsync(TorrentEventType eventType, TorrentDto torrent, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task PublishRemovedAsync(Guid torrentId, string userId, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class CaptureTorrentRepository : ITorrentRepository
    {
        private readonly Action<Torrent> _onAdd;
        public CaptureTorrentRepository(Action<Torrent> onAdd) => _onAdd = onAdd;
        public Task AddAsync(Torrent torrent, CancellationToken cancellationToken) { _onAdd(torrent); return Task.CompletedTask; }
        public Task<Torrent?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<Torrent?>(null);
        public Task<bool> ExistsByInfoHashAsync(string infoHash, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<IReadOnlyCollection<Torrent>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<Torrent>>([]);
        public Task<IReadOnlyCollection<Torrent>> ListAllUnfilteredAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<Torrent>>([]);
        public Task<(IReadOnlyCollection<Torrent> Items, int TotalCount)> ListPaginatedAsync(string? status, string? query, int pageNumber, int pageSize, CancellationToken cancellationToken) =>
            Task.FromResult<(IReadOnlyCollection<Torrent>, int)>(([], 0));
        public Task<IReadOnlyCollection<Torrent>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<Torrent>>([]);
        public Task DeleteAsync(Torrent torrent, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
