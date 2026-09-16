using HarborTorrent.Application.Abstractions.Realtime;
using HarborTorrent.Application.Abstractions.Runtime;
using HarborTorrent.Application.Abstractions.Torrents;
using HarborTorrent.Infrastructure.Realtime;
using HarborTorrent.Infrastructure.Runtime;
using HarborTorrent.Application.Abstractions.Search;
using HarborTorrent.Infrastructure.Search;
using HarborTorrent.Infrastructure.Torrents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HarborTorrent.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSignalR().AddJsonProtocol(options =>
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