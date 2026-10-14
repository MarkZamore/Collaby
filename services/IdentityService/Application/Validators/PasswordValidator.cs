using System.Text.RegularExpressions;

namespace IdentityService.Application.Validators;

/// <summary>
/// Валидатор для проверки сложности пароля
/// Требования:
/// - Минимум 8 символов
/// - Хотя бы одна прописная буква (A-Z)
/// - Хотя бы одна строчная буква (a-z)
/// - Хотя бы одна цифра (0-9)
/// - Хотя бы один спецсимвол (!@#$%^&*)
/// </summary>
public static class PasswordValidator
{
    private const int MinLength = 8;
    private const int MaxLength = 128;
    
    // Regex-паттерны для каждого требования к паролю.
    private static readonly Regex HasUpperCase = new Regex(@"[A-Z]", RegexOptions.Compiled);
    private static readonly Regex HasLowerCase = new Regex(@"[a-z]", RegexOptions.Compiled);
    private static readonly Regex HasDigit = new Regex(@"[0-9]", RegexOptions.Compiled);
    private static readonly Regex HasSpecialChar = new Regex(@"[!@#$%^&*()_+\-=\[\]{};:""'<>?.,/\\|`~]", RegexOptions.Compiled);

    /// <summary>
    /// Валидировать пароль
    /// </summary>
    public static (bool IsValid, string[] Errors) Validate(string password)
    {
        var errors = new List<string>();

        if (string.IsNullOrEmpty(password))
        {
            errors.Add("Password is required");
            return (false, errors.ToArray());
        }

        if (password.Length < MinLength)
        {
            errors.Add($"Password must be at least {MinLength} characters long");
        }

        if (password.Length > MaxLength)
        {
            errors.Add($"Password must not exceed {MaxLength} characters");
        }

        if (!HasUpperCase.IsMatch(password))
        {
            errors.Add("Password must contain at least one uppercase letter (A-Z)");
        }

        if (!HasLowerCase.IsMatch(password))
        {
            errors.Add("Password must contain at least one lowercase letter (a-z)");
        }

        if (!HasDigit.IsMatch(password))
        {
            errors.Add("Password must contain at least one digit (0-9)");
        }

        if (!HasSpecialChar.IsMatch(password))
        {
            errors.Add("Password must contain at least one special character");
        }

        return (errors.Count == 0, errors.ToArray());
    }

    /// <summary>
    /// Получить требования к паролю как строку для отправки клиенту
    /// </summary>
    public static string GetRequirements()
    {
        return $@"Password requirements:
- Minimum {MinLength} characters
- At least one uppercase letter (A-Z)
- At least one lowercase letter (a-z)
- At least one digit (0-9)
- At least one special character (!@#$%^&*()_+-=[]{{}};\':""<>?.,/|`~)";
    }
}

