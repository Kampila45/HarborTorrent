namespace HarborTorrent.Application.Contracts.Settings;

public sealed record SettingsDto(
    long MaxDownloadSpeedBytes,
    long MaxUploadSpeedBytes,
    string DefaultDownloadPath,
    bool EnableDht,
    bool EnablePex,
    bool EnableLpd,
    bool AnchorModeEnabled,
    long AnchorMaxDownloadSpeedBytes,
    long AnchorMaxUploadSpeedBytes
);
