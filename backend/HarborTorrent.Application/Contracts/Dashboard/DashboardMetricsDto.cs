namespace HarborTorrent.Application.Contracts.Dashboard;

public class DashboardMetricsDto
{
    public int ActiveDownloads { get; init; }
    public int CompletedTorrents { get; init; }
    public long TotalDownloadedBytes { get; init; }
    public long TotalUploadedBytes { get; init; }
    public long TotalDownloadSpeedBytesPerSecond { get; init; }
    public long TotalUploadSpeedBytesPerSecond { get; init; }
}
