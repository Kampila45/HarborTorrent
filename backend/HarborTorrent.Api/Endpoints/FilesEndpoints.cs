using HarborTorrent.Api.Contracts;
using HarborTorrent.Api.Security;
using HarborTorrent.Application.Abstractions.Persistence;
using HarborTorrent.Application.Abstractions.Runtime;

namespace HarborTorrent.Api.Endpoints;

internal static class FilesEndpoints
{
    public static IEndpointRouteBuilder MapFilesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/files");

        // Download a specific file by its path (verified against allowed download roots) or torrentId + fileIndex
        group.MapGet("/download", DownloadFileAsync);

        // Issue a short-lived scoped media token for browser <video> streaming
        group.MapPost("/stream-token", IssueStreamTokenAsync);

        // Stream a file directly from the torrent engine
        group.MapGet("/stream", StreamFileAsync);

        return app;
    }

    private static Task<IResult> IssueStreamTokenAsync(
        IssueStreamTokenRequest request,
        StreamTokenService tokenService,
        HttpContext httpContext)
    {
        if (request.TorrentId == Guid.Empty || request.FileIndex < 0)
        {
            return Task.FromResult(Results.BadRequest(ApiResponseFactory.Failure<object?>(
                httpContext,
                new[] { HarborTorrent.Domain.Common.Error.Validation("A valid torrentId and non-negative fileIndex are required.") })));
        }

        var (token, expiresAtUtc) = tokenService.IssueToken(request.TorrentId, request.FileIndex);
        return Task.FromResult(Results.Ok(ApiResponseFactory.Success(httpContext, new StreamTokenResponse(token, expiresAtUtc))));
    }

    private static async Task<IResult> DownloadFileAsync(
        string? path,
        Guid? torrentId,
        int? fileIndex,
        ITorrentRepository torrentRepository,
        ISettingsRepository settingsRepository,
        ITorrentRuntimeCoordinator coordinator,
        CancellationToken cancellationToken)
    {
        if (torrentId.HasValue && fileIndex.HasValue)
        {
            var torrent = await torrentRepository.GetByIdAsync(torrentId.Value, cancellationToken);
            if (torrent is null)
            {
                return Results.NotFound("Torrent not found.");
            }

            var files = await coordinator.GetFilesAsync(torrentId.Value, cancellationToken);
            var matchedFile = files.FirstOrDefault(f => f.Index == fileIndex.Value);
            if (matchedFile is null)
            {
                return Results.NotFound("File not found in the torrent.");
            }

            var resolvedPath = ResolveContainedPath(torrent.SavePath, matchedFile.Path);
            if (resolvedPath is null || !File.Exists(resolvedPath))
            {
                return Results.NotFound("File not found on disk.");
            }

            return ServeDiskFile(resolvedPath);
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            return Results.BadRequest("File path or torrentId/fileIndex is required.");
        }

        var settings = await settingsRepository.GetAsync(cancellationToken);
        var torrents = await torrentRepository.ListAsync(cancellationToken);

        var allowedRoots = torrents
            .Select(t => t.SavePath)
            .Append(settings.DefaultDownloadPath)
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var requestedFullPath = Path.GetFullPath(path);
        var isAllowed = allowedRoots.Any(root => IsSubPathOf(root, requestedFullPath));

        if (!isAllowed)
        {
            return Results.Forbid();
        }

        if (!File.Exists(requestedFullPath))
        {
            return Results.NotFound("File not found on disk.");
        }

        return ServeDiskFile(requestedFullPath);
    }

    private static IResult ServeDiskFile(string fullPath)
    {
        var fileName = Path.GetFileName(fullPath);
        var mimeType = GetMimeType(fileName);
        var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        return Results.File(fileStream, mimeType, fileName, enableRangeProcessing: true);
    }

    internal static string? ResolveContainedPath(string rootPath, string relativeFilePath)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || string.IsNullOrWhiteSpace(relativeFilePath))
        {
            return null;
        }

        var fullRoot = Path.GetFullPath(rootPath);
        var candidate = Path.GetFullPath(Path.Combine(fullRoot, relativeFilePath));
        return IsSubPathOf(fullRoot, candidate) ? candidate : null;
    }

    internal static bool IsSubPathOf(string rootPath, string candidatePath)
    {
        var fullRoot = Path.GetFullPath(rootPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullCandidate = Path.GetFullPath(candidatePath);

        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        var relative = Path.GetRelativePath(fullRoot, fullCandidate);
        if (string.Equals(relative, ".", StringComparison.Ordinal))
        {
            return true;
        }

        if (Path.IsPathRooted(relative))
        {
            return false;
        }

        return !relative.Equals("..", comparison)
            && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", comparison)
            && !relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", comparison);
    }

    private static async Task<IResult> StreamFileAsync(
        Guid torrentId,
        int fileIndex,
        ITorrentRuntimeCoordinator coordinator,
        CancellationToken cancellationToken)
    {
        var files = await coordinator.GetFilesAsync(torrentId, cancellationToken);
        var file = files.FirstOrDefault(f => f.Index == fileIndex);
        if (file is null)
        {
            return Results.NotFound("File not found in the torrent.");
        }

        var stream = await coordinator.CreateStreamAsync(torrentId, fileIndex, cancellationToken);
        if (stream == null)
        {
            return Results.NotFound("Could not create stream for this file.");
        }

        var fileName = Path.GetFileName(file.Path);
        var mimeType = GetMimeType(fileName);

        // Do NOT pass fileName as fileDownloadName, otherwise it forces 'Content-Disposition: attachment'
        return Results.File(stream, contentType: mimeType, enableRangeProcessing: true);
    }

    internal static string GetMimeType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".mp4" or ".m4v" => "video/mp4",
            ".webm" => "video/webm",
            ".mkv" => "video/x-matroska",
            ".avi" => "video/x-msvideo",
            ".mov" => "video/quicktime",
            ".ogg" or ".ogv" => "video/ogg",
            ".mp3" => "audio/mpeg",
            ".flac" => "audio/flac",
            ".wav" => "audio/wav",
            ".pdf" => "application/pdf",
            ".zip" => "application/zip",
            ".epub" => "application/epub+zip",
            _ => "application/octet-stream"
        };
    }
}

public sealed record IssueStreamTokenRequest(Guid TorrentId, int FileIndex);

public sealed record StreamTokenResponse(string Token, DateTimeOffset ExpiresAtUtc);
