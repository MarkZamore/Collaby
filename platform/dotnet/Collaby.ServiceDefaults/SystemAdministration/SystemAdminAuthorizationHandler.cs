using Microsoft.AspNetCore.Authorization;

namespace Collaby.ServiceDefaults.SystemAdministration;

public sealed class SystemAdminAuthorizationHandler : AuthorizationHandler<SystemAdminRequirement>
{
    private readonly ISystemAdminRoster _systemAdminRoster;

    public SystemAdminAuthorizationHandler(ISystemAdminRoster systemAdminRoster)
    {
        _systemAdminRoster = systemAdminRoster;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        SystemAdminRequirement requirement)
    {
        if (_systemAdminRoster.IsSystemAdmin(context.User))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
