using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace HarborTorrent.Infrastructure.Realtime;

public sealed class SystemHub : Hub
{
    private readonly ILogger<SystemHub> _logger;

    public SystemHub(ILogger<SystemHub> logger)
    {
        _logger = logger;
    }

    public override Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected to SystemHub: {ConnectionId}", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected from SystemHub: {ConnectionId}", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
