using HarborTorrent.Domain.Common;

namespace HarborTorrent.Domain.Torrents;

/// <summary>
/// Captures the external source used to add a torrent and the metadata needed to resume it.
/// </summary>
public sealed class TorrentSource
{
    private TorrentSource()
    {
        Value = string.Empty;
    }

    private TorrentSource(TorrentSourceKind kind, string value, string? fileName)
    {
        Kind = kind;
        Value = value;
        FileName = fileName;
    }

    public TorrentSourceKind Kind { get; private set; }

    public string Value { get; private set; }

    public string? FileName { get; private set; }

    public static Result<TorrentSource> FromMagnetLink(string magnetLink)
    {
        if (string.IsNullOrWhiteSpace(magnetLink))
        {
            return Result<TorrentSource>.Failure(Error.Validation("A magnet link is required."));
        }

        var link = magnetLink.Trim();

        // Add reliable public UDP trackers to accelerate metadata resolution for trackerless links
        var publicTrackers = new[]
        {
            "udp://tracker.opentrackr.org:1337/announce",
            "udp://9.rarbg.com:2810/announce",
            "udp://tracker.openbittorrent.com:6969/announce",
            "udp://exodus.desync.com:6969/announce",
            "udp://tracker.torrent.eu.org:451/announce"
        };

        foreach (var tracker in publicTrackers)
        {
            var encodedTracker = Uri.EscapeDataString(tracker);
            // Magnet links can contain trackers as raw strings or url-encoded strings depending on the source. 
            // Check if the tracker is already present to avoid duplicates.
            if (!link.Contains(encodedTracker, StringComparison.OrdinalIgnoreCase) && 
                !link.Contains($"tr={tracker}", StringComparison.OrdinalIgnoreCase))
            {
                link += $"&tr={encodedTracker}";
            }
        }

        return Result<TorrentSource>.Success(new TorrentSource(TorrentSourceKind.MagnetLink, link, null));
    }

    public static Result<TorrentSource> FromTorrentFile(string fileName, string encodedContent)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return Result<TorrentSource>.Failure(Error.Validation("A torrent file name is required."));
        }

        if (string.IsNullOrWhiteSpace(encodedContent))
        {
            return Result<TorrentSource>.Failure(Error.Validation("A torrent file payload is required."));
        }

        return Result<TorrentSource>.Success(new TorrentSource(TorrentSourceKind.TorrentFile, encodedContent.Trim(), fileName.Trim()));
    }

    public string GetDisplayName()
    {
        if (Kind == TorrentSourceKind.TorrentFile && !string.IsNullOrWhiteSpace(FileName))
        {
            return Path.GetFileNameWithoutExtension(FileName);
        }

        const string displayNameToken = "dn=";
        var index = Value.IndexOf(displayNameToken, StringComparison.OrdinalIgnoreCase);

        if (index < 0)
        {
            return "magnet-download";
        }

        var valueStart = index + displayNameToken.Length;
        var valueEnd = Value.IndexOf('&', valueStart);
        var rawValue = valueEnd >= 0 ? Value[valueStart..valueEnd] : Value[valueStart..];

        return string.IsNullOrWhiteSpace(rawValue)
            ? "magnet-download"
            : Uri.UnescapeDataString(rawValue);
    }
}