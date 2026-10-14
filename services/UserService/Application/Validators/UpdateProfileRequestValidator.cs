using FluentValidation;
using UserService.Contracts.Requests;

namespace UserService.Application.Validators;

public class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    private static readonly string[] AllowedLanguages = ["en", "ru", "kk"];

    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.DisplayName)
            .MaximumLength(120)
            .When(x => x.DisplayName is not null);

        RuleFor(x => x.AvatarUrl)
            .MaximumLength(1024)
            .Must(BeValidUrl)
            .When(x => !string.IsNullOrWhiteSpace(x.AvatarUrl))
            .WithMessage("AvatarUrl must be a valid absolute URL");

        RuleFor(x => x.Bio)
            .MaximumLength(2000)
            .When(x => x.Bio is not null);

        RuleFor(x => x.Timezone)
            .MaximumLength(100)
            .When(x => x.Timezone is not null);

        RuleFor(x => x.Language)
            .Must(language => language is null || AllowedLanguages.Contains(language.Trim().ToLowerInvariant()))
            .WithMessage("Language must be one of: en, ru, kk")
            .When(x => !string.IsNullOrWhiteSpace(x.Language));
    }

    private static bool BeValidUrl(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}

