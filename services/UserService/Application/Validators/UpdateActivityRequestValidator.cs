using FluentValidation;
using UserService.Contracts.Requests;

namespace UserService.Application.Validators;

public class UpdateActivityRequestValidator : AbstractValidator<UpdateActivityRequest>
{
    public UpdateActivityRequestValidator()
    {
        RuleFor(x => x.TaskCompletedCount)
            .GreaterThanOrEqualTo(0)
            .When(x => x.TaskCompletedCount.HasValue);

        RuleFor(x => x.CommentCount)
            .GreaterThanOrEqualTo(0)
            .When(x => x.CommentCount.HasValue);

        RuleFor(x => x.WorkspaceVisitCount)
            .GreaterThanOrEqualTo(0)
            .When(x => x.WorkspaceVisitCount.HasValue);
    }
}

