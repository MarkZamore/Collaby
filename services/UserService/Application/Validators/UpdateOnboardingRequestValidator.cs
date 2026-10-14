using FluentValidation;
using UserService.Contracts.Requests;
using UserService.Domain.Entities;

namespace UserService.Application.Validators;

public class UpdateOnboardingRequestValidator : AbstractValidator<UpdateOnboardingRequest>
{
    private static readonly string[] AllowedStatuses =
    [
        UserDefaults.OnboardingNotStarted,
        UserDefaults.OnboardingInProgress,
        UserDefaults.OnboardingCompleted
    ];

    public UpdateOnboardingRequestValidator()
    {
        RuleFor(x => x.Status)
            .Must(status => status is null || AllowedStatuses.Contains(status, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Status must be one of: {string.Join(", ", AllowedStatuses)}");

        RuleFor(x => x.CurrentStep)
            .InclusiveBetween(0, 100)
            .When(x => x.CurrentStep.HasValue);
    }
}

