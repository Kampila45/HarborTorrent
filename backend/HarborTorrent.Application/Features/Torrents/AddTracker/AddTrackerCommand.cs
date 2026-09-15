using HarborTorrent.Application.Abstractions.Runtime;
using HarborTorrent.Domain.Common;
using MediatR;

namespace HarborTorrent.Application.Features.Torrents.AddTracker;

public sealed record AddTrackerCommand(Guid TorrentId, string TrackerUrl) : IRequest<Result>;

internal sealed class AddTrackerCommandHandler : IRequestHandler<AddTrackerCommand, Result>
{
    private readonly ITorrentRuntimeCoordinator _runtimeCoordinator;

    public AddTrackerCommandHandler(ITorrentRuntimeCoordinator runtimeCoordinator)
    {
        _runtimeCoordinator = runtimeCoordinator;
    }

    public async Task<Result> Handle(AddTrackerCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TrackerUrl))
        {
            return Result.Failure(Error.Validation("TrackerUrl is required."));
        }

        if (!Uri.TryCreate(request.TrackerUrl, UriKind.Absolute, out _))
        {
            return Result.Failure(Error.Validation("TrackerUrl must be a valid absolute URI."));
        }

        try
        {
            await _runtimeCoordinator.AddTrackerAsync(request.TorrentId, request.TrackerUrl, cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(Error.Conflict($"Failed to add tracker: {ex.Message}"));
        }
    }
}
