using HarborTorrent.Api.Security;

namespace HarborTorrent.Api.Middleware;

/// <summary>
/// Rejects any request that does not present the per-session launch token (or a valid
/// scoped media token on the streaming endpoint).
///
/// Accepted token locations:
/// <list type="bullet">
///   <item><c>X-Launch-Token</c> request header — used by the HTTP API client on all routes.</item>
///   <item><c>Authorization: Bearer &lt;token&gt;</c> header — used by the SignalR JS client on
///   negotiation and HTTP transports.</item>
///   <item><c>access_token</c> query parameter on <c>/hubs/*</c> only — used by browser WebSocket
///   and SSE transports where custom headers cannot be set.</item>
///   <item><c>mediaToken</c> query parameter on <c>/api/v1/files/stream</c> only — a short-lived
///   HMAC-scoped token bound to a single torrent and file index.</item>
/// </list>
/// </summary>
/// <remarks>
/// The <c>/health</c> endpoint is exempt so that the frontend readiness check can
/// poll it before the WebView has retrieved its token via the Tauri invoke bridge.
/// </remarks>
internal sealed class LaunchTokenAuthenticationMiddleware
{
    internal const string HeaderName = "X-Launch-Token";
    private const string BearerPrefix = "Bearer ";

    private static readonly string[] ExemptPaths = ["/health"];

    private readonly RequestDelegate _next;
    private readonly string _expectedToken;
    private readonly StreamTokenService _streamTokenService;

    public LaunchTokenAuthenticationMiddleware(
        RequestDelegate next,
        string expectedToken,
        StreamTokenService streamTokenService)
    {
        _next = next;
        _expectedToken = expectedToken;
        _streamTokenService = streamTokenService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (IsExempt(context.Request.Path))
        {
            await _next(context);
            return;
        }

        if (IsAuthenticated(context.Request))
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            success = false,
            errors = new[] { new { code = "unauthorized", message = "Invalid or missing launch token." } }
        });
    }

    private bool IsAuthenticated(HttpRequest request)
    {
        // Check the custom header used by the HTTP API client.
        if (request.Headers.TryGetValue(HeaderName, out var headerValue) &&
            string.Equals(headerValue, _expectedToken, StringComparison.Ordinal))
        {
            return true;
        }

        // Check the Authorization: Bearer header used by SignalR negotiation/HTTP transports.
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

        // Browser WebSockets/SSE cannot send custom headers, so SignalR passes access_token
        // in the query string on hub endpoints only.
        if (request.Path.StartsWithSegments("/hubs", StringComparison.OrdinalIgnoreCase))
        {
            var accessToken = request.Query["access_token"].ToString();
            if (!string.IsNullOrEmpty(accessToken) &&
                string.Equals(accessToken, _expectedToken, StringComparison.Ordinal))
            {
                return true;
            }
        }

        // Native <video> elements cannot send custom headers; allow a scoped media token
        // exclusively on the streaming endpoint bound to the requested torrentId and fileIndex.
        if (request.Path.Equals("/api/v1/files/stream", StringComparison.OrdinalIgnoreCase))
        {
            var mediaToken = request.Query["mediaToken"].ToString();
            if (!string.IsNullOrEmpty(mediaToken) &&
                Guid.TryParse(request.Query["torrentId"].ToString(), out var torrentId) &&
                int.TryParse(request.Query["fileIndex"].ToString(), out var fileIndex) &&
                _streamTokenService.ValidateToken(mediaToken, torrentId, fileIndex))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsExempt(PathString path)
    {
        foreach (var exemptPath in ExemptPaths)
        {
            if (path.StartsWithSegments(exemptPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
