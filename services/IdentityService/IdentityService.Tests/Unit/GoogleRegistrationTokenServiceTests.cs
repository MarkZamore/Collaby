using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using IdentityService.Application.Services;
using IdentityService.Application.Settings;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace IdentityService.Tests.Unit;

public sealed class GoogleRegistrationTokenServiceTests
{
    private static readonly JwtSettings JwtSettings = new()
    {
        Issuer = "identity-tests",
        Audience = "identity-tests",
        SigningKey = "01234567890123456789012345678901",
        AccessTokenLifetimeMinutes = 60
    };

    [Fact]
    public void GenerateToken_AndValidateToken_ShouldRoundTripPayload()
    {
        var service = CreateService();
        var token = service.GenerateToken(new GoogleRegistrationTokenPayload
        {
            Subject = "google-subject",
            Email = "user@collaby.test",
            EmailVerified = true,
            Name = "New User",
            Picture = "https://example.test/picture.png",
            Locale = "en"
        });

        var payload = service.ValidateToken(token);

        Assert.Equal("google-subject", payload.Subject);
        Assert.Equal("user@collaby.test", payload.Email);
        Assert.True(payload.EmailVerified);
        Assert.Equal("New User", payload.Name);
        Assert.Equal("https://example.test/picture.png", payload.Picture);
        Assert.Equal("en", payload.Locale);
    }

    [Fact]
    public void ValidateToken_ShouldRejectExpiredToken()
    {
        var service = CreateService();
        var token = CreateExpiredToken();

        Assert.Throws<SecurityTokenExpiredException>(() => service.ValidateToken(token));
    }

    private static GoogleRegistrationTokenService CreateService()
    {
        return new GoogleRegistrationTokenService(Options.Create(JwtSettings));
    }

    private static string CreateExpiredToken()
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSettings.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: JwtSettings.Issuer,
            audience: JwtSettings.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, "expired-subject"),
                new Claim(JwtRegisteredClaimNames.Email, "expired@collaby.test"),
                new Claim("purpose", "google-registration"),
                new Claim("email_verified", bool.TrueString)
            ],
            notBefore: DateTime.UtcNow.AddMinutes(-30),
            expires: DateTime.UtcNow.AddMinutes(-5),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
