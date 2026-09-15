using HarborTorrent.Application.Features.Dashboard.GetMetrics;
using MediatR;
using HarborTorrent.Api.Contracts;

namespace HarborTorrent.Api.Endpoints;

internal static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/dashboard");

        group.MapGet("/metrics", GetMetricsAsync);

        return app;
    }

    private static async Task<IResult> GetMetricsAsync(ISender sender, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var metrics = await sender.Send(new GetDashboardMetricsQuery(), cancellationToken);
        return Results.Json(ApiResponseFactory.Success(httpContext, metrics));
    }
}
