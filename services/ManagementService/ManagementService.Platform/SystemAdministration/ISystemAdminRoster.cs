using System.Security.Claims;

namespace ManagementService.Platform.SystemAdministration;

public interface ISystemAdminRoster
{
    IReadOnlyCollection<string> GetEmails();
    bool IsSystemAdminEmail(string? email);
    bool IsSystemAdmin(ClaimsPrincipal principal);
}
