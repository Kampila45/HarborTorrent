using FluentValidation;
using System.Text.RegularExpressions;

namespace HarborTorrent.Application.Features.Torrents.AddTorrent;

internal sealed class AddTorrentCommandValidator : AbstractValidator<AddTorrentCommand>
{
    private static readonly Regex MagnetLinkPattern = new(
        @"^magnet:\?.*xt=urn:btih:([a-f0-9]{40}|[a-z2-7]{32})(?:&.*)?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public AddTorrentCommandValidator()
    {
        RuleFor(command => command.SavePath)
            .NotEmpty();

        RuleFor(command => command.MagnetLink)
            .Must(BeValidMagnetLink)
            .When(command => !string.IsNullOrWhiteSpace(command.MagnetLink))
            .WithMessage("The magnet link must include a valid btih info hash.");

        RuleFor(command => command.TorrentFileContentBase64)
            .Must(BeValidBase64)
            .When(command => !string.IsNullOrWhiteSpace(command.TorrentFileContentBase64))
            .WithMessage("The torrent file payload must be valid base64.");

        RuleFor(command => command)
            .Must(HaveExactlyOneSource)
            .WithMessage("Provide either a magnet link or a torrent file payload.");
    }

    private static bool HaveExactlyOneSource(AddTorrentCommand command)
    {
        var hasMagnetLink = !string.IsNullOrWhiteSpace(command.MagnetLink);
        var hasTorrentFile = !string.IsNullOrWhiteSpace(command.TorrentFileName) && !string.IsNullOrWhiteSpace(command.TorrentFileContentBase64);

        return hasMagnetLink ^ hasTorrentFile;
    }

    private static bool BeValidMagnetLink(string? magnetLink)
    {
        return !string.IsNullOrWhiteSpace(magnetLink) && MagnetLinkPattern.IsMatch(magnetLink.Trim());
    }

    private static bool BeValidBase64(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        try
        {
            _ = Convert.FromBase64String(content);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}