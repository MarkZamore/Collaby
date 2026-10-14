using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ManagementService.Platform.SystemAdministration;

public static class SystemAdminServiceCollectionExtensions
{
    public static IServiceCollection AddSystemAdministration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<ISystemAdminRoster>(_ => new ConfigurationSystemAdminRoster(configuration));
        services.AddSingleton<IAuthorizationHandler, SystemAdminAuthorizationHandler>();
        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                SystemAdminConstants.PolicyName,
                policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.AddRequirements(new SystemAdminRequirement());
                });
        });

        return services;
    }
}
