using FluentValidation.Results;
using MessengerService.Application.Validators;
using MessengerService.Contracts.Requests;
using Xunit;

namespace MessengerService.Tests.Unit;

public sealed class SendMessageRequestValidatorTests
{
    [Fact]
    public void Validate_ShouldRequireFilePayloadForFileMessages()
    {
        var validator = new SendMessageRequestValidator();
        ValidationResult result = validator.Validate(new SendMessageRequest
        {
            Type = "file",
            File = null
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(SendMessageRequest.File));
    }
}
