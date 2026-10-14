using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Collaby.ServiceDefaults.InternalAuth;

public static class InternalAuthServiceCollectionExtensions
{
    public static IServiceCollection AddInternalServiceAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<InternalServiceAuthOptions>()
            .Bind(configuration.GetSection(InternalServiceAuthOptions.SectionName));

        services.AddSingleton<InternalServiceRequestAuthenticator>();
        return services;
    }
}
