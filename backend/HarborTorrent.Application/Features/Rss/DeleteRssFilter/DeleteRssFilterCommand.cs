using HarborTorrent.Application.Abstractions.Persistence;
using MediatR;

namespace HarborTorrent.Application.Features.Rss.DeleteRssFilter;

public record DeleteRssFilterCommand(Guid Id) : IRequest;

public class DeleteRssFilterCommandHandler : IRequestHandler<DeleteRssFilterCommand>
{
    private readonly IRssFeedRepository _repository;

    public DeleteRssFilterCommandHandler(IRssFeedRepository repository)
    {
        _repository = repository;
    }

    public async Task<Unit> Handle(DeleteRssFilterCommand request, CancellationToken cancellationToken)
    {
        var filter = await _repository.GetFilterByIdAsync(request.Id, cancellationToken);
        if (filter != null)
        {
            _repository.RemoveFilter(filter);
            await _repository.SaveChangesAsync(cancellationToken);
        }
        
        return Unit.Value;
    }
}
