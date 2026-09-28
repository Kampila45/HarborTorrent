using HarborTorrent.Application.Features.System.GetChangelog;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Threading;
using System.Threading.Tasks;

namespace HarborTorrent.Api.Endpoints;

internal static class SystemEndpoints
{
    public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/system");

        group.MapGet("/changelog", GetChangelogAsync);
        group.MapGet("/version", GetVersionAsync);

        return app;
    }

    private static async Task<IResult> GetChangelogAsync(ISender sender, CancellationToken cancellationToken)
    {
        var changelog = await sender.Send(new GetChangelogQuery(), cancellationToken);
        return Results.Ok(new { changelog });
    }

    private static Task<IResult> GetVersionAsync()
    {
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
        return Task.FromResult(Results.Ok(new { version = version, latestVersion = version }));
    }

}
