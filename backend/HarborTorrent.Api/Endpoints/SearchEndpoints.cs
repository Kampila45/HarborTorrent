using HarborTorrent.Application.Features.Search.SearchTorrents;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace HarborTorrent.Api.Endpoints;

public static class SearchEndpoints
{
    public static void MapSearchEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/search").WithTags("Search");

        group.MapGet("/", async ([FromQuery] string q, ISender sender, CancellationToken cancellationToken) =>
        {
            var results = await sender.Send(new SearchTorrentsQuery(q ?? string.Empty), cancellationToken);
            return Results.Ok(results);
        });
    }
}
