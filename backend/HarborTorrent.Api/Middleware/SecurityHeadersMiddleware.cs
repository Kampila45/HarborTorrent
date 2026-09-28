namespace HarborTorrent.Api.Middleware;

/// <summary>
/// Adds security-related HTTP response headers to every response to reduce the
/// attack surface of the embedded local API against content injection and MIME sniffing.
/// </summary>
internal sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Prevent the browser from inferring a MIME type that differs from the
        // declared Content-Type, which can enable certain content injection attacks.
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";

        // Restrict the embedded WebView to resources that originate from the
        // local API itself. No inline scripts, eval, or external frames are permitted.
        context.Response.Headers["Content-Security-Policy"] =
            "default-src 'self'; script-src 'self'; connect-src 'self' ws://127.0.0.1; " +
            "img-src 'self' data:; style-src 'self' 'unsafe-inline'; frame-ancestors 'none';";

        await _next(context);
    }
}
