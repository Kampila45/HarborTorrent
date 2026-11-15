using HarborTorrent.Application.Abstractions.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HarborTorrent.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<HarborDbContext>((serviceProvider, options) =>
        {
            // Resolve configuration lazily from DI so test host overrides (e.g. WebApplicationFactory)
            // take effect before choosing the SQLite database path.
            var resolvedConfig = serviceProvider.GetService<IConfiguration>() ?? configuration;
            var configuredConnectionString = resolvedConfig.GetConnectionString("HarborTorrent");

            string connectionString;
            if (string.IsNullOrWhiteSpace(configuredConnectionString))
            {
                var defaultDbPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "HarborTorrent",
                    "harbor.db");
                EnsureSqliteDirectoryExists(defaultDbPath);
                connectionString = $"Data Source={defaultDbPath}";
            }
            else
            {
                connectionString = configuredConnectionString;
                var sqliteBuilder = new SqliteConnectionStringBuilder(connectionString);
                if (!string.IsNullOrWhiteSpace(sqliteBuilder.DataSource) &&
                    !string.Equals(sqliteBuilder.DataSource, ":memory:", StringComparison.OrdinalIgnoreCase))
                {
                    EnsureSqliteDirectoryExists(sqliteBuilder.DataSource);
                }
            }

            options.UseSqlite(connectionString);
        });

        services.AddScoped<ITorrentRepository, Repositories.TorrentRepository>();
        services.AddScoped<ISettingsRepository, Repositories.SettingsRepository>();
        services.AddScoped<IRssFeedRepository, Repositories.RssFeedRepository>();

        return services;
    }

    private static void EnsureSqliteDirectoryExists(string databasePath)
    {
        var dbDir = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(dbDir))
        {
            Directory.CreateDirectory(dbDir);
        }
    }
}