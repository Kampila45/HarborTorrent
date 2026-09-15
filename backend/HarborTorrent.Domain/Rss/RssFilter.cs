namespace HarborTorrent.Domain.Rss;

/// <summary>
/// Represents a regex-based filter applied to an RSS feed to automate torrent downloads.
/// </summary>
public sealed class RssFilter
{
    /// <summary>
    /// Gets the unique identifier of the RSS filter.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Gets the unique identifier of the parent RSS feed.
    /// </summary>
    public Guid RssFeedId { get; private set; }

    /// <summary>
    /// Gets the regex pattern used to match RSS item titles.
    /// </summary>
    public string RegexPattern { get; private set; }

    /// <summary>
    /// Gets the destination path where matching torrents should be saved.
    /// </summary>
    public string SavePath { get; private set; }

    private RssFilter() { } // EF Core

    /// <summary>
    /// Initializes a new instance of the <see cref="RssFilter"/> class.
    /// </summary>
    /// <param name="id">The unique identifier.</param>
    /// <param name="rssFeedId">The parent RSS feed identifier.</param>
    /// <param name="regexPattern">The regex pattern.</param>
    /// <param name="savePath">The save path.</param>
    public RssFilter(Guid id, Guid rssFeedId, string regexPattern, string savePath)
    {
        Id = id;
        RssFeedId = rssFeedId;
        RegexPattern = regexPattern;
        SavePath = savePath;
    }
}
