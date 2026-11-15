using System.Collections.Concurrent;
using HarborTorrent.Application.Abstractions.Rss;

namespace HarborTorrent.Infrastructure.BackgroundJobs;

internal sealed class RssPollStatusTracker : IRssPollStatusTracker
{
    private readonly ConcurrentDictionary<Guid, RssPollStatus> _statuses = new();
    private readonly TimeProvider _timeProvider;

    public RssPollStatusTracker(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public void MarkSuccess(Guid feedId)
    {
        _statuses.TryRemove(feedId, out _);
    }

    public void MarkFailure(Guid feedId, string errorMessage)
    {
        _statuses[feedId] = new RssPollStatus(_timeProvider.GetUtcNow(), errorMessage);
    }

    public RssPollStatus? GetStatus(Guid feedId)
    {
        return _statuses.TryGetValue(feedId, out var status) ? status : null;
    }
}

