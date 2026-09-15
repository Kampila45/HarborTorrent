namespace HarborTorrent.Api.Middleware;

internal sealed class RequestContextMiddleware
{
    internal const string RequestIdHeaderName = "X-Request-ID";

    private readonly RequestDelegate _next;

    public RequestContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var requestId = context.Request.Headers.TryGetValue(RequestContextMiddleware.RequestIdHeaderName, out var headerValue)
            && !string.IsNullOrWhiteSpace(headerValue)
                ? headerValue.ToString()
                : Guid.NewGuid().ToString("n");

        context.TraceIdentifier = requestId;
        context.Response.Headers[RequestIdHeaderName] = requestId;

        await _next(context);
    }
}