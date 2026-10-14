using System.Security.Claims;
using Microsoft.Extensions.Configuration;

namespace ManagementService.Platform.SystemAdministration;

public sealed class ConfigurationSystemAdminRoster : ISystemAdminRoster
{
    private static readonly string[] EmailClaimTypes =
    [
        ClaimTypes.Email,
        "email"
    ];

    private readonly IConfiguration _configuration;

    public ConfigurationSystemAdminRoster(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public IReadOnlyCollection<string> GetEmails()
    {
        return SystemAdminEmailParser.Parse(_configuration[SystemAdminConstants.EmailsConfigurationKey]);
    }

    public bool IsSystemAdminEmail(string? email)
    {
        var normalizedEmail = SystemAdminEmailParser.NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return false;
        }

        return SystemAdminEmailParser
            .Parse(_configuration[SystemAdminConstants.EmailsConfigurationKey])
            .Contains(normalizedEmail);
    }

    public bool IsSystemAdmin(ClaimsPrincipal principal)
    {
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var email = EmailClaimTypes
            .Select(principal.FindFirstValue)
            .FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value));

        return IsSystemAdminEmail(email);
    }
}
