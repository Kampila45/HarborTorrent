using HarborTorrent.Api.Contracts;
using HarborTorrent.Api.Contracts.Torrents;
using HarborTorrent.Application.Contracts.Torrents;
using HarborTorrent.Application.Features.Torrents.AddTorrent;
using HarborTorrent.Application.Features.Torrents.GetTorrent;
using HarborTorrent.Application.Features.Torrents.ListTorrents;
using HarborTorrent.Application.Features.Torrents.PauseTorrent;
using HarborTorrent.Application.Features.Torrents.RemoveTorrent;
using HarborTorrent.Application.Features.Torrents.StartTorrent;
using HarborTorrent.Application.Features.Torrents.StopTorrent;
using HarborTorrent.Domain.Common;
using MediatR;

namespace HarborTorrent.Api.Endpoints;

internal static class TorrentEndpoints
{
    public static IEndpointRouteBuilder MapTorrentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/torrents");

        group.MapGet("/", GetTorrentsAsync);
        group.MapGet("/{torrentId:guid}", GetTorrentAsync);
        group.MapGet("/{torrentId:guid}/files", GetTorrentFilesAsync);
        group.MapPut("/{torrentId:guid}/files/priority", UpdateFilePriorityAsync);
        group.MapPost("/", AddTorrentAsync);
        group.MapPost("/{torrentId:guid}/start", StartTorrentAsync);
        group.MapPost("/{torrentId:guid}/pause", PauseTorrentAsync);
        group.MapPost("/{torrentId:guid}/stop", StopTorrentAsync);
        group.MapDelete("/{torrentId:guid}", RemoveTorrentAsync);
        group.MapGet("/{torrentId:guid}/peers", GetTorrentPeersAsync);
        group.MapGet("/{torrentId:guid}/trackers", GetTorrentTrackersAsync);
        group.MapPost("/{torrentId:guid}/trackers", AddTrackerAsync);

        return app;
    }

    private static async Task<IResult> GetTorrentsAsync(int? page, int? pageSize, string? status, string? q, ISender sender, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var torrents = await sender.Send(new ListTorrentsQuery(page ?? 1, pageSize ?? 10, status, q), cancellationToken);
        return Results.Json(ApiResponseFactory.Success(httpContext, torrents));
    }

    private static async Task<IResult> GetTorrentAsync(Guid torrentId, ISender sender, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetTorrentQuery(torrentId), cancellationToken);
        return result.ToHttpResult(httpContext);
    }

    private static async Task<IResult> GetTorrentFilesAsync(Guid torrentId, ISender sender, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var files = await sender.Send(new HarborTorrent.Application.Features.Torrents.GetTorrentFiles.GetTorrentFilesQuery(torrentId), cancellationToken);
        return Results.Json(ApiResponseFactory.Success(httpContext, files));
    }

    private static async Task<IResult> UpdateFilePriorityAsync(Guid torrentId, HarborTorrent.Application.Contracts.Torrents.FilePriorityUpdateDto[] updates, ISender sender, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new HarborTorrent.Application.Features.Torrents.UpdateFilesPriority.UpdateTorrentFilesPriorityCommand(torrentId, updates), cancellationToken);
        return result.ToHttpResult(httpContext);
    }

    private static async Task<IResult> AddTorrentAsync(CreateTorrentRequest request, ISender sender, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new AddTorrentCommand(
                request.Name,
                request.SavePath,
                request.MagnetLink,
                request.TorrentFileName,
                request.TorrentFileContentBase64),
            cancellationToken);

        return result.ToHttpResult(httpContext, StatusCodes.Status201Created);
    }

    private static async Task<IResult> StartTorrentAsync(Guid torrentId, ISender sender, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new StartTorrentCommand(torrentId), cancellationToken);
        return result.ToHttpResult(httpContext);
    }

    private static async Task<IResult> PauseTorrentAsync(Guid torrentId, ISender sender, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new PauseTorrentCommand(torrentId), cancellationToken);
        return result.ToHttpResult(httpContext);
    }

    private static async Task<IResult> StopTorrentAsync(Guid torrentId, ISender sender, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new StopTorrentCommand(torrentId), cancellationToken);
        return result.ToHttpResult(httpContext);
    }

    private static async Task<IResult> RemoveTorrentAsync(Guid torrentId, ISender sender, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new RemoveTorrentCommand(torrentId), cancellationToken);
        return result.ToHttpResult(httpContext, payload: new { TorrentId = torrentId });
    }

    private static async Task<IResult> GetTorrentPeersAsync(Guid torrentId, ISender sender, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new HarborTorrent.Application.Features.Torrents.GetTorrentPeers.GetTorrentPeersQuery(torrentId), cancellationToken);
        return result.ToHttpResult(httpContext);
    }

    private static async Task<IResult> GetTorrentTrackersAsync(Guid torrentId, ISender sender, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new HarborTorrent.Application.Features.Torrents.GetTorrentTrackers.GetTorrentTrackersQuery(torrentId), cancellationToken);
        return result.ToHttpResult(httpContext);
    }

    private static async Task<IResult> AddTrackerAsync(Guid torrentId, [Microsoft.AspNetCore.Mvc.FromBody] TrackerUrlRequest request, ISender sender, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new HarborTorrent.Application.Features.Torrents.AddTracker.AddTrackerCommand(torrentId, request.Url), cancellationToken);
        return result.ToHttpResult(httpContext);
    }
}

public sealed record TrackerUrlRequest(string Url);