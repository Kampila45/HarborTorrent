using HarborTorrent.Application.Abstractions.Realtime;
using HarborTorrent.Application.Abstractions.Runtime;
using HarborTorrent.Application.Abstractions.Torrents;
using HarborTorrent.Infrastructure.Realtime;
using HarborTorrent.Infrastructure.Runtime;
using HarborTorrent.Application.Abstractions.Search;
using HarborTorrent.Infrastructure.Search;
using HarborTorrent.Infrastructure.Torrents;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HarborTorrent.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, string launchToken)
    {
        // Register the hub filter as a singleton for DI resolution, then wire it
        // explicitly through SignalR options. Both steps are required: the singleton
        // registration makes the filter available for constructor injection; the
        // AddFilter call ensures SignalR actually applies it to every hub connection.
        services.AddSingleton(new LaunchTokenHubFilter(launchToken));
        services.AddSignalR(options =>
            {
                options.AddFilter<LaunchTokenHubFilter>();
            })
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
            });
        services.AddSingleton<MonoTorrentRuntimeCoordinator>();
        services.AddSingleton<ITorrentRuntimeCoordinator>(serviceProvider => serviceProvider.GetRequiredService<MonoTorrentRuntimeCoordinator>());
        services.AddSingleton<ITorrentSourceMetadataProvider, MonoTorrentSourceMetadataProvider>();
        services.AddScoped<ITorrentEventPublisher, SignalRTorrentEventPublisher>();
        services.AddScoped<ISystemEventPublisher, SystemEventPublisher>();
        services.AddHostedService<TorrentLifecycleHostedService>();
        services.AddHostedService<HarborTorrent.Infrastructure.BackgroundJobs.RssPollingHostedService>();
        
        services.AddHttpClient<ApibaySearchProvider>();
        services.AddScoped<ITorrentSearchProvider>(sp => sp.GetRequiredService<ApibaySearchProvider>());

        services.AddHttpClient<YtsSearchProvider>();
        services.AddScoped<ITorrentSearchProvider>(sp => sp.GetRequiredService<YtsSearchProvider>());

        return services;
    }
}