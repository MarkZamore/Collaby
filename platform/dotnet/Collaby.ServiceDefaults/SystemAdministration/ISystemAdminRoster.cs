using System.Security.Claims;

namespace Collaby.ServiceDefaults.SystemAdministration;

public interface ISystemAdminRoster
{
    IReadOnlyCollection<string> GetEmails();
    bool IsSystemAdminEmail(string? email);
    bool IsSystemAdmin(ClaimsPrincipal principal);
}
