using FluentValidation;
using UserService.Contracts.Requests;

namespace UserService.Application.Validators;

public class UpdatePreferencesRequestValidator : AbstractValidator<UpdatePreferencesRequest>
{
    private static readonly string[] AllowedThemes = ["light", "dark", "system"];
    private static readonly string[] AllowedBackgroundTypes = ["color", "image", "upload"];

    public UpdatePreferencesRequestValidator()
    {
        RuleFor(x => x.Theme)
            .Must(theme => theme is null || AllowedThemes.Contains(theme, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Theme must be one of: light, dark, system");

        When(x => x.DefaultBoardBackground is not null, () =>
        {
            RuleFor(x => x.DefaultBoardBackground!.Type)
                .Must(type => !string.IsNullOrWhiteSpace(type) && AllowedBackgroundTypes.Contains(type, StringComparer.OrdinalIgnoreCase))
                .WithMessage("DefaultBoardBackground.Type must be one of: color, image, upload");

            RuleFor(x => x.DefaultBoardBackground!.Value)
                .Must(value => !string.IsNullOrWhiteSpace(value))
                .WithMessage("DefaultBoardBackground.Value is required when default board background is provided");
        });
    }
}

