using HarborTorrent.Application.Abstractions.Torrents;
using HarborTorrent.Domain.Common;
using HarborTorrent.Domain.Torrents;
using MonoTorrent;

namespace HarborTorrent.Infrastructure.Torrents;

internal sealed class MonoTorrentSourceMetadataProvider : ITorrentSourceMetadataProvider
{
    public Result<TorrentSourceMetadata> GetMetadata(TorrentSource source)
    {
        try
        {
            if (source.Kind == TorrentSourceKind.MagnetLink)
            {
                var magnetLink = MagnetLink.Parse(source.Value);
                return Result<TorrentSourceMetadata>.Success(new TorrentSourceMetadata(ToInfoHashKey(magnetLink.InfoHashes)));
            }

            var bytes = Convert.FromBase64String(source.Value);

            if (!MonoTorrent.Torrent.TryLoad(bytes, out var torrent))
            {
                return Result<TorrentSourceMetadata>.Failure(Error.Validation("The torrent file payload could not be parsed."));
            }

            return Result<TorrentSourceMetadata>.Success(new TorrentSourceMetadata(ToInfoHashKey(torrent.InfoHashes)));
        }
        catch (FormatException)
        {
            return Result<TorrentSourceMetadata>.Failure(Error.Validation("The torrent source format is invalid."));
        }
    }

    private static string ToInfoHashKey(InfoHashes infoHashes)
    {
        return infoHashes.V1?.ToHex()
            ?? infoHashes.V2?.ToHex()
            ?? throw new InvalidOperationException("The torrent source did not contain a supported info hash.");
    }
}