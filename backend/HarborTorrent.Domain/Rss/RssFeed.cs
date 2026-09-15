namespace HarborTorrent.Domain.Rss;

/// <summary>
/// Represents an RSS feed that the automation engine polls periodically to discover new torrents.
/// </summary>
public sealed class RssFeed
{
    /// <summary>
    /// Gets the unique identifier of the RSS feed.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Gets the user-friendly name of the RSS feed.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// Gets the URL of the RSS feed.
    /// </summary>
    public string Url { get; private set; }

    /// <summary>
    /// Gets the UTC timestamp when the feed was last successfully polled.
    /// </summary>
    public DateTime? LastPolledAt { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the feed is active and should be polled.
    /// </summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Gets the list of regex filters applied to this feed.
    /// </summary>
    public ICollection<RssFilter> Filters { get; private set; } = new List<RssFilter>();

    private RssFeed() { } // EF Core

    /// <summary>
    /// Initializes a new instance of the <see cref="RssFeed"/> class.
    /// </summary>
    /// <param name="id">The unique identifier.</param>
    /// <param name="name">The name.</param>
    /// <param name="url">The URL.</param>
    public RssFeed(Guid id, string name, string url)
    {
        Id = id;
        Name = name;
        Url = url;
        IsActive = true;
    }

    /// <summary>
    /// Updates the RSS feed details.
    /// </summary>
    /// <param name="name">The new name.</param>
    /// <param name="url">The new URL.</param>
    /// <param name="isActive">Whether the feed is active.</param>
    public void Update(string name, string url, bool isActive)
    {
        Name = name;
        Url = url;
        IsActive = isActive;
    }

    /// <summary>
    /// Marks the feed as polled at the current UTC time.
    /// </summary>
    public void MarkAsPolled()
    {
        LastPolledAt = DateTime.UtcNow;
    }
}
