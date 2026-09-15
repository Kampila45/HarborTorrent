namespace HarborTorrent.Api.Endpoints;

internal static class FilesEndpoints
{
    public static IEndpointRouteBuilder MapFilesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/files");

        // Download a specific file by its full server path
        group.MapGet("/download", DownloadFileAsync);

        // Stream a file directly from the torrent engine
        group.MapGet("/stream", StreamFileAsync);

        return app;
    }

    private static Task<IResult> DownloadFileAsync(string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
            return Task.FromResult(Results.BadRequest("File path is required."));

        // Security: ensure the path stays within /app/downloads and is not a path traversal attack
        var downloadsRoot = Path.GetFullPath("/app/downloads");
        var requestedPath = Path.GetFullPath(path);

        if (!requestedPath.StartsWith(downloadsRoot, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(Results.Forbid());

        if (!File.Exists(requestedPath))
            return Task.FromResult(Results.NotFound("File not found on server."));

        var fileName = Path.GetFileName(requestedPath);
        var mimeType = GetMimeType(fileName);
        var fileStream = new FileStream(requestedPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);

        return Task.FromResult(Results.File(fileStream, mimeType, fileName, enableRangeProcessing: true));
    }

    private static async Task<IResult> StreamFileAsync(
        Guid torrentId, 
        int fileIndex, 
        HarborTorrent.Application.Abstractions.Runtime.ITorrentRuntimeCoordinator coordinator, 
        CancellationToken cancellationToken)
    {
        var files = await coordinator.GetFilesAsync(torrentId, cancellationToken);
        if (files == null || fileIndex < 0 || fileIndex >= files.Count)
        {
            return Results.NotFound("File not found in the torrent.");
        }

        var stream = await coordinator.CreateStreamAsync(torrentId, fileIndex, cancellationToken);
        if (stream == null)
        {
            return Results.NotFound("Could not create stream for this file.");
        }

        var file = files.First(f => f.Index == fileIndex);
        var fileName = Path.GetFileName(file.Path);
        var mimeType = GetMimeType(fileName);

        // Do NOT pass fileName as fileDownloadName, otherwise it forces 'Content-Disposition: attachment'
        return Results.File(stream, contentType: mimeType, enableRangeProcessing: true);
    }

    private static string GetMimeType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".mp4" => "video/mp4",
            ".mkv" => "video/x-matroska",
            ".avi" => "video/x-msvideo",
            ".mp3" => "audio/mpeg",
            ".flac" => "audio/flac",
            ".pdf" => "application/pdf",
            ".zip" => "application/zip",
            ".epub" => "application/epub+zip",
            _ => "application/octet-stream"
        };
    }
}
