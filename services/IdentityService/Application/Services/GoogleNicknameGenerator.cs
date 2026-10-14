using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using IdentityService.Application.Interfaces;

namespace IdentityService.Application.Services;

public sealed class GoogleNicknameGenerator : IGoogleNicknameGenerator
{
    private const int MaxNicknameLength = 50;

    public string GenerateBaseNickname(string? name, string email, string subject)
    {
        var candidates = new[]
        {
            Normalize(name),
            Normalize(GetEmailLocalPart(email)),
            GenerateFallback(subject, email)
        };

        return candidates.First(candidate => candidate.Length >= 3);
    }

    public string GenerateSuffix(string subject, string email, int length = 6)
    {
        var raw = $"{subject}:{email}".ToLowerInvariant();
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant()[..Math.Clamp(length, 4, 12)];
    }

    private static string GetEmailLocalPart(string email)
    {
        var separatorIndex = email.IndexOf('@');
        return separatorIndex > 0 ? email[..separatorIndex] : email;
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        var pendingUnderscore = false;

        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (character is >= 'A' and <= 'Z')
            {
                if (pendingUnderscore && builder.Length > 0)
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(character));
                pendingUnderscore = false;
                continue;
            }

            if (character is >= 'a' and <= 'z' || character is >= '0' and <= '9')
            {
                if (pendingUnderscore && builder.Length > 0)
                {
                    builder.Append('_');
                }

                builder.Append(character);
                pendingUnderscore = false;
                continue;
            }

            if (character == '_' && builder.Length > 0)
            {
                pendingUnderscore = true;
                continue;
            }

            if (char.IsWhiteSpace(character) || character is '-' or '.' or '+')
            {
                pendingUnderscore = builder.Length > 0;
            }
        }

        var result = builder.ToString().Trim('_');
        if (result.Length > MaxNicknameLength)
        {
            result = result[..MaxNicknameLength].TrimEnd('_');
        }

        return result;
    }

    private string GenerateFallback(string subject, string email)
    {
        return $"collaby_{GenerateSuffix(subject, email)}";
    }
}
