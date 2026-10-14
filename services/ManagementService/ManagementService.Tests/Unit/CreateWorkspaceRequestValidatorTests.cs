using FluentValidation.Results;
using ManagementService.Application.Validators;
using ManagementService.Contracts.Requests;
using Xunit;

namespace ManagementService.Tests.Unit;

public sealed class CreateWorkspaceRequestValidatorTests
{
    [Fact]
    public void Validate_ShouldRejectEmptyWorkspaceName()
    {
        var validator = new CreateWorkspaceRequestValidator();
        ValidationResult result = validator.Validate(new CreateWorkspaceRequest
        {
            Name = string.Empty
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateWorkspaceRequest.Name));
    }
}
