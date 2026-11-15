using FluentValidation;

namespace HarborTorrent.Application.Features.Settings.UpdateSettings;

public sealed class UpdateSettingsCommandValidator : AbstractValidator<UpdateSettingsCommand>
{
    public UpdateSettingsCommandValidator()
    {
        RuleFor(x => x.MaxDownloadSpeedBytes)
            .InclusiveBetween(0, int.MaxValue)
            .WithMessage($"Maximum download speed must be between 0 and {int.MaxValue} bytes/s.");

        RuleFor(x => x.MaxUploadSpeedBytes)
            .InclusiveBetween(0, int.MaxValue)
            .WithMessage($"Maximum upload speed must be between 0 and {int.MaxValue} bytes/s.");

        RuleFor(x => x.AnchorMaxDownloadSpeedBytes)
            .InclusiveBetween(0, int.MaxValue)
            .WithMessage($"Anchor maximum download speed must be between 0 and {int.MaxValue} bytes/s.");

        RuleFor(x => x.AnchorMaxUploadSpeedBytes)
            .InclusiveBetween(0, int.MaxValue)
            .WithMessage($"Anchor maximum upload speed must be between 0 and {int.MaxValue} bytes/s.");

        RuleFor(x => x.DefaultDownloadPath)
            .NotEmpty()
            .MaximumLength(1024)
            .WithMessage("Default download path is required.");
    }
}

