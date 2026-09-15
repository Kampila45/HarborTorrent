using HarborTorrent.Domain.Rss;
using HarborTorrent.Domain.Settings;
using HarborTorrent.Domain.Torrents;
using Microsoft.EntityFrameworkCore;

namespace HarborTorrent.Persistence;

public sealed class HarborDbContext : DbContext
{
    public HarborDbContext(DbContextOptions<HarborDbContext> options)
        : base(options)
    {
    }

    public DbSet<Torrent> Torrents => Set<Torrent>();
    public DbSet<GlobalSettings> Settings => Set<GlobalSettings>();
    public DbSet<RssFeed> RssFeeds => Set<RssFeed>();
    public DbSet<RssFilter> RssFilters => Set<RssFilter>();
    public DbSet<RssFeedItemHistory> RssFeedItemHistories => Set<RssFeedItemHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var torrent = modelBuilder.Entity<Torrent>();

        torrent.HasKey(entity => entity.Id);
        torrent.Property(entity => entity.InfoHash).HasMaxLength(64);
        torrent.Property(entity => entity.Name).HasMaxLength(256);
        torrent.Property(entity => entity.SavePath).HasMaxLength(512);
        torrent.Property(entity => entity.UserId).HasMaxLength(128).IsRequired();
        torrent.Property(entity => entity.Status).HasConversion<string>().HasMaxLength(32);
        torrent.Property(entity => entity.Progress).HasPrecision(5, 2);
        torrent.Property(entity => entity.Ratio).HasPrecision(8, 3);
        torrent.Property(entity => entity.ErrorMessage).IsRequired(false);

        // Prevents adding the same torrent twice on a single-user desktop install.
        torrent.HasIndex(entity => entity.InfoHash)
            .IsUnique()
            .HasFilter("\"InfoHash\" <> ''");

        torrent.OwnsOne(entity => entity.Source, source =>
        {
            source.Property(property => property.Kind).HasConversion<string>().HasColumnName("SourceKind").HasMaxLength(32);
            source.Property(property => property.Value).HasColumnName("SourceValue");
            source.Property(property => property.FileName).HasColumnName("SourceFileName").HasMaxLength(256);
        });

        var settings = modelBuilder.Entity<GlobalSettings>();
        settings.HasKey(entity => entity.Id);
        settings.Property(entity => entity.DefaultDownloadPath).HasMaxLength(512);

        // Apply any IEntityTypeConfiguration from the assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HarborDbContext).Assembly);
    }
}