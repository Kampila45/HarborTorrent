using HarborTorrent.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HarborTorrent.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        // Default to a SQLite database file in the user's local app data folder.
        // This works on Windows, macOS, and Linux without any server installation.
        var defaultDbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HarborTorrent",
            "harbor.db");

        var connectionString = configuration.GetConnectionString("HarborTorrent")
            ?? $"Data Source={defaultDbPath}";

        // Ensure the directory exists before EF Core tries to create the file
        var dbDir = Path.GetDirectoryName(defaultDbPath);
        if (!string.IsNullOrEmpty(dbDir))
        {
            Directory.CreateDirectory(dbDir);
        }

        services.AddDbContext<HarborDbContext>(options => options.UseSqlite(connectionString));

        services.AddScoped<ITorrentRepository, Repositories.TorrentRepository>();
        services.AddScoped<ISettingsRepository, Repositories.SettingsRepository>();
        services.AddScoped<IRssFeedRepository, Repositories.RssFeedRepository>();

        return services;
    }
}