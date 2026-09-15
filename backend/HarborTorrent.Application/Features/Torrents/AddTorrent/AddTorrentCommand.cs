using HarborTorrent.Application.Abstractions.Persistence;
using HarborTorrent.Application.Abstractions.Realtime;
using HarborTorrent.Application.Abstractions.Torrents;
using HarborTorrent.Application.Contracts.Torrents;
using HarborTorrent.Application.Features.Torrents.Common;
using HarborTorrent.Domain.Common;
using HarborTorrent.Domain.Torrents;
using MediatR;
using Microsoft.Extensions.Configuration;

namespace HarborTorrent.Application.Features.Torrents.AddTorrent;

public sealed record AddTorrentCommand(
    string? Name,
    string? SavePath,
    string? MagnetLink,
    string? TorrentFileName,
    string? TorrentFileContentBase64) : IRequest<Result<TorrentDto>>;

internal sealed class AddTorrentCommandHandler : IRequestHandler<AddTorrentCommand, Result<TorrentDto>>
{
    // Single-user desktop app — no authentication required.
    // All torrents are owned by the local user.
    private const string LocalUserId = "local";

    private readonly ITorrentRepository _torrentRepository;
    private readonly ITorrentEventPublisher _eventPublisher;
    private readonly ITorrentSourceMetadataProvider _torrentSourceMetadataProvider;
    private readonly TimeProvider _timeProvider;
    private readonly IConfiguration _configuration;

    public AddTorrentCommandHandler(
        ITorrentRepository torrentRepository,
        ITorrentEventPublisher eventPublisher,
        ITorrentSourceMetadataProvider torrentSourceMetadataProvider,
        TimeProvider timeProvider,
        IConfiguration configuration)
    {
        _torrentRepository = torrentRepository;
        _eventPublisher = eventPublisher;
        _torrentSourceMetadataProvider = torrentSourceMetadataProvider;
        _timeProvider = timeProvider;
        _configuration = configuration;
    }

    public async Task<Result<TorrentDto>> Handle(AddTorrentCommand request, CancellationToken cancellationToken)
    {
        var sourceResult = !string.IsNullOrWhiteSpace(request.MagnetLink)
            ? TorrentSource.FromMagnetLink(request.MagnetLink)
            : TorrentSource.FromTorrentFile(request.TorrentFileName ?? string.Empty, request.TorrentFileContentBase64 ?? string.Empty);

        if (sourceResult.IsFailure)
        {
            return Result<TorrentDto>.Failure(sourceResult.Errors);
        }

        var metadataResult = _torrentSourceMetadataProvider.GetMetadata(sourceResult.Value!);

        if (metadataResult.IsFailure)
        {
            return Result<TorrentDto>.Failure(metadataResult.Errors);
        }

        if (await _torrentRepository.ExistsByInfoHashAsync(metadataResult.Value!.InfoHash, cancellationToken))
        {
            return Result<TorrentDto>.Failure(Error.Conflict($"Torrent '{sourceResult.Value!.GetDisplayName()}' is already registered."));
        }

        // Use the provided save path, or fall back to the configured default
        var savePath = !string.IsNullOrWhiteSpace(request.SavePath)
            ? request.SavePath
            : _configuration["DefaultSavePath"] ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "HarborDownloads");

        var torrentResult = Torrent.Create(
            metadataResult.Value!.InfoHash,
            request.Name,
            savePath,
            LocalUserId,
            sourceResult.Value!,
            _timeProvider.GetUtcNow());

        if (torrentResult.IsFailure)
        {
            return Result<TorrentDto>.Failure(torrentResult.Errors);
        }

        var torrent = torrentResult.Value!;
        await _torrentRepository.AddAsync(torrent, cancellationToken);
        await _torrentRepository.SaveChangesAsync(cancellationToken);

        var dto = torrent.ToDto();
        await _eventPublisher.PublishAsync(TorrentEventType.Added, dto, cancellationToken);

        return Result<TorrentDto>.Success(dto);
    }
}