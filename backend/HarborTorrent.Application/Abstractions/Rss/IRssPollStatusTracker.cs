namespace HarborTorrent.Application.Abstractions.Rss;

public sealed record RssPollStatus(DateTimeOffset? LastErrorAt, string? LastError);

public interface IRssPollStatusTracker
{
    void MarkSuccess(Guid feedId);

    void MarkFailure(Guid feedId, string errorMessage);

    RssPollStatus? GetStatus(Guid feedId);
}

