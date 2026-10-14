using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using IdentityService.Application.Interfaces;
using IdentityService.Application.Settings;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace IdentityService.Application.Services;

public sealed class GoogleRegistrationTokenService : IGoogleRegistrationTokenService
{
    private const string TokenPurpose = "google-registration";
    private readonly JwtSettings _jwtSettings;

    public GoogleRegistrationTokenService(IOptions<JwtSettings> jwtSettingsOptions)
    {
        _jwtSettings = jwtSettingsOptions.Value;
    }

    public string GenerateToken(GoogleRegistrationTokenPayload payload)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, payload.Subject),
            new(JwtRegisteredClaimNames.Email, payload.Email),
            new("purpose", TokenPurpose),
            new("email_verified", payload.EmailVerified ? bool.TrueString : bool.FalseString)
        };

        if (!string.IsNullOrWhiteSpace(payload.Name))
        {
            claims.Add(new Claim("name", payload.Name));
        }

        if (!string.IsNullOrWhiteSpace(payload.Picture))
        {
            claims.Add(new Claim("picture", payload.Picture));
        }

        if (!string.IsNullOrWhiteSpace(payload.Locale))
        {
            claims.Add(new Claim("locale", payload.Locale));
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public GoogleRegistrationTokenPayload ValidateToken(string token)
    {
        var principal = new JwtSecurityTokenHandler().ValidateToken(
            token,
            new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SigningKey)),
                ValidateIssuer = true,
                ValidIssuer = _jwtSettings.Issuer,
                ValidateAudience = true,
                ValidAudience = _jwtSettings.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(5)
            },
            out var validatedToken);

        if (validatedToken is not JwtSecurityToken jwtToken ||
            !jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityTokenException("Invalid registration token");
        }

        var claims = jwtToken.Claims.ToList();
        var purpose = claims.FirstOrDefault(claim => claim.Type == "purpose")?.Value;
        if (!string.Equals(purpose, TokenPurpose, StringComparison.Ordinal))
        {
            throw new SecurityTokenException("Invalid registration token purpose");
        }

        var subject = claims.FirstOrDefault(claim => claim.Type == JwtRegisteredClaimNames.Sub)?.Value;
        var email = claims.FirstOrDefault(claim => claim.Type == JwtRegisteredClaimNames.Email)?.Value;
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(email))
        {
            throw new SecurityTokenException("Registration token is missing required claims");
        }

        return new GoogleRegistrationTokenPayload
        {
            Subject = subject,
            Email = email,
            EmailVerified = bool.TryParse(claims.FirstOrDefault(claim => claim.Type == "email_verified")?.Value, out var emailVerified) && emailVerified,
            Name = claims.FirstOrDefault(claim => claim.Type == "name")?.Value,
            Picture = claims.FirstOrDefault(claim => claim.Type == "picture")?.Value,
            Locale = claims.FirstOrDefault(claim => claim.Type == "locale")?.Value
        };
    }
}
