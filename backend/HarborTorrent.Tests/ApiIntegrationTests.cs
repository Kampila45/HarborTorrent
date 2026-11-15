using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace HarborTorrent.Tests;

public sealed class ApiIntegrationTests : IAsyncLifetime
{
    private const string LaunchToken = "integration-launch-token-98765";
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"harbor-int-{Guid.NewGuid():N}");
    private HarborApiFactory _factory = null!;
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_tempDir);
        Environment.SetEnvironmentVariable("HARBOR_LAUNCH_TOKEN", LaunchToken);
        _factory = new HarborApiFactory(_tempDir);
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        Environment.SetEnvironmentVariable("HARBOR_LAUNCH_TOKEN", null);

        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task Health_ReturnsHealthyWithoutAuth_AndProtectedRoutesRequireAuth()
    {
        var healthResponse = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);

        var unauthSettings = await _client.GetAsync("/api/v1/settings");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthSettings.StatusCode);

        using var authReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/settings");
        authReq.Headers.Add("X-Launch-Token", LaunchToken);
        var authSettings = await _client.SendAsync(authReq);
        Assert.Equal(HttpStatusCode.OK, authSettings.StatusCode);
    }

    [Fact]
    public async Task SignalR_NegotiateAndWebSocketHandshake_SucceedWithValidToken_AndRejectInvalid()
    {
        // Unauthenticated negotiate must be 401
        var unauthNeg = await _client.PostAsync("/hubs/torrents/negotiate?negotiateVersion=1", null);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthNeg.StatusCode);

        // Authenticated negotiate with Bearer token (matching @microsoft/signalr client)
        using var negReq = new HttpRequestMessage(HttpMethod.Post, "/hubs/torrents/negotiate?negotiateVersion=1");
        negReq.Headers.Add("Authorization", $"Bearer {LaunchToken}");
        var negResp = await _client.SendAsync(negReq);
        Assert.Equal(HttpStatusCode.OK, negResp.StatusCode);

        var negJson = await negResp.Content.ReadFromJsonAsync<JsonElement>();
        var connectionToken = negJson.GetProperty("connectionToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(connectionToken));

        // WebSocket upgrade with ?access_token= must succeed and complete SignalR JSON handshake
        var wsClient = _factory.Server.CreateWebSocketClient();
        using var ws = await wsClient.ConnectAsync(
            new Uri(_factory.Server.BaseAddress, $"hubs/torrents?id={Uri.EscapeDataString(connectionToken!)}&access_token={LaunchToken}"),
            CancellationToken.None);

        var handshakeBytes = Encoding.UTF8.GetBytes("{\"protocol\":\"json\",\"version\":1}\u001e");
        await ws.SendAsync(handshakeBytes, WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);

        var recvBuffer = new byte[256];
        var recvResult = await ws.ReceiveAsync(recvBuffer, CancellationToken.None);
        var handshakeAck = Encoding.UTF8.GetString(recvBuffer, 0, recvResult.Count);
        Assert.Contains("{}", handshakeAck);

        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);

        // WebSocket upgrade with invalid access_token must be rejected before upgrading
        var badWsClient = _factory.Server.CreateWebSocketClient();
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await badWsClient.ConnectAsync(
                new Uri(_factory.Server.BaseAddress, $"hubs/torrents?id={Uri.EscapeDataString(connectionToken)}&access_token=wrong-token"),
                CancellationToken.None);
        });
    }

    [Fact]
    public async Task Settings_ValidationRejectsInvalidSpeeds_AndValidSaveAppliesAuthoritativePath()
    {
        // Invalid negative speed -> 400 validation error
        using var invalidReq = new HttpRequestMessage(HttpMethod.Put, "/api/v1/settings")
        {
            Content = JsonContent.Create(new
            {
                maxDownloadSpeedBytes = -10,
                maxUploadSpeedBytes = 0,
                defaultDownloadPath = Path.Combine(_tempDir, "dl"),
                enableDht = true,
                enablePex = false,
                enableLpd = true,
                anchorModeEnabled = false,
                anchorMaxDownloadSpeedBytes = 10240,
                anchorMaxUploadSpeedBytes = 10240
            })
        };
        invalidReq.Headers.Add("X-Launch-Token", LaunchToken);
        var invalidResp = await _client.SendAsync(invalidReq);
        Assert.Equal(HttpStatusCode.BadRequest, invalidResp.StatusCode);

        // Valid settings update -> 204
        var customPath = Path.Combine(_tempDir, "custom-downloads");
        using var validReq = new HttpRequestMessage(HttpMethod.Put, "/api/v1/settings")
        {
            Content = JsonContent.Create(new
            {
                maxDownloadSpeedBytes = 204800,
                maxUploadSpeedBytes = 102400,
                defaultDownloadPath = customPath,
                enableDht = true,
                enablePex = false,
                enableLpd = false,
                anchorModeEnabled = false,
                anchorMaxDownloadSpeedBytes = 10240,
                anchorMaxUploadSpeedBytes = 10240
            })
        };
        validReq.Headers.Add("X-Launch-Token", LaunchToken);
        var validResp = await _client.SendAsync(validReq);
        Assert.Equal(HttpStatusCode.NoContent, validResp.StatusCode);

        // Verify test execution uses the isolated temporary database, never the user's local DB.
        Assert.True(File.Exists(Path.Combine(_tempDir, "test.db")));

        // Verify GET /api/v1/settings returns persisted DHT / PEX / LPD flags
        using var getSettingsReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/settings");
        getSettingsReq.Headers.Add("X-Launch-Token", LaunchToken);
        var getSettingsResp = await _client.SendAsync(getSettingsReq);
        Assert.Equal(HttpStatusCode.OK, getSettingsResp.StatusCode);
        var getSettingsJson = await getSettingsResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(getSettingsJson.GetProperty("enableDht").GetBoolean());
        Assert.False(getSettingsJson.GetProperty("enablePex").GetBoolean());
        Assert.False(getSettingsJson.GetProperty("enableLpd").GetBoolean());
        Assert.Equal(customPath, getSettingsJson.GetProperty("defaultDownloadPath").GetString());

        // Add torrent without SavePath -> uses saved DefaultDownloadPath
        using var addReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/torrents")
        {
            Content = JsonContent.Create(new
            {
                name = "Integration Test Torrent",
                magnetLink = "magnet:?xt=urn:btih:abcdef0123456789abcdef0123456789abcdef01&dn=Test"
            })
        };
        addReq.Headers.Add("X-Launch-Token", LaunchToken);
        var addResp = await _client.SendAsync(addReq);
        Assert.Equal(HttpStatusCode.Created, addResp.StatusCode);

        var addJson = await addResp.Content.ReadFromJsonAsync<JsonElement>();
        var data = addJson.GetProperty("data");
        var torrentId = data.GetProperty("id").GetGuid();
        Assert.Equal(customPath, data.GetProperty("savePath").GetString());

        // Scoped stream token works on /api/v1/files/stream (returns 404 for nonexistent file, NOT 401)
        using var tokenReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/files/stream-token")
        {
            Content = JsonContent.Create(new { torrentId, fileIndex = 0 })
        };
        tokenReq.Headers.Add("X-Launch-Token", LaunchToken);
        var tokenResp = await _client.SendAsync(tokenReq);
        Assert.Equal(HttpStatusCode.OK, tokenResp.StatusCode);

        var tokenJson = await tokenResp.Content.ReadFromJsonAsync<JsonElement>();
        var mediaToken = tokenJson.GetProperty("data").GetProperty("token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(mediaToken));

        var streamResp = await _client.GetAsync($"/api/v1/files/stream?torrentId={torrentId}&fileIndex=0&mediaToken={Uri.EscapeDataString(mediaToken!)}");
        Assert.Equal(HttpStatusCode.NotFound, streamResp.StatusCode);

        var wrongFileStreamResp = await _client.GetAsync($"/api/v1/files/stream?torrentId={torrentId}&fileIndex=1&mediaToken={Uri.EscapeDataString(mediaToken)}");
        Assert.Equal(HttpStatusCode.Unauthorized, wrongFileStreamResp.StatusCode);

        // Clean up created torrent
        using var delReq = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/torrents/{torrentId}");
        delReq.Headers.Add("X-Launch-Token", LaunchToken);
        var delResp = await _client.SendAsync(delReq);
        Assert.Equal(HttpStatusCode.OK, delResp.StatusCode);
    }

    [Fact]
    public async Task Changelog_And_DashboardMetrics_ReturnExpectedShape()
    {
        using var changelogReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/system/changelog");
        changelogReq.Headers.Add("X-Launch-Token", LaunchToken);
        var changelogResp = await _client.SendAsync(changelogReq);
        Assert.Equal(HttpStatusCode.OK, changelogResp.StatusCode);
        var changelogJson = await changelogResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(changelogJson.GetProperty("changelog").GetString()));

        using var metricsReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/dashboard/metrics");
        metricsReq.Headers.Add("X-Launch-Token", LaunchToken);
        var metricsResp = await _client.SendAsync(metricsReq);
        Assert.Equal(HttpStatusCode.OK, metricsResp.StatusCode);
        var metricsJson = await metricsResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(metricsJson.GetProperty("data").TryGetProperty("totalTorrents", out _));

        using var versionReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/system/version");
        versionReq.Headers.Add("X-Launch-Token", LaunchToken);
        var versionResp = await _client.SendAsync(versionReq);
        Assert.Equal(HttpStatusCode.OK, versionResp.StatusCode);
        var versionJson = await versionResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("1.0.3", versionJson.GetProperty("version").GetString());
        Assert.False(versionJson.TryGetProperty("latestVersion", out _));
    }

    private sealed class HarborApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _tempDir;

        public HarborApiFactory(string tempDir)
        {
            _tempDir = tempDir;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:HarborTorrent"] = $"Data Source={Path.Combine(_tempDir, "test.db")}",
                    ["HarborTorrent:RuntimeDataPath"] = Path.Combine(_tempDir, "runtime"),
                    ["HarborTorrent:DisableInstanceLock"] = "true"
                });
            });
        }
    }
}

