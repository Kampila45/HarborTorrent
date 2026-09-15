using HarborTorrent.Application.Contracts.Settings;
using HarborTorrent.Application.Features.Settings.GetSettings;
using HarborTorrent.Application.Features.Settings.UpdateSettings;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace HarborTorrent.Api.Endpoints;

public static class SettingsEndpoints
{
    public static void MapSettingsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/settings").WithTags("Settings");

        group.MapGet("/", async (ISender sender, CancellationToken cancellationToken) =>
        {
            var settings = await sender.Send(new GetSettingsQuery(), cancellationToken);
            return Results.Ok(settings);
        });

        group.MapPut("/", async ([FromBody] SettingsDto dto, ISender sender, CancellationToken cancellationToken) =>
        {
            var command = new UpdateSettingsCommand(
                dto.MaxDownloadSpeedBytes,
                dto.MaxUploadSpeedBytes,
                dto.DefaultDownloadPath,
                dto.EnableDht,
                dto.EnablePex,
                dto.EnableLpd,
                dto.AnchorModeEnabled,
                dto.AnchorMaxDownloadSpeedBytes,
                dto.AnchorMaxUploadSpeedBytes
            );

            await sender.Send(command, cancellationToken);
            return Results.NoContent();
        });
    }
}
