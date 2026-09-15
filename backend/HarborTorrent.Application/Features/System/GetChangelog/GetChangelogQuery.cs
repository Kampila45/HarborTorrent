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
        var changelogPath = Path.Combine(global::System.AppContext.BaseDirectory, "../../../../CHANGELOG.md");
        
        // Check alternate relative paths to support both local development (dotnet run) and published binaries.
        if (!File.Exists(changelogPath))
        {
            changelogPath = Path.Combine(global::System.AppContext.BaseDirectory, "CHANGELOG.md");
        }

        if (File.Exists(changelogPath))
        {
            return await File.ReadAllTextAsync(changelogPath, cancellationToken);
        }

        return "Changelog not found.";
    }
}
