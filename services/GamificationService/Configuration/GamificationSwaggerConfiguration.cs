using Microsoft.OpenApi;

namespace GamificationService.Configuration;

public static class GamificationSwaggerConfiguration
{
    public static IServiceCollection AddGamificationSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Collaby Gamification API",
                Version = "v1",
                Description = "XP, levels, quests, leaderboards, shop, and skill trees"
            });
            // Swagger также доступен через отдельный gateway route без PathBase запроса.
            options.AddServer(new OpenApiServer { Url = "/api/gamification" });
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "JWT Authorization header using the Bearer scheme."
            });
            options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
            {
                { new OpenApiSecuritySchemeReference("Bearer", null, null), new List<string>() }
            });
        });
        return services;
    }
}
