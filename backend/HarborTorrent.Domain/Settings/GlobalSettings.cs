namespace HarborTorrent.Domain.Settings;

public sealed class GlobalSettings
{
    public int Id { get; set; } = 1;
    public long MaxDownloadSpeedBytes { get; set; } = 0; // 0 means unlimited
    public long MaxUploadSpeedBytes { get; set; } = 0; // 0 means unlimited
    public string DefaultDownloadPath { get; set; } = string.Empty;
    
    public bool EnableDht { get; set; } = true;
    public bool EnablePex { get; set; } = true;
    public bool EnableLpd { get; set; } = true;
    
    public bool AnchorModeEnabled { get; set; } = false;
    public long AnchorMaxDownloadSpeedBytes { get; set; } = 10 * 1024; // 10 KB/s
    public long AnchorMaxUploadSpeedBytes { get; set; } = 10 * 1024; // 10 KB/s
}
