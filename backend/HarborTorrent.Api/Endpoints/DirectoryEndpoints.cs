using HarborTorrent.Application.Features.Directories.ListDirectories;
using MediatR;

namespace HarborTorrent.Api.Endpoints;

internal static class DirectoryEndpoints
{
    public static IEndpointRouteBuilder MapDirectoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/directories");

        group.MapGet("/", GetDirectoriesAsync);

        return app;
    }

    private static async Task<IResult> GetDirectoriesAsync(string? path, ISender sender, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ListDirectoriesQuery(path), cancellationToken);
        return result.ToHttpResult(httpContext);
    }
}
