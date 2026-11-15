using HarborTorrent.Api.Middleware;
using HarborTorrent.Api.Security;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace HarborTorrent.Tests;

public sealed class LaunchTokenAuthenticationTests
{
    private const string TestLaunchToken = "test-launch-token-1234";

    [Fact]
    public async Task HealthEndpoint_IsExemptFromAuth()
    {
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();
        context.Request.Path = "/health";

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task ApiRoute_WithXLaunchTokenHeader_Succeeds()
    {
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/torrents";
        context.Request.Headers["X-Launch-Token"] = TestLaunchToken;

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task ApiRoute_WithQueryAccessTokenOnly_IsRejected()
    {
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/torrents";
        context.Request.QueryString = new QueryString($"?access_token={TestLaunchToken}");

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task HubRoute_WithAccessTokenQueryParam_Succeeds()
    {
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();
        context.Request.Path = "/hubs/torrents";
        context.Request.QueryString = new QueryString($"?access_token={TestLaunchToken}");

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task HubRoute_WithBearerAuthorization_Succeeds()
    {
        var middleware = CreateMiddleware();
        var context = new DefaultHttpContext();
        context.Request.Path = "/hubs/torrents/negotiate";
        context.Request.Headers["Authorization"] = $"Bearer {TestLaunchToken}";

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task StreamEndpoint_WithValidScopedMediaToken_Succeeds()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var streamTokenService = new StreamTokenService(TestLaunchToken, timeProvider);
        var middleware = CreateMiddleware(streamTokenService);

        var torrentId = Guid.NewGuid();
        var (mediaToken, _) = streamTokenService.IssueToken(torrentId, 2);

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/files/stream";
        context.Request.QueryString = new QueryString($"?torrentId={torrentId}&fileIndex=2&mediaToken={mediaToken}");

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task StreamEndpoint_WithMediaTokenForDifferentFile_IsRejected()
    {
        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var streamTokenService = new StreamTokenService(TestLaunchToken, timeProvider);
        var middleware = CreateMiddleware(streamTokenService);

        var torrentId = Guid.NewGuid();
        var (mediaToken, _) = streamTokenService.IssueToken(torrentId, 2);

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/files/stream";
        context.Request.QueryString = new QueryString($"?torrentId={torrentId}&fileIndex=3&mediaToken={mediaToken}");

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public void StreamTokenService_ExpiredToken_IsRejected()
    {
        var now = new DateTimeOffset(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(now);
        var service = new StreamTokenService(TestLaunchToken, timeProvider);
        var torrentId = Guid.NewGuid();

        var (token, _) = service.IssueToken(torrentId, 0);
        Assert.True(service.ValidateToken(token, torrentId, 0));

        timeProvider.Advance(TimeSpan.FromHours(5));
        Assert.False(service.ValidateToken(token, torrentId, 0));
    }

    private static LaunchTokenAuthenticationMiddleware CreateMiddleware(StreamTokenService? streamTokenService = null)
    {
        return new LaunchTokenAuthenticationMiddleware(
            ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            },
            TestLaunchToken,
            streamTokenService ?? new StreamTokenService(TestLaunchToken, TimeProvider.System));
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public FakeTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan delta) => _utcNow = _utcNow.Add(delta);
    }
}

