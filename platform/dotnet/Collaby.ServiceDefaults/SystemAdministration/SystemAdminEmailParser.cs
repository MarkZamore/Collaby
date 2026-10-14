using System.Collections.Immutable;

namespace Collaby.ServiceDefaults.SystemAdministration;

public static class SystemAdminEmailParser
{
    public static ImmutableHashSet<string> Parse(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return ImmutableHashSet<string>.Empty;
        }

        return rawValue
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeEmail)
            .Where(static email => !string.IsNullOrWhiteSpace(email))
            .ToImmutableHashSet(StringComparer.Ordinal);
    }

    public static string NormalizeEmail(string? email)
    {
        return string.IsNullOrWhiteSpace(email)
            ? string.Empty
            : email.Trim().ToLowerInvariant();
    }
}
