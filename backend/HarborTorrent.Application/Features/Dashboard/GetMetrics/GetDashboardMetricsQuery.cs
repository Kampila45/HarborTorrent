using HarborTorrent.Application.Abstractions.Persistence;
using HarborTorrent.Application.Contracts.Dashboard;
using HarborTorrent.Domain.Torrents;
using MediatR;

namespace HarborTorrent.Application.Features.Dashboard.GetMetrics;

public sealed record GetDashboardMetricsQuery : IRequest<DashboardMetricsDto>;

internal sealed class GetDashboardMetricsQueryHandler : IRequestHandler<GetDashboardMetricsQuery, DashboardMetricsDto>
{
    private readonly ITorrentRepository _torrentRepository;

    public GetDashboardMetricsQueryHandler(ITorrentRepository torrentRepository)
    {
        _torrentRepository = torrentRepository;
    }

    public async Task<DashboardMetricsDto> Handle(GetDashboardMetricsQuery request, CancellationToken cancellationToken)
    {
        var torrents = await _torrentRepository.ListAsync(cancellationToken);

        return new DashboardMetricsDto
        {
            ActiveDownloads = torrents.Count(t => t.Status == TorrentStatus.Downloading || t.Status == TorrentStatus.Seeding),
            CompletedTorrents = torrents.Count(t => t.Progress >= 100m),
            TotalDownloadedBytes = torrents.Sum(t => t.DownloadedBytes),
            TotalUploadedBytes = torrents.Sum(t => t.UploadedBytes),
            TotalDownloadSpeedBytesPerSecond = torrents.Sum(t => t.DownloadSpeedBytesPerSecond),
            TotalUploadSpeedBytesPerSecond = torrents.Sum(t => t.UploadSpeedBytesPerSecond)
        };
    }
}
