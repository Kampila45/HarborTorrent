using HarborTorrent.Application.Abstractions.Persistence;
using HarborTorrent.Application.Abstractions.Runtime;
using HarborTorrent.Domain.Settings;
using MediatR;

namespace HarborTorrent.Application.Features.Settings.UpdateSettings;

public sealed record UpdateSettingsCommand(
    long MaxDownloadSpeedBytes,
    long MaxUploadSpeedBytes,
    string DefaultDownloadPath,
    bool EnableDht,
    bool EnablePex,
    bool EnableLpd,
    bool AnchorModeEnabled,
    long AnchorMaxDownloadSpeedBytes,
    long AnchorMaxUploadSpeedBytes
) : IRequest;

internal sealed class UpdateSettingsCommandHandler : IRequestHandler<UpdateSettingsCommand>
{
    private readonly ISettingsRepository _settingsRepository;
    private readonly ITorrentRuntimeCoordinator _runtimeCoordinator;

    public UpdateSettingsCommandHandler(
        ISettingsRepository settingsRepository, 
        ITorrentRuntimeCoordinator runtimeCoordinator)
    {
        _settingsRepository = settingsRepository;
        _runtimeCoordinator = runtimeCoordinator;
    }

    public async Task<Unit> Handle(UpdateSettingsCommand request, CancellationToken cancellationToken)
    {
        var settings = await _settingsRepository.GetAsync(cancellationToken);

        settings.MaxDownloadSpeedBytes = request.MaxDownloadSpeedBytes;
        settings.MaxUploadSpeedBytes = request.MaxUploadSpeedBytes;
        settings.DefaultDownloadPath = request.DefaultDownloadPath;
        settings.EnableDht = request.EnableDht;
        settings.EnablePex = request.EnablePex;
        settings.EnableLpd = request.EnableLpd;
        settings.AnchorModeEnabled = request.AnchorModeEnabled;
        settings.AnchorMaxDownloadSpeedBytes = request.AnchorMaxDownloadSpeedBytes;
        settings.AnchorMaxUploadSpeedBytes = request.AnchorMaxUploadSpeedBytes;

        await _settingsRepository.UpdateAsync(settings, cancellationToken);

        // Apply settings live to the running torrent engine
        await _runtimeCoordinator.UpdateEngineSettingsAsync(settings, cancellationToken);

        return Unit.Value;
    }
}
