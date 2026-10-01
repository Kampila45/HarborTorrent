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
        // Try the embedded manifest resource first (requires EmbeddedResource in csproj).
        var assembly = typeof(GetChangelogQueryHandler).Assembly;
        using var stream = assembly.GetManifestResourceStream("CHANGELOG.md");

        if (stream != null)
        {
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync(cancellationToken);
        }

        // Fall back to reading from the filesystem relative to the sidecar binary.
        // AppContext.BaseDirectory is the directory containing the published executable,
        // which is reliable in both self-contained and framework-dependent deployments.
        // Environment.CurrentDirectory cannot be used because it depends on the working
        // directory at the time the process was launched, which varies by host.
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "CHANGELOG.md"),
            Path.Combine(AppContext.BaseDirectory, "..", "CHANGELOG.md"),
        };

        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(candidate);
            if (File.Exists(fullPath))
            {
                return await File.ReadAllTextAsync(fullPath, cancellationToken);
            }
        }

        return string.Empty;
    }
}
