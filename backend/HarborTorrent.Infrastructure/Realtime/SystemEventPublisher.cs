using HarborTorrent.Application.Abstractions.Realtime;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace HarborTorrent.Infrastructure.Realtime;

internal sealed class SystemEventPublisher : ISystemEventPublisher
{
    private readonly IHubContext<SystemHub> _hubContext;

    public SystemEventPublisher(IHubContext<SystemHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task BroadcastUpdateProgressAsync(int progressPercentage)
    {
        await _hubContext.Clients.All.SendAsync("UpdateProgress", progressPercentage);
    }

    public async Task BroadcastUpdateCompleteAsync()
    {
        await _hubContext.Clients.All.SendAsync("UpdateComplete");
    }
}
