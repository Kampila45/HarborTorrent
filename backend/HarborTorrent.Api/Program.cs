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

// Bind explicitly to port 5000.
// The Vite dev proxy and all frontend service URLs target this port.
// appsettings.json is NOT reliably available when running as a Tauri sidecar
// (single-file publish places it beside the exe, but Tauri only bundles the binary),
// so all critical configuration is hardcoded here.
builder.WebHost.UseUrls("http://localhost:5000");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddApplication();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
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
        // Origins that need to reach this API:
        //   - Vite dev server (npm run dev)
        //   - Tauri WebView on Windows  → http://tauri.localhost
        //   - Tauri WebView on Linux    → http://localhost:1420
        // These are fixed for a local desktop app; hardcoded here
        // rather than relying on appsettings.json (which is not bundled with the sidecar).
        string[] allowedOrigins =
        [
            "http://localhost:5173",   // Vite dev server
            "http://localhost:1420",   // Tauri WebView (Linux)
            "http://tauri.localhost",  // Tauri WebView (Windows)
            "tauri://localhost",       // Tauri WebView (older versions)
        ];

        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

app.UseMiddleware<RequestContextMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
// HTTPS redirect is omitted — this is a local sidecar that only listens on HTTP localhost.
// There is no certificate, no external traffic, and no reason to redirect.

app.UseCors();

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
        version = typeof(Program).Assembly.GetName().Version?.ToString(),
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
