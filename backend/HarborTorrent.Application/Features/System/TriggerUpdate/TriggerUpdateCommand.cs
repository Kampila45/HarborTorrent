using HarborTorrent.Application.Abstractions.Realtime;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace HarborTorrent.Application.Features.System.TriggerUpdate;

public sealed record TriggerUpdateCommand() : IRequest;

internal sealed class TriggerUpdateCommandHandler : IRequestHandler<TriggerUpdateCommand>
{
    private readonly ISystemEventPublisher _eventPublisher;

    public TriggerUpdateCommandHandler(ISystemEventPublisher eventPublisher)
    {
        _eventPublisher = eventPublisher;
    }

    public Task<Unit> Handle(TriggerUpdateCommand request, CancellationToken cancellationToken)
    {
        // Execute simulation in background to avoid blocking the API response.
        _ = Task.Run(async () =>
        {
            try
            {
                for (int i = 0; i <= 100; i += 5)
                {
                    await _eventPublisher.BroadcastUpdateProgressAsync(i);
                    // Add delay to mimic realistic network latency during download phases
                    await Task.Delay(250);
                }

                await _eventPublisher.BroadcastUpdateCompleteAsync();
            }
            catch
            {
                // Silently swallow exceptions here for simulation purposes, 
                // preventing background thread crashes from halting the process
            }
        });

        return Task.FromResult(Unit.Value);
    }
}
