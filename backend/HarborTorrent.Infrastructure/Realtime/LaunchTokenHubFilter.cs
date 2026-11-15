using Microsoft.AspNetCore.SignalR;

namespace HarborTorrent.Infrastructure.Realtime;

/// <summary>
/// Validates that SignalR hub connections present the per-session launch token via
/// <c>X-Launch-Token</c>, <c>Authorization: Bearer</c>, or the <c>access_token</c> /
/// <c>token</c> query parameter (used by WebSocket and SSE transports).
/// </summary>
internal sealed class LaunchTokenHubFilter : IHubFilter
{
    private const string HeaderName = "X-Launch-Token";
    private const string BearerPrefix = "Bearer ";

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
        var httpRequest = context.Context.GetHttpContext()?.Request;

        if (httpRequest is null || !IsTokenValid(httpRequest))
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

    private bool IsTokenValid(Microsoft.AspNetCore.Http.HttpRequest request)
    {
        if (request.Headers.TryGetValue(HeaderName, out var headerValue) &&
            string.Equals(headerValue, _expectedToken, StringComparison.Ordinal))
        {
            return true;
        }

        if (request.Headers.TryGetValue("Authorization", out var authValue))
        {
            var auth = authValue.ToString();
            if (auth.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var bearer = auth[BearerPrefix.Length..];
                if (string.Equals(bearer, _expectedToken, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        var accessToken = request.Query["access_token"].ToString();
        if (!string.IsNullOrEmpty(accessToken) &&
            string.Equals(accessToken, _expectedToken, StringComparison.Ordinal))
        {
            return true;
        }

        var queryToken = request.Query["token"].ToString();
        return !string.IsNullOrEmpty(queryToken) &&
            string.Equals(queryToken, _expectedToken, StringComparison.Ordinal);
    }
}
