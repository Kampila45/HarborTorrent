namespace HarborTorrent.Domain.Rss;

/// <summary>
/// Tracks RSS items that have already been matched and added, preventing duplicate downloads on subsequent polls.
/// </summary>
public sealed class RssFeedItemHistory
{
    /// <summary>
    /// Gets the unique identifier of the history record.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Gets the unique identifier of the RSS feed this item came from.
    /// </summary>
    public Guid RssFeedId { get; private set; }

    /// <summary>
    /// Gets the unique identifier of the filter that matched this item.
    /// </summary>
    public Guid RssFilterId { get; private set; }

    /// <summary>
    /// Gets a unique hash or identifier of the RSS item (e.g., the magnet link hash, the GUID, or the title).
    /// </summary>
    public string ItemIdentifier { get; private set; }

    /// <summary>
    /// Gets the UTC timestamp when this item was processed and added.
    /// </summary>
    public DateTime ProcessedAt { get; private set; }

    private RssFeedItemHistory() { } // EF Core

    /// <summary>
    /// Initializes a new instance of the <see cref="RssFeedItemHistory"/> class.
    /// </summary>
    /// <param name="id">The unique identifier.</param>
    /// <param name="rssFeedId">The parent RSS feed identifier.</param>
    /// <param name="rssFilterId">The parent RSS filter identifier.</param>
    /// <param name="itemIdentifier">The unique item identifier.</param>
    public RssFeedItemHistory(Guid id, Guid rssFeedId, Guid rssFilterId, string itemIdentifier)
    {
        Id = id;
        RssFeedId = rssFeedId;
        RssFilterId = rssFilterId;
        ItemIdentifier = itemIdentifier;
        ProcessedAt = DateTime.UtcNow;
    }
}
