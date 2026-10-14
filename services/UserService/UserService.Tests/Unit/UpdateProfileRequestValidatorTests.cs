using FluentValidation.Results;
using UserService.Application.Validators;
using UserService.Contracts.Requests;
using Xunit;

namespace UserService.Tests.Unit;

public sealed class UpdateProfileRequestValidatorTests
{
    [Fact]
    public void Validate_ShouldAcceptAbsoluteHttpAvatarUrl()
    {
        var validator = new UpdateProfileRequestValidator();
        ValidationResult result = validator.Validate(new UpdateProfileRequest
        {
            AvatarUrl = "https://cdn.collaby.org/avatars/me.png"
        });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ShouldRejectRelativeAvatarUrl()
    {
        var validator = new UpdateProfileRequestValidator();
        ValidationResult result = validator.Validate(new UpdateProfileRequest
        {
            AvatarUrl = "/avatars/me.png"
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(UpdateProfileRequest.AvatarUrl));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    [InlineData("kk")]
    public void Validate_ShouldAcceptSupportedLanguages(string language)
    {
        var validator = new UpdateProfileRequestValidator();
        ValidationResult result = validator.Validate(new UpdateProfileRequest
        {
            Language = language
        });

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("es")]
    [InlineData("kk-KZ")]
    [InlineData("EN-US")]
    public void Validate_ShouldRejectUnsupportedLanguages(string language)
    {
        var validator = new UpdateProfileRequestValidator();
        ValidationResult result = validator.Validate(new UpdateProfileRequest
        {
            Language = language
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(UpdateProfileRequest.Language));
    }
}
