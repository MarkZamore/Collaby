using FluentValidation;

namespace IdentityService.Contracts.Requests;

public sealed class CompleteGoogleRegistrationRequestValidator : AbstractValidator<CompleteGoogleRegistrationRequest>
{
    public CompleteGoogleRegistrationRequestValidator()
    {
        RuleFor(x => x.RegistrationToken)
            .NotEmpty().WithMessage("Registration token is required");

        RuleFor(x => x.Nickname)
            .NotEmpty().WithMessage("Nickname is required")
            .MinimumLength(3).WithMessage("Nickname must be at least 3 characters")
            .MaximumLength(50).WithMessage("Nickname must not exceed 50 characters")
            .Matches("^[a-zA-Z0-9_]+$").WithMessage("Nickname can only contain letters, numbers, and underscores");
    }
}
