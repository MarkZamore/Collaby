using FluentValidation;
using IdentityService.Contracts.Requests;
using IdentityService.Application.Validators;

namespace IdentityService.Contracts.Requests;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Nickname)
            .NotEmpty().WithMessage("Nickname is required")
            .MinimumLength(3).WithMessage("Nickname must be at least 3 characters")
            .MaximumLength(50).WithMessage("Nickname must not exceed 50 characters")
            .Matches("^[a-zA-Z0-9_]+$").WithMessage("Nickname can only contain letters, numbers, and underscores");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format")
            .MaximumLength(255).WithMessage("Email must not exceed 255 characters");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required")
            .Custom((password, context) =>
            {
                var (isValid, errors) = PasswordValidator.Validate(password);
                if (!isValid)
                {
                    foreach (var error in errors)
                    {
                        context.AddFailure(error);
                    }
                }
            });

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password).WithMessage("Passwords do not match");
    }
}