using System.Reflection;
using System.Text;
using AccessHost;
using Collaby.EventBus;
using Collaby.ServiceDefaults.Correlation;
using Collaby.ServiceDefaults.Health;
using Collaby.ServiceDefaults.InternalAuth;
using Collaby.ServiceDefaults.Observability;
using Collaby.ServiceDefaults.SystemAdministration;
using FluentValidation;
using FluentValidation.AspNetCore;
using IdentityService.Application.Interfaces;
using IdentityService.Application.Services;
using IdentityService.Application.Settings;
using IdentityService.Controllers;
using IdentityService.Infrastructure.Data;
using IdentityService.Infrastructure.Health;
using IdentityService.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using MongoDB.Bson;
using StackExchange.Redis;
using UserService.Application.Interfaces;
using UserService.Application.Services;
using UserService.Application.Validators;
using UserService.Configuration;
using UserService.Controllers;
using UserService.Infrastructure.Data;
using UserService.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);
builder.AddCollabyServiceObservability("access-service");

var authAssembly = typeof(AuthController).Assembly;
var usersAssembly = typeof(UsersController).Assembly;
var modules = new[]
{
    new HostModuleDescriptor(authAssembly, "api/auth", "auth"),
    new HostModuleDescriptor(usersAssembly, "api/users", "users")
};

builder.Services
    .AddControllers(options =>
    {
        options.Conventions.Add(new HostModuleConvention(modules));
    })
    .AddApplicationPart(authAssembly)
    .AddApplicationPart(usersAssembly)
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

builder.Services.AddValidatorsFromAssemblyContaining<IdentityService.Contracts.Requests.RegisterRequestValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<UpdateProfileRequestValidator>();
builder.Services.AddFluentValidationAutoValidation();

builder.Services.AddSwaggerGen(options =>
{
    ConfigureBearerSecurity(options);
    ConfigureModuleSwagger(
        options,
        "auth",
        "Collaby Identity API",
        "Authentication and user management service");
    ConfigureModuleSwagger(
        options,
        "users",
        "Collaby User API",
        "User profile, preferences, onboarding and activity service");

    options.DocInclusionPredicate((documentName, apiDescription) =>
        string.Equals(apiDescription.GroupName, documentName, StringComparison.OrdinalIgnoreCase));
});

var identityPostgresConnection = builder.Configuration["ConnectionStrings:IdentityPostgres"]
    ?? throw new InvalidOperationException("Connection string 'IdentityPostgres' not found.");
var usersPostgresConnection = builder.Configuration["ConnectionStrings:UsersPostgres"]
    ?? throw new InvalidOperationException("Connection string 'UsersPostgres' not found.");

builder.Services.AddDbContext<IdentityDbContext>(options => options.UseNpgsql(identityPostgresConnection));
builder.Services.AddDbContext<UserDbContext>(options => options.UseNpgsql(usersPostgresConnection));

builder.Services.AddSingleton<MongoDbContext>();
builder.Services.AddSingleton<IdentityServiceReadinessState>();
builder.Services.AddSingleton<ServiceReadinessState>();

var redisConnection = builder.Configuration.GetConnectionString("Redis");
try
{
    if (!string.IsNullOrWhiteSpace(redisConnection))
    {
        var redis = ConnectionMultiplexer.Connect(redisConnection);
        builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Failed to connect to Redis: {ex.Message}. Rate limiting disabled.");
}

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<GoogleOptions>(builder.Configuration.GetSection("Google"));

var jwtSettings = builder.Configuration.GetSection("Jwt");
var signingKey = jwtSettings["SigningKey"] ?? throw new InvalidOperationException("JWT SigningKey not configured.");

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidateIssuer = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSettings["Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(5)
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSystemAdministration(builder.Configuration);
builder.Services.AddCollabyEventBus(builder.Configuration, "access-service");
builder.Services.AddInternalServiceAuthentication(builder.Configuration);

builder.Services.AddSingleton<IJwtService, JwtService>();
builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
builder.Services.AddSingleton<IEncryptionService, EncryptionService>();
builder.Services.AddSingleton<IGoogleOAuthService, GoogleOAuthService>();
builder.Services.AddSingleton<IGoogleRegistrationTokenService, GoogleRegistrationTokenService>();
builder.Services.AddSingleton<IGoogleNicknameGenerator, GoogleNicknameGenerator>();
builder.Services.AddSingleton<IUserDeletionTargetCatalog, ConfigurationUserDeletionTargetCatalog>();
builder.Services.AddScoped<IIdentityDirectoryOutboxWriter, IdentityDirectoryOutboxWriter>();
builder.Services.AddScoped<IUserDeletionOrchestrator, UserDeletionOrchestrator>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IEmailVerificationService, EmailVerificationService>();
builder.Services.AddHostedService<UserDeletionBackgroundService>();
builder.Services.AddHostedService<UserDeletionResultsConsumer>();
builder.Services.AddHostedService<IdentityDirectoryOutboxDispatcher>();

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IIdentityDirectoryProjectionWriter, IdentityDirectoryProjectionWriter>();
builder.Services.AddScoped<IIdentityDirectoryBootstrapClient, InProcessIdentityDirectoryBootstrapClient>();
builder.Services.AddScoped<IIdentityDirectoryBootstrapService, IdentityDirectoryBootstrapService>();
builder.Services.AddScoped<IUserMeService, UserMeService>();
builder.Services.AddScoped<UserService.Application.Services.AdminUserDeletionPurgeService>();
builder.Services.AddHostedService<UserService.Application.Services.AdminUserDeletionCommandConsumer>();
builder.Services.AddHostedService<IdentityUserDirectoryConsumer>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                builder.Configuration["Frontend:BaseUrl"] ?? "http://localhost:8080",
                builder.Configuration["Frontend:BaseUrl"] ?? "http://localhost:8080",
                "https://collaby.org",
                "http://localhost:3000",
                "http://localhost",
                "http://localhost:80",
                "http://127.0.0.1:3000",
                "http://127.0.0.1")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
#pragma warning disable ASPDEPR005
    options.KnownNetworks.Clear();
#pragma warning restore ASPDEPR005
    options.KnownProxies.Clear();
});

var app = builder.Build();
var identityReadiness = app.Services.GetRequiredService<IdentityServiceReadinessState>();
var usersReadiness = app.Services.GetRequiredService<ServiceReadinessState>();
var rateLimitingEnabled = builder.Configuration.GetValue<bool>("RateLimiting:Enabled");

app.UseCollabyCorrelation();
app.UseErrorHandling();
if (rateLimitingEnabled)
{
    app.UseRateLimit();
}
else
{
    app.Logger.LogWarning("Rate limiting middleware is disabled because Redis is unavailable.");
}

app.UseForwardedHeaders();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.RoutePrefix = "swagger";
    options.SwaggerEndpoint("./auth/swagger.json", "Collaby Identity API v1");
    options.SwaggerEndpoint("./users/swagger.json", "Collaby User API v1");
});

app.MapControllers();

app.MapGet("/api/users/health/live", () => Results.Ok(new { status = "alive", service = "users" }));
app.MapGet("/api/users/health/ready", async (ServiceReadinessState state, UserDbContext dbContext) =>
{
    if (!state.IsReady)
    {
        return Results.Json(
            new
            {
                status = "unhealthy",
                service = "users",
                reason = state.StatusDetail ?? "Startup checks have not completed yet"
            },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    try
    {
        if (!await dbContext.Database.CanConnectAsync())
        {
            return Results.Json(
                new { status = "unhealthy", service = "users", reason = "PostgreSQL is unavailable" },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Results.Ok(new { status = "ready", service = "users" });
    }
    catch (Exception ex)
    {
        return Results.Json(
            new { status = "unhealthy", service = "users", reason = ex.Message },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});
app.MapGet("/api/users/health", async (ServiceReadinessState state, UserDbContext dbContext) =>
{
    if (!state.IsReady)
    {
        return Results.Json(
            new
            {
                status = "unhealthy",
                service = "users",
                reason = state.StatusDetail ?? "Startup checks have not completed yet"
            },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    try
    {
        if (!await dbContext.Database.CanConnectAsync())
        {
            return Results.Json(
                new { status = "unhealthy", service = "users", reason = "PostgreSQL is unavailable" },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Results.Ok(new { status = "ready", service = "users" });
    }
    catch (Exception ex)
    {
        return Results.Json(
            new { status = "unhealthy", service = "users", reason = ex.Message },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapGet("/health/live", () => Results.Ok(new { status = "alive", service = "access" }));
app.MapGet("/health/ready", async (
    IdentityServiceReadinessState identityState,
    ServiceReadinessState userState,
    IdentityDbContext identityDbContext,
    UserDbContext userDbContext,
    MongoDbContext mongoDbContext) =>
{
    if (!identityState.IsReady)
    {
        return Results.Json(
            new { status = "unhealthy", service = "access", reason = identityState.FailureReason ?? "Identity startup checks have not completed yet" },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    if (!userState.IsReady)
    {
        return Results.Json(
            new { status = "unhealthy", service = "access", reason = userState.StatusDetail ?? "User startup checks have not completed yet" },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    try
    {
        if (!await identityDbContext.Database.CanConnectAsync())
        {
            return Results.Json(
                new { status = "unhealthy", service = "access", reason = "Identity PostgreSQL is unavailable" },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (!await userDbContext.Database.CanConnectAsync())
        {
            return Results.Json(
                new { status = "unhealthy", service = "access", reason = "User PostgreSQL is unavailable" },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        await mongoDbContext.Database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));

        return Results.Ok(new { status = "ready", service = "access" });
    }
    catch (Exception ex)
    {
        return Results.Json(
            new { status = "unhealthy", service = "access", reason = ex.Message },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});
app.MapGet("/health", async (
    IdentityServiceReadinessState identityState,
    ServiceReadinessState userState,
    IdentityDbContext identityDbContext,
    UserDbContext userDbContext,
    MongoDbContext mongoDbContext) =>
{
    if (!identityState.IsReady)
    {
        return Results.Json(
            new { status = "unhealthy", service = "access", reason = identityState.FailureReason ?? "Identity startup checks have not completed yet" },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    if (!userState.IsReady)
    {
        return Results.Json(
            new { status = "unhealthy", service = "access", reason = userState.StatusDetail ?? "User startup checks have not completed yet" },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    try
    {
        if (!await identityDbContext.Database.CanConnectAsync())
        {
            return Results.Json(
                new { status = "unhealthy", service = "access", reason = "Identity PostgreSQL is unavailable" },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (!await userDbContext.Database.CanConnectAsync())
        {
            return Results.Json(
                new { status = "unhealthy", service = "access", reason = "User PostgreSQL is unavailable" },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        await mongoDbContext.Database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));

        return Results.Ok(new { status = "ready", service = "access" });
    }
    catch (Exception ex)
    {
        return Results.Json(
            new { status = "unhealthy", service = "access", reason = ex.Message },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

using (var scope = app.Services.CreateScope())
{
    const int maxRetries = 5;
    const int delayMs = 3000;

    try
    {
        await ExecuteCriticalStartupActionAsync(
            "Applying IdentityService migrations",
            async serviceProvider =>
            {
                var db = serviceProvider.GetRequiredService<IdentityDbContext>();
                await db.Database.MigrateAsync();
            });

        await ExecuteCriticalStartupActionAsync(
            "Checking IdentityService MongoDB connectivity",
            async serviceProvider =>
            {
                var mongo = serviceProvider.GetRequiredService<MongoDbContext>();
                await mongo.Database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
            });

        identityReadiness.MarkReady();

        usersReadiness.MarkStarting("Applying UserService migrations");
        await ExecuteUserStartupStageAsync(
            "Applying UserService migrations",
            async serviceProvider =>
            {
                var db = serviceProvider.GetRequiredService<UserDbContext>();
                await db.Database.MigrateAsync();
            });

        usersReadiness.MarkStarting("Bootstrapping identity directory projection");
        await ExecuteUserStartupStageAsync(
            "Bootstrapping identity directory projection",
            async serviceProvider =>
            {
                await serviceProvider.GetRequiredService<IIdentityDirectoryBootstrapService>().EnsureSeededAsync();
            });

        usersReadiness.MarkReady();
        app.Logger.LogInformation("AccessHost startup checks completed. Service is ready to accept requests.");
    }
    catch (Exception ex)
    {
        if (!identityReadiness.IsReady)
        {
            identityReadiness.MarkFailed(ex.Message);
        }

        if (!usersReadiness.IsReady)
        {
            usersReadiness.MarkFailed(ex.Message);
        }

        app.Logger.LogCritical(ex, "AccessHost startup failed because a critical dependency is unavailable.");
        throw;
    }

    async Task ExecuteCriticalStartupActionAsync(
        string operationName,
        Func<IServiceProvider, Task> action)
    {
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await action(scope.ServiceProvider);
                app.Logger.LogInformation("{OperationName} completed successfully.", operationName);
                return;
            }
            catch (Exception ex)
            {
                if (attempt == maxRetries)
                {
                    app.Logger.LogError(ex, "{OperationName} failed after {RetryCount} attempts.", operationName, maxRetries);
                    throw;
                }

                app.Logger.LogWarning(
                    ex,
                    "Attempt {Attempt}/{MaxRetries} for {OperationName} failed. Retrying in {DelaySeconds} seconds...",
                    attempt,
                    maxRetries,
                    operationName,
                    delayMs / 1000);

                await Task.Delay(delayMs);
            }
        }
    }

    async Task ExecuteUserStartupStageAsync(
        string stageDescription,
        Func<IServiceProvider, Task> action)
    {
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await action(scope.ServiceProvider);
                app.Logger.LogInformation("{StageDescription} completed successfully.", stageDescription);
                return;
            }
            catch (Exception ex)
            {
                if (attempt == maxRetries)
                {
                    usersReadiness.MarkFailed($"{stageDescription}: {ex.Message}");
                    app.Logger.LogError(ex, "{StageDescription} failed after {RetryCount} attempts.", stageDescription, maxRetries);
                    throw;
                }

                app.Logger.LogWarning(
                    ex,
                    "Attempt {Attempt}/{MaxRetries} for {StageDescription} failed. Retrying in {DelaySeconds} seconds...",
                    attempt,
                    maxRetries,
                    stageDescription,
                    delayMs / 1000);

                await Task.Delay(delayMs);
            }
        }
    }
}

app.Run();

return;

static void ConfigureBearerSecurity(Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions options)
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using Bearer scheme."
    });

    options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", null, null),
            new List<string>()
        }
    });
}

static void ConfigureModuleSwagger(
    Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions options,
    string documentName,
    string title,
    string description)
{
    options.SwaggerDoc(documentName, new OpenApiInfo
    {
        Title = title,
        Version = "v1",
        Description = description
    });
}

public partial class Program;
