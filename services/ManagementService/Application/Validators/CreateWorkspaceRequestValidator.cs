using FluentValidation;
using ManagementService.Contracts.Requests;

namespace ManagementService.Application.Validators;

/// <summary>
/// Валидатор для CreateWorkspaceRequest
/// </summary>
public class CreateWorkspaceRequestValidator : AbstractValidator<CreateWorkspaceRequest>
{
    public CreateWorkspaceRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Название рабочего пространства не может быть пустым")
            .MaximumLength(255)
            .WithMessage("Название не может быть длиннее 255 символов");

        RuleFor(x => x.Description)
            .MaximumLength(1000)
            .WithMessage("Описание не может быть длиннее 1000 символов")
            .When(x => x.Description != null);
    }
}
