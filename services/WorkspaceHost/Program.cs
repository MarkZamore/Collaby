using System.Text;
using AnalyticsService.Application.Interfaces;
using AnalyticsService.Application.Services;
using AnalyticsService.Controllers;
using AnalyticsService.Infrastructure.Data;
using Collaby.EventBus;
using Collaby.ServiceDefaults.Correlation;
using Collaby.ServiceDefaults.Health;
using Collaby.ServiceDefaults.InternalAuth;
using Collaby.ServiceDefaults.Observability;
using Collaby.ServiceDefaults.SystemAdministration;
using FluentValidation;
using ManagementService.Application.Interfaces;
using ManagementService.Application.Services;
using ManagementService.Application.Validators;
using ManagementService.Controllers;
using ManagementService.Hubs;
using ManagementService.Infrastructure.Data;
using ManagementService.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using StackExchange.Redis;
using WorkspaceHost;

var builder = WebApplication.CreateBuilder(args);
builder.AddCollabyServiceObservability("workspace-service");

var managementAssembly = typeof(WorkspacesController).Assembly;
var analyticsAssembly = typeof(AnalyticsSummaryController).Assembly;
var modules = new[]
{
    new HostModuleDescriptor(managementAssembly, "api/management", "management"),
    new HostModuleDescriptor(analyticsAssembly, "api/analytics", "analytics")
};

builder.Services
    .AddControllers(options =>
    {
        options.Conventions.Add(new HostModuleConvention(modules));
    })
    .AddApplicationPart(managementAssembly)
    .AddApplicationPart(analyticsAssembly)
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase));
    });

builder.Services.AddValidatorsFromAssemblyContaining<CreateWorkspaceRequestValidator>();

builder.Services.AddSwaggerGen(options =>
{
    ConfigureBearerSecurity(options);
    ConfigureModuleSwagger(options, "management", "Collaby Management API", "Project and task management service");
    ConfigureModuleSwagger(options, "analytics", "Collaby Analytics API", "Read-only analytics and reporting service");
    options.DocInclusionPredicate((documentName, apiDescription) =>
        string.Equals(apiDescription.GroupName, documentName, StringComparison.OrdinalIgnoreCase));
});

var managementPostgresConnection = builder.Configuration["ConnectionStrings:ManagementPostgres"]
    ?? throw new InvalidOperationException("Connection string 'ManagementPostgres' not found.");
var analyticsPostgresConnection = builder.Configuration["ConnectionStrings:AnalyticsPostgres"]
    ?? throw new InvalidOperationException("Connection string 'AnalyticsPostgres' not found.");
var redisConnectionString = builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("Connection string 'Redis' not found.");

builder.Services.AddDbContext<ManagementDbContext>(options => options.UseNpgsql(managementPostgresConnection));
builder.Services.AddDbContext<AnalyticsDbContext>(options => options.UseNpgsql(analyticsPostgresConnection));
builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnectionString));

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

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) &&
                    (path.StartsWithSegments("/api/management/hubs") || path.Value?.Contains("/api/management/hubs/") == true))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

builder.Services.AddAuthorization();
builder.Services.AddSystemAdministration(builder.Configuration);
builder.Services.AddCollabyEventBus(builder.Configuration, "workspace-service");
builder.Services.AddHttpContextAccessor();
builder.Services.AddInternalServiceAuthentication(builder.Configuration);
builder.Services.AddSingleton<ServiceReadinessState>();
builder.Services.AddSingleton<IServiceCatalogProvider, ServiceCatalogProvider>();
builder.Services.AddSingleton<ILandingDefaultsProvider, LandingDefaultsProvider>();
builder.Services.AddSingleton<IRealtimeNotifier, RealtimeNotifier>();
builder.Services.AddScoped<IServiceRestartClient, KubernetesAdminRestartClient>();
builder.Services.AddHostedService<ServiceRestartWorker>();
builder.Services.AddScoped<ILandingContentService, LandingContentService>();

builder.Services.AddScoped<IWorkspaceService, WorkspaceService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<IStatusService, StatusService>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<IInvitationService, InvitationService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IBootstrapService, BootstrapService>();
builder.Services.AddScoped<IProjectProgressSnapshotBuilder, ProjectProgressSnapshotBuilder>();
builder.Services.AddScoped<IProjectProgressSnapshotOutbox, ProjectProgressSnapshotOutbox>();
builder.Services.AddScoped<IGamificationHook, OutboxGamificationHook>();
builder.Services.AddScoped<ManagementService.Application.Services.AdminUserDeletionPurgeService>();
builder.Services.AddSingleton<IEmailQueueService, EmailQueueService>();
builder.Services.AddHostedService<GamificationOutboxDispatcher>();
builder.Services.AddHostedService<WorkspaceManagementAdminUserDeletionCommandConsumer>();

builder.Services.AddOptions<ManagementService.Configuration.UserServiceOptions>()
    .Bind(builder.Configuration.GetSection("ManagementService:UserService"))
    .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _), "ManagementService:UserService:BaseUrl must be an absolute URI.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.CallerService), "ManagementService:UserService:CallerService must be configured.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.InternalApiKey), "ManagementService:UserService:InternalApiKey must be configured.")
    .ValidateOnStart();

builder.Services.AddHttpClient("ManagementUserLookupClient", (serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ManagementService.Configuration.UserServiceOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddScoped<ManagementService.Application.Interfaces.IUserLookupClient>(serviceProvider =>
{
    var factory = serviceProvider.GetRequiredService<IHttpClientFactory>();
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ManagementService.Configuration.UserServiceOptions>>();
    var logger = serviceProvider.GetRequiredService<ILogger<ManagementService.Application.Services.UserLookupClient>>();
    return new ManagementService.Application.Services.UserLookupClient(
        factory.CreateClient("ManagementUserLookupClient"),
        options,
        logger);
});

builder.Services.AddHttpClient("Unsplash", client =>
{
    client.BaseAddress = new Uri("https://api.unsplash.com");
    client.DefaultRequestHeaders.Add("Accept-Version", "v1");
});
builder.Services.AddScoped<IUnsplashImageSearchService, UnsplashImageSearchService>();

builder.Services.AddOptions<AnalyticsService.Configuration.UserServiceOptions>()
    .Bind(builder.Configuration.GetSection("AnalyticsService:UserService"))
    .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _), "AnalyticsService:UserService:BaseUrl must be an absolute URI.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.CallerService), "AnalyticsService:UserService:CallerService must be configured.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.InternalApiKey), "AnalyticsService:UserService:InternalApiKey must be configured.")
    .ValidateOnStart();

builder.Services.AddHttpClient("AnalyticsUserLookupClient", (serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AnalyticsService.Configuration.UserServiceOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddScoped<AnalyticsService.Application.Interfaces.IUserLookupClient>(serviceProvider =>
{
    var factory = serviceProvider.GetRequiredService<IHttpClientFactory>();
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AnalyticsService.Configuration.UserServiceOptions>>();
    var logger = serviceProvider.GetRequiredService<ILogger<AnalyticsService.Application.Services.UserLookupClient>>();
    return new AnalyticsService.Application.Services.UserLookupClient(
        factory.CreateClient("AnalyticsUserLookupClient"),
        options,
        logger);
});

builder.Services.AddScoped<IManagementProjectAnalyticsSnapshotClient, InProcessManagementProjectAnalyticsSnapshotClient>();
builder.Services.AddScoped<AnalyticsProjectionService>();
builder.Services.AddScoped<AnalyticsService.Application.Services.AdminUserDeletionPurgeService>();
builder.Services.AddScoped<IProjectAnalyticsOverviewService, ProjectAnalyticsOverviewService>();
builder.Services.AddHostedService<ManagementTaskEventAnalyticsConsumer>();
builder.Services.AddHostedService<GamificationProgressAnalyticsConsumer>();
builder.Services.AddHostedService<WorkspaceAnalyticsAdminUserDeletionCommandConsumer>();

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
                "http://127.0.0.1")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();
var readinessState = app.Services.GetRequiredService<ServiceReadinessState>();

app.UseCollabyCorrelation();
app.UseErrorHandling();
app.UseForwardedHeaders();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.RoutePrefix = "swagger";
    options.SwaggerEndpoint("./management/swagger.json", "Collaby Management API v1");
    options.SwaggerEndpoint("./analytics/swagger.json", "Collaby Analytics API v1");
});

app.MapControllers();
app.MapHub<WorkspaceHub>("/api/management/hubs/workspace");

app.MapGet("/api/management/health/live", () => Results.Ok(new { status = "alive", service = "management" }));
app.MapGet("/api/management/health/ready", async (ServiceReadinessState state, ManagementDbContext dbContext, IConnectionMultiplexer redisMultiplexer) =>
    await EvaluateManagementHealthAsync(state, dbContext, redisMultiplexer));
app.MapGet("/api/management/health", async (ServiceReadinessState state, ManagementDbContext dbContext, IConnectionMultiplexer redisMultiplexer) =>
    await EvaluateManagementHealthAsync(state, dbContext, redisMultiplexer));

app.MapGet("/api/analytics", () => Results.Ok(new { status = "alive", service = "analytics" }));
app.MapGet("/api/analytics/health/live", () => Results.Ok(new { status = "alive", service = "analytics" }));
app.MapGet("/api/analytics/health/ready", async (ServiceReadinessState state, AnalyticsDbContext dbContext) =>
    await EvaluateAnalyticsHealthAsync(state, dbContext));
app.MapGet("/api/analytics/health", async (ServiceReadinessState state, AnalyticsDbContext dbContext) =>
    await EvaluateAnalyticsHealthAsync(state, dbContext));

app.MapGet("/health/live", () => Results.Ok(new { status = "alive", service = "workspace" }));
app.MapGet("/health/ready", async (ServiceReadinessState state, ManagementDbContext managementDbContext, AnalyticsDbContext analyticsDbContext, IConnectionMultiplexer redisMultiplexer) =>
    await EvaluateWorkspaceHealthAsync(state, managementDbContext, analyticsDbContext, redisMultiplexer));
app.MapGet("/health", async (ServiceReadinessState state, ManagementDbContext managementDbContext, AnalyticsDbContext analyticsDbContext, IConnectionMultiplexer redisMultiplexer) =>
    await EvaluateWorkspaceHealthAsync(state, managementDbContext, analyticsDbContext, redisMultiplexer));

using (var scope = app.Services.CreateScope())
{
    const int maxRetries = 5;
    const int delayMs = 3000;

    for (var attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            var managementDb = scope.ServiceProvider.GetRequiredService<ManagementDbContext>();
            await managementDb.Database.MigrateAsync();

            var analyticsDb = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
            await analyticsDb.Database.MigrateAsync();

            readinessState.MarkReady();
            app.Logger.LogInformation("WorkspaceHost database migrations applied successfully.");
            break;
        }
        catch (Exception ex)
        {
            if (attempt == maxRetries)
            {
                readinessState.MarkFailed(ex.Message);
                app.Logger.LogError(ex, "Failed to apply WorkspaceHost migrations after {RetryCount} attempts.", maxRetries);
                throw;
            }

            app.Logger.LogWarning(
                ex,
                "Attempt {Attempt}/{MaxRetries} to apply WorkspaceHost migrations failed. Retrying in {DelaySeconds} seconds...",
                attempt,
                maxRetries,
                delayMs / 1000);

            await Task.Delay(delayMs);
        }
    }
}

app.Run();

return;

static async Task<IResult> EvaluateManagementHealthAsync(ServiceReadinessState state, ManagementDbContext dbContext, IConnectionMultiplexer redisMultiplexer)
{
    if (!state.IsReady)
    {
        return Results.Json(
            new { status = "unhealthy", service = "management", reason = state.FailureReason ?? "Startup checks have not completed yet" },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    try
    {
        if (!await dbContext.Database.CanConnectAsync())
        {
            return Results.Json(
                new { status = "unhealthy", service = "management", reason = "PostgreSQL is unavailable" },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        await redisMultiplexer.GetDatabase().PingAsync();
        return Results.Ok(new { status = "ready", service = "management" });
    }
    catch (Exception ex)
    {
        return Results.Json(
            new { status = "unhealthy", service = "management", reason = ex.Message },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}

static async Task<IResult> EvaluateAnalyticsHealthAsync(ServiceReadinessState state, AnalyticsDbContext dbContext)
{
    if (!state.IsReady)
    {
        return Results.Json(
            new { status = "unhealthy", service = "analytics", reason = state.FailureReason ?? "Startup checks have not completed yet" },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    try
    {
        if (!await dbContext.Database.CanConnectAsync())
        {
            return Results.Json(
                new { status = "unhealthy", service = "analytics", reason = "PostgreSQL is unavailable" },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Results.Ok(new { status = "ready", service = "analytics" });
    }
    catch (Exception ex)
    {
        return Results.Json(
            new { status = "unhealthy", service = "analytics", reason = ex.Message },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}

static async Task<IResult> EvaluateWorkspaceHealthAsync(
    ServiceReadinessState state,
    ManagementDbContext managementDbContext,
    AnalyticsDbContext analyticsDbContext,
    IConnectionMultiplexer redisMultiplexer)
{
    if (!state.IsReady)
    {
        return Results.Json(
            new { status = "unhealthy", service = "workspace", reason = state.FailureReason ?? "Startup checks have not completed yet" },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    try
    {
        if (!await managementDbContext.Database.CanConnectAsync())
        {
            return Results.Json(
                new { status = "unhealthy", service = "workspace", reason = "Management PostgreSQL is unavailable" },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (!await analyticsDbContext.Database.CanConnectAsync())
        {
            return Results.Json(
                new { status = "unhealthy", service = "workspace", reason = "Analytics PostgreSQL is unavailable" },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        await redisMultiplexer.GetDatabase().PingAsync();
        return Results.Ok(new { status = "ready", service = "workspace" });
    }
    catch (Exception ex)
    {
        return Results.Json(
            new { status = "unhealthy", service = "workspace", reason = ex.Message },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}

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
