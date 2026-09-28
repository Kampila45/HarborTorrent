using Microsoft.AspNetCore.SignalR;

namespace HarborTorrent.Infrastructure.Realtime;

/// <summary>
/// Rejects SignalR connection attempts that do not present the per-session launch
/// token. The token is expected as the <c>token</c> query string parameter on the
/// initial HTTP negotiation request, matching the pattern used by the frontend
/// SignalR client configuration.
/// </summary>
internal sealed class LaunchTokenHubFilter : IHubFilter
{
    private const string QueryParamName = "token";

    private readonly string _expectedToken;

    public LaunchTokenHubFilter(string expectedToken)
    {
        _expectedToken = expectedToken;
    }

    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        return await next(invocationContext);
    }

    public async Task OnConnectedAsync(
        HubLifetimeContext context,
        Func<HubLifetimeContext, Task> next)
    {
        var token = context.Context.GetHttpContext()?.Request.Query[QueryParamName].ToString();

        if (!string.Equals(token, _expectedToken, StringComparison.Ordinal))
        {
            context.Context.Abort();
            return;
        }

        await next(context);
    }

    public async Task OnDisconnectedAsync(
        HubLifetimeContext context,
        Exception? exception,
        Func<HubLifetimeContext, Exception?, Task> next)
    {
        await next(context, exception);
    }
}
