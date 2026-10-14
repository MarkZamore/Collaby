using CallService.Application.Validators;
using CallService.Contracts.Requests;
using FluentValidation.Results;
using Xunit;

namespace CallService.Tests.Unit;

public sealed class StartCallRequestValidatorTests
{
    [Fact]
    public void Validate_ShouldRejectUnsupportedCallType()
    {
        var validator = new StartCallRequestValidator();
        ValidationResult result = validator.Validate(new StartCallRequest
        {
            Type = "screen-share-only",
            ParticipantIds = { Guid.NewGuid() }
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(StartCallRequest.Type));
    }
}
