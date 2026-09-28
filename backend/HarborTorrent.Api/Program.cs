using HarborTorrent.Api.Endpoints;
using HarborTorrent.Api.Middleware;
using HarborTorrent.Application;
using HarborTorrent.Infrastructure;
using HarborTorrent.Infrastructure.Realtime;
using HarborTorrent.Persistence;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
var startedAtUtc = TimeProvider.System.GetUtcNow();

// Read the launch token and port injected by the Tauri host via environment variables.
// In local development these variables are absent, so defaults are used.
var launchToken = Environment.GetEnvironmentVariable("HARBOR_LAUNCH_TOKEN");
if (string.IsNullOrWhiteSpace(launchToken))
{
    launchToken = "dev-token-only";
}
var portString = Environment.GetEnvironmentVariable("HARBOR_API_PORT");
var apiPort = int.TryParse(portString, out var parsed) ? parsed : 5000;

// Bind exclusively to the loopback interface on the port selected by the Tauri host.
// This prevents the API from being reachable from any other device on the network.
builder.WebHost.UseUrls($"http://127.0.0.1:{apiPort}");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddApplication();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration, launchToken);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddHttpClient();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            // In development the Vite dev server and Tauri dev WebView use various origins.
            policy.WithOrigins(
                    "http://localhost:5173",
                    "http://localhost:1420",
                    "http://tauri.localhost",
                    "tauri://localhost")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
        else
        {
            // In production only the packaged Tauri WebView origin is permitted.
            // On Linux the WebView origin is http://tauri.localhost; on Windows/macOS it is tauri://localhost.
            policy.WithOrigins(
                    "http://tauri.localhost",
                    "tauri://localhost")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
    });
});

var app = builder.Build();

app.UseMiddleware<RequestContextMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseCors();

// Enforce launch token authentication on all requests except /health.
// This must come after exception handling so that auth failures produce a structured response.
app.UseMiddleware<LaunchTokenAuthenticationMiddleware>(launchToken);

// HTTPS redirect is omitted — this is a local sidecar that only listens on HTTP localhost.
// There is no certificate, no external traffic, and no reason to redirect.

app.UseSwagger();
app.MapScalarApiReference(options =>
{
    options.Title = "HarborTorrent API";
    options.WithOpenApiRoutePattern("/swagger/{documentName}/swagger.json");
});

app.MapGet("/health", async (
    HarborDbContext dbContext,
    IHostEnvironment hostEnvironment,
    TimeProvider timeProvider,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var nowUtc = timeProvider.GetUtcNow();
    var databaseHealthy = await dbContext.Database.CanConnectAsync(cancellationToken);
    var overallStatus = databaseHealthy ? "Healthy" : "Degraded";

    var payload = new
    {
        status = overallStatus,
        service = "HarborTorrent.Api",
        environment = hostEnvironment.EnvironmentName,
        timestampUtc = nowUtc,
        uptimeSeconds = Math.Round((nowUtc - startedAtUtc).TotalSeconds, 2),
        version = typeof(Program).Assembly.GetName().Version?.ToString(3),
        commit = Environment.GetEnvironmentVariable("HARBOR_COMMIT_SHA") ?? "local",
        checks = new
        {
            database = databaseHealthy ? "Healthy" : "Unhealthy"
        },
        traceId = httpContext.TraceIdentifier
    };

    return databaseHealthy
        ? Results.Ok(payload)
        : Results.Json(payload, statusCode: StatusCodes.Status503ServiceUnavailable);
});

app.MapTorrentEndpoints();
app.MapDirectoryEndpoints();
app.MapDashboardEndpoints();
app.MapFilesEndpoints();
app.MapSettingsEndpoints();
app.MapSearchEndpoints();
app.MapSystemEndpoints();
app.MapRssEndpoints();
app.MapHub<TorrentHub>("/hubs/torrents");
app.MapHub<HarborTorrent.Infrastructure.Realtime.SystemHub>("/hubs/system");

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<HarborDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.Run();

public partial class Program;
