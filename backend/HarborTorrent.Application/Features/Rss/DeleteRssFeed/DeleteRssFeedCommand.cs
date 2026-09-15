using HarborTorrent.Application.Abstractions.Persistence;
using MediatR;

namespace HarborTorrent.Application.Features.Rss.DeleteRssFeed;

public record DeleteRssFeedCommand(Guid Id) : IRequest;

public class DeleteRssFeedCommandHandler : IRequestHandler<DeleteRssFeedCommand>
{
    private readonly IRssFeedRepository _repository;

    public DeleteRssFeedCommandHandler(IRssFeedRepository repository)
    {
        _repository = repository;
    }

    public async Task<Unit> Handle(DeleteRssFeedCommand request, CancellationToken cancellationToken)
    {
        var feed = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (feed != null)
        {
            _repository.Remove(feed);
            await _repository.SaveChangesAsync(cancellationToken);
        }
        
        return Unit.Value;
    }
}
