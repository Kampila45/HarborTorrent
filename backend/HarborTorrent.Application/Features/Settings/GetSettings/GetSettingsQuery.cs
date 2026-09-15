using HarborTorrent.Application.Abstractions.Persistence;
using HarborTorrent.Application.Contracts.Settings;
using MediatR;

namespace HarborTorrent.Application.Features.Settings.GetSettings;

public sealed record GetSettingsQuery : IRequest<SettingsDto>;

internal sealed class GetSettingsQueryHandler : IRequestHandler<GetSettingsQuery, SettingsDto>
{
    private readonly ISettingsRepository _settingsRepository;

    public GetSettingsQueryHandler(ISettingsRepository settingsRepository)
    {
        _settingsRepository = settingsRepository;
    }

    public async Task<SettingsDto> Handle(GetSettingsQuery request, CancellationToken cancellationToken)
    {
        var settings = await _settingsRepository.GetAsync(cancellationToken);

        return new SettingsDto(
            settings.MaxDownloadSpeedBytes,
            settings.MaxUploadSpeedBytes,
            settings.DefaultDownloadPath,
            settings.EnableDht,
            settings.EnablePex,
            settings.EnableLpd,
            settings.AnchorModeEnabled,
            settings.AnchorMaxDownloadSpeedBytes,
            settings.AnchorMaxUploadSpeedBytes
        );
    }
}
