using MediatR;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace HarborTorrent.Application.Features.System.GetChangelog;

public sealed record GetChangelogQuery() : IRequest<string>;

internal sealed class GetChangelogQueryHandler : IRequestHandler<GetChangelogQuery, string>
{
    public async Task<string> Handle(GetChangelogQuery request, CancellationToken cancellationToken)
    {
        var assembly = typeof(GetChangelogQueryHandler).Assembly;
        using var stream = assembly.GetManifestResourceStream("CHANGELOG.md");
        
        if (stream != null)
        {
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync(cancellationToken);
        }

        return "Changelog not found.";
    }
}
