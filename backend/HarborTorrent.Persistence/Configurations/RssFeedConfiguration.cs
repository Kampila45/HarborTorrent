using HarborTorrent.Domain.Rss;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HarborTorrent.Persistence.Configurations;

internal sealed class RssFeedConfiguration : IEntityTypeConfiguration<RssFeed>
{
    public void Configure(EntityTypeBuilder<RssFeed> builder)
    {
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.Name).IsRequired().HasMaxLength(128);
        builder.Property(x => x.Url).IsRequired().HasMaxLength(2048);
        
        builder.HasMany(x => x.Filters)
            .WithOne()
            .HasForeignKey(x => x.RssFeedId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class RssFilterConfiguration : IEntityTypeConfiguration<RssFilter>
{
    public void Configure(EntityTypeBuilder<RssFilter> builder)
    {
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.RegexPattern).IsRequired().HasMaxLength(512);
        builder.Property(x => x.SavePath).IsRequired().HasMaxLength(512);
    }
}

internal sealed class RssFeedItemHistoryConfiguration : IEntityTypeConfiguration<RssFeedItemHistory>
{
    public void Configure(EntityTypeBuilder<RssFeedItemHistory> builder)
    {
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.ItemIdentifier).IsRequired().HasMaxLength(1024);
        
        builder.HasIndex(x => new { x.RssFeedId, x.ItemIdentifier }).IsUnique();
    }
}
