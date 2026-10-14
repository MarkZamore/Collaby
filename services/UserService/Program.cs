using System.Text;
using Collaby.EventBus;
using Collaby.ServiceDefaults.Correlation;
using Collaby.ServiceDefaults.Health;
using Collaby.ServiceDefaults.InternalAuth;
using Collaby.ServiceDefaults.Observability;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using UserService.Application.Interfaces;
using UserService.Application.Services;
using UserService.Application.Validators;
using UserService.Configuration;
using UserService.Infrastructure.Data;
using UserService.Infrastructure.Repositories;
using UserService.Middleware;

var builder = WebApplication.CreateBuilder(args);
builder.AddCollabyServiceObservability("user-service");

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });
builder.Services.AddValidatorsFromAssemblyContaining<UpdateProfileRequestValidator>();
builder.Services.AddFluentValidationAutoValidation();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Collaby User API",
        Version = "v1",
        Description = "User profile, preferences, onboarding and activity service"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using Bearer scheme."
    });

    c.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", null, null),
            new List<string>()
        }
    });
});

var pgConnectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Connection string 'Postgres' not found");

builder.Services.AddDbContext<UserDbContext>(options =>
{
    options.UseNpgsql(pgConnectionString);
});

var jwtSettings = builder.Configuration.GetSection("Jwt");
var signingKey = jwtSettings["SigningKey"] ?? throw new InvalidOperationException("JWT SigningKey not configured");

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
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddCollabyEventBus(builder.Configuration, "user-service");
builder.Services.AddHttpContextAccessor();
builder.Services.AddInternalServiceAuthentication(builder.Configuration);
builder.Services.AddSingleton<ServiceReadinessState>();

builder.Services.AddOptions<IdentityServiceOptions>()
    .Bind(builder.Configuration.GetSection("IdentityService"))
    .Validate(
        options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _),
        "IdentityService:BaseUrl must be an absolute URI.")
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.CallerService),
        "IdentityService:CallerService must be configured.")
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.InternalApiKey),
        "IdentityService:InternalApiKey must be configured.")
    .ValidateOnStart();

builder.Services.AddHttpClient<IIdentityDirectoryBootstrapClient, IdentityDirectoryBootstrapClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<IdentityServiceOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IIdentityDirectoryProjectionWriter, IdentityDirectoryProjectionWriter>();
builder.Services.AddScoped<IIdentityDirectoryBootstrapService, IdentityDirectoryBootstrapService>();
builder.Services.AddScoped<IUserMeService, UserMeService>();
builder.Services.AddScoped<AdminUserDeletionPurgeService>();
builder.Services.AddHostedService<AdminUserDeletionCommandConsumer>();
builder.Services.AddHostedService<IdentityUserDirectoryConsumer>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                builder.Configuration["Frontend:BaseUrl"] ?? "http://localhost:8080",
                "https://collaby.org",
                "http://localhost:3000",
                "http://localhost",
                "http://localhost:80",
                "http://127.0.0.1:3000",
                "http://127.0.0.1"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
#pragma warning disable ASPDEPR005
    options.KnownNetworks.Clear();
#pragma warning restore ASPDEPR005
    options.KnownProxies.Clear();
});

var app = builder.Build();
var readinessState = app.Services.GetRequiredService<ServiceReadinessState>();

app.UseCollabyCorrelation();
app.UseErrorHandling();
app.UseForwardedHeaders();
app.UsePathBase("/api/users");
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("./v1/swagger.json", "Collaby User API v1");
    c.RoutePrefix = "swagger";
});

app.MapControllers();
app.MapGet("/health/live", () => Results.Ok(new { status = "alive", service = "users" }));
app.MapGet("/health/ready", async (ServiceReadinessState state, UserDbContext dbContext) =>
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
app.MapGet("/health", async (ServiceReadinessState state, UserDbContext dbContext) =>
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

using (var scope = app.Services.CreateScope())
{
    const int maxRetries = 5;
    const int delayMs = 3000;

    var migrationsCompleted = await ExecuteStartupStageAsync(
        stageDescription: "Applying UserService migrations",
        successLogMessage: "UserService database migrations applied successfully.",
        action: async serviceProvider =>
        {
            var db = serviceProvider.GetRequiredService<UserDbContext>();
            await db.Database.MigrateAsync();
        });

    var bootstrapCompleted = migrationsCompleted && await ExecuteStartupStageAsync(
        stageDescription: "Bootstrapping identity directory projection",
        successLogMessage: "UserService identity directory bootstrap completed successfully.",
        action: async serviceProvider =>
        {
            await serviceProvider.GetRequiredService<IIdentityDirectoryBootstrapService>().EnsureSeededAsync();
        });

    if (migrationsCompleted && bootstrapCompleted)
    {
        readinessState.MarkReady();
        app.Logger.LogInformation("UserService startup checks completed. Service is ready to accept requests.");
    }
    else
    {
        app.Logger.LogCritical(
            "UserService startup did not reach ready state. Last readiness detail: {Reason}",
            readinessState.StatusDetail ?? "unknown");
    }

    async Task<bool> ExecuteStartupStageAsync(
        string stageDescription,
        string successLogMessage,
        Func<IServiceProvider, Task> action)
    {
        readinessState.MarkStarting(stageDescription);

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await action(scope.ServiceProvider);
                app.Logger.LogInformation(successLogMessage);
                return true;
            }
            catch (Exception ex)
            {
                if (attempt == maxRetries)
                {
                    var failureReason = $"{stageDescription}: {ex.Message}";
                    readinessState.MarkFailed(failureReason);
                    app.Logger.LogError(
                        ex,
                        "{StageDescription} failed after {RetryCount} attempts.",
                        stageDescription,
                        maxRetries);
                    return false;
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

        return false;
    }
}

app.Run();

public partial class Program;
