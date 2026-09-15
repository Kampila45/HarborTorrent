using HarborTorrent.Api.Contracts;
using HarborTorrent.Application.Features.Rss.AddRssFeed;
using HarborTorrent.Application.Features.Rss.AddRssFilter;
using HarborTorrent.Application.Features.Rss.DeleteRssFeed;
using HarborTorrent.Application.Features.Rss.DeleteRssFilter;
using HarborTorrent.Application.Features.Rss.GetRssFeeds;
using MediatR;

namespace HarborTorrent.Api.Endpoints;

internal static class RssEndpoints
{
    public static IEndpointRouteBuilder MapRssEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/rss");

        group.MapGet("/", GetFeedsAsync);
        group.MapPost("/", AddFeedAsync);
        group.MapDelete("/{id:guid}", DeleteFeedAsync);
        group.MapPost("/{id:guid}/filters", AddFilterAsync);
        group.MapDelete("/filters/{filterId:guid}", DeleteFilterAsync);

        return app;
    }

    private static async Task<IResult> GetFeedsAsync(ISender sender, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetRssFeedsQuery(), cancellationToken);
        return Results.Json(ApiResponseFactory.Success(httpContext, result));
    }

    private static async Task<IResult> AddFeedAsync(AddRssFeedCommand command, ISender sender, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var result = await sender.Send(command, cancellationToken);
        return Results.Json(ApiResponseFactory.Success(httpContext, result));
    }

    private static async Task<IResult> DeleteFeedAsync(Guid id, ISender sender, HttpContext httpContext, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteRssFeedCommand(id), cancellationToken);
        return Results.Json(ApiResponseFactory.Success<object?>(httpContext, null));
    }

    private static async Task<IResult> AddFilterAsync(Guid id, AddRssFilterRequest request, ISender sender, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var command = new AddRssFilterCommand(id, request.RegexPattern, request.SavePath);
        var result = await sender.Send(command, cancellationToken);
        return Results.Json(ApiResponseFactory.Success(httpContext, result));
    }

    private static async Task<IResult> DeleteFilterAsync(Guid filterId, ISender sender, HttpContext httpContext, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteRssFilterCommand(filterId), cancellationToken);
        return Results.Json(ApiResponseFactory.Success<object?>(httpContext, null));
    }
}

public record AddRssFilterRequest(string RegexPattern, string SavePath);
