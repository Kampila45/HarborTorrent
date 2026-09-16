using HarborTorrent.Domain.Common;

namespace HarborTorrent.Domain.Torrents;

/// <summary>
/// Models a managed torrent and enforces the lifecycle invariants around starting, pausing, stopping, and completion.
/// </summary>
public sealed class Torrent
{
    private Torrent()
    {
        InfoHash = string.Empty;
        Name = string.Empty;
        SavePath = string.Empty;
        UserId = string.Empty;
        Source = null!;
    }

    private Torrent(Guid id, string infoHash, string name, string savePath, string userId, TorrentSource source, DateTimeOffset createdAtUtc)
    {
        Id = id;
        InfoHash = infoHash;
        Name = name;
        SavePath = savePath;
        UserId = userId;
        Source = source;
        Status = TorrentStatus.Queued;
        AddedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public string InfoHash { get; private set; }

    public string Name { get; private set; }

    public string UserId { get; private set; }

    public TorrentStatus Status { get; private set; }

    public decimal Progress { get; private set; }

    public long DownloadedBytes { get; private set; }

    public long UploadedBytes { get; private set; }

    public long DownloadSpeedBytesPerSecond { get; private set; }

    public long UploadSpeedBytesPerSecond { get; private set; }

    public long? EtaSeconds { get; private set; }

    public string SavePath { get; private set; }

    public DateTimeOffset AddedAtUtc { get; private set; }

    public DateTimeOffset? StartedAtUtc { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public decimal Ratio { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public string? ErrorMessage { get; private set; }

    public TorrentSource Source { get; private set; }

    public static Result<Torrent> Create(string infoHash, string? name, string savePath, string userId, TorrentSource source, DateTimeOffset createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Result<Torrent>.Failure(Error.Validation("A user ID is required."));
        }

        if (string.IsNullOrWhiteSpace(infoHash))
        {
            return Result<Torrent>.Failure(Error.Validation("A torrent info hash is required."));
        }

        if (string.IsNullOrWhiteSpace(savePath))
        {
            return Result<Torrent>.Failure(Error.Validation("A save path is required."));
        }

        var resolvedName = string.IsNullOrWhiteSpace(name) ? source.GetDisplayName() : name.Trim();

        if (string.IsNullOrWhiteSpace(resolvedName))
        {
            return Result<Torrent>.Failure(Error.Validation("A torrent name could not be determined."));
        }

        return Result<Torrent>.Success(new Torrent(Guid.NewGuid(), infoHash.Trim(), resolvedName, savePath.Trim(), userId.Trim(), source, createdAtUtc));
    }

    public Result Start(DateTimeOffset updatedAtUtc)
    {
        if (Status == TorrentStatus.Downloading || Status == TorrentStatus.Seeding)
        {
            return Result.Failure(Error.Conflict("The torrent is already active."));
        }

        Status = Progress >= 100m ? TorrentStatus.Seeding : TorrentStatus.Downloading;
        StartedAtUtc ??= updatedAtUtc;
        ErrorMessage = null;
        UpdatedAtUtc = updatedAtUtc;

        return Result.Success();
    }

    public Result Pause(DateTimeOffset updatedAtUtc)
    {
        if (Status != TorrentStatus.Downloading && Status != TorrentStatus.Seeding)
        {
            return Result.Failure(Error.Conflict("Only active torrents can be paused."));
        }

        Status = TorrentStatus.Paused;
        DownloadSpeedBytesPerSecond = 0;
        UploadSpeedBytesPerSecond = 0;
        EtaSeconds = null;
        UpdatedAtUtc = updatedAtUtc;

        return Result.Success();
    }

    public Result Stop(DateTimeOffset updatedAtUtc)
    {
        if (Status != TorrentStatus.Downloading && Status != TorrentStatus.Seeding && Status != TorrentStatus.Paused && Status != TorrentStatus.Queued)
        {
            return Result.Failure(Error.Conflict("The torrent cannot be stopped from its current state."));
        }

        Status = TorrentStatus.Stopped;
        DownloadSpeedBytesPerSecond = 0;
        UploadSpeedBytesPerSecond = 0;
        EtaSeconds = null;
        UpdatedAtUtc = updatedAtUtc;

        return Result.Success();
    }

    public Result Complete(DateTimeOffset updatedAtUtc)
    {
        if (Status != TorrentStatus.Seeding)
        {
            return Result.Failure(Error.Conflict("Only seeding torrents can be completed."));
        }

        Status = TorrentStatus.Completed;
        DownloadSpeedBytesPerSecond = 0;
        UploadSpeedBytesPerSecond = 0;
        EtaSeconds = 0;
        UpdatedAtUtc = updatedAtUtc;

        return Result.Success();
    }

    public void UpdateProgress(
        decimal progress,
        long downloadedBytes,
        long uploadedBytes,
        long downloadSpeedBytesPerSecond,
        long uploadSpeedBytesPerSecond,
        decimal ratio,
        long? etaSeconds,
        DateTimeOffset updatedAtUtc)
    {
        var normalizedProgress = decimal.Clamp(progress, 0m, 100m);

        Progress = normalizedProgress;
        DownloadedBytes = Math.Max(0, downloadedBytes);
        UploadedBytes = Math.Max(0, uploadedBytes);
        DownloadSpeedBytesPerSecond = downloadSpeedBytesPerSecond;
        UploadSpeedBytesPerSecond = uploadSpeedBytesPerSecond;
        Ratio = ratio < 0m ? 0m : ratio;
        EtaSeconds = normalizedProgress >= 100m ? 0 : etaSeconds;
        UpdatedAtUtc = updatedAtUtc;

        if (normalizedProgress >= 100m && Status == TorrentStatus.Downloading)
        {
            Status = TorrentStatus.Seeding;
        }

        if (normalizedProgress >= 100m)
        {
            CompletedAtUtc ??= updatedAtUtc;
            DownloadSpeedBytesPerSecond = 0;
            EtaSeconds = 0;
        }
    }

    public void SetError(string errorMessage, DateTimeOffset updatedAtUtc)
    {
        ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? null : errorMessage.Trim();
        UpdatedAtUtc = updatedAtUtc;
    }
}