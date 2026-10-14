using System.Text;
using Collaby.EventBus;
using Collaby.ServiceDefaults.Correlation;
using Collaby.ServiceDefaults.Health;
using Collaby.ServiceDefaults.InternalAuth;
using Collaby.ServiceDefaults.Observability;
using FluentValidation;
using ManagementService.Application.Interfaces;
using ManagementService.Application.Services;
using ManagementService.Application.Validators;
using ManagementService.Configuration;
using ManagementService.Hubs;
using ManagementService.Infrastructure.Data;
using ManagementService.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using StackExchange.Redis;
using Swashbuckle.AspNetCore.SwaggerUI;
using Collaby.ServiceDefaults.SystemAdministration;

var builder = WebApplication.CreateBuilder(args);
builder.AddCollabyServiceObservability("management-service");

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase));
    });

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Collaby Management API",
        Version = "v1",
        Description = "Project and task management service"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme."
    });

    c.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", null, null),
            new List<string>()
        }
    });
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

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) &&
                    (path.StartsWithSegments("/hubs") || path.Value?.Contains("/hubs/") == true))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddSignalR()
    .AddJsonProtocol(o =>
    {
        o.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });
builder.Services.AddSingleton<IRealtimeNotifier, RealtimeNotifier>();

builder.Services.AddSystemAdministration(builder.Configuration);
builder.Services.AddCollabyEventBus(builder.Configuration, "management-service");
builder.Services.AddHttpContextAccessor();
builder.Services.AddInternalServiceAuthentication(builder.Configuration);
builder.Services.AddSingleton<ServiceReadinessState>();
builder.Services.AddSingleton<IServiceCatalogProvider, ServiceCatalogProvider>();
builder.Services.AddSingleton<ILandingDefaultsProvider, LandingDefaultsProvider>();
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
builder.Services.AddScoped<AdminUserDeletionPurgeService>();
builder.Services.AddSingleton<IEmailQueueService, EmailQueueService>();
builder.Services.AddHostedService<GamificationOutboxDispatcher>();
builder.Services.AddHostedService<AdminUserDeletionCommandConsumer>();

builder.Services.AddOptions<UserServiceOptions>()
    .Bind(builder.Configuration.GetSection("UserService"))
    .Validate(
        options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _),
        "UserService:BaseUrl must be an absolute URI.")
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.CallerService),
        "UserService:CallerService must be configured.")
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.InternalApiKey),
        "UserService:InternalApiKey must be configured.")
    .ValidateOnStart();

builder.Services.AddHttpClient<IUserLookupClient, UserLookupClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<UserServiceOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddHttpClient("Unsplash", client =>
{
    client.BaseAddress = new Uri("https://api.unsplash.com");
    client.DefaultRequestHeaders.Add("Accept-Version", "v1");
});
builder.Services.AddScoped<IUnsplashImageSearchService, UnsplashImageSearchService>();

builder.Services.AddValidatorsFromAssemblyContaining<CreateWorkspaceRequestValidator>();

var pgConnectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Connection string 'Postgres' not found");
builder.Services.AddDbContext<ManagementDbContext>(options => options.UseNpgsql(pgConnectionString));

var redisConnectionString = builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("Connection string 'Redis' not found");
var redis = ConnectionMultiplexer.Connect(redisConnectionString);
builder.Services.AddSingleton<IConnectionMultiplexer>(redis);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .WithOrigins(
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
app.UsePathBase("/api/management");
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("./v1/swagger.json", "Management API v1");
    c.RoutePrefix = "swagger";
});

app.MapControllers();
app.MapHub<WorkspaceHub>("/hubs/workspace");
app.MapGet("/health/live", () => Results.Ok(new { status = "alive", service = "management" }));
app.MapGet("/health/ready", async (ServiceReadinessState state, ManagementDbContext dbContext, IConnectionMultiplexer redisMultiplexer) =>
{
    if (!state.IsReady)
    {
        return Results.Json(
            new
            {
                status = "unhealthy",
                service = "management",
                reason = state.FailureReason ?? "Startup checks have not completed yet"
            },
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
});
app.MapGet("/health", async (ServiceReadinessState state, ManagementDbContext dbContext, IConnectionMultiplexer redisMultiplexer) =>
{
    if (!state.IsReady)
    {
        return Results.Json(
            new
            {
                status = "unhealthy",
                service = "management",
                reason = state.FailureReason ?? "Startup checks have not completed yet"
            },
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
});

using (var scope = app.Services.CreateScope())
{
    const int maxRetries = 5;
    const int delayMs = 3000;

    for (var attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            var db = scope.ServiceProvider.GetRequiredService<ManagementDbContext>();
            await db.Database.MigrateAsync();
            app.Logger.LogInformation("ManagementService database migrations applied successfully.");
            readinessState.MarkReady();
            break;
        }
        catch (Exception ex)
        {
            if (attempt == maxRetries)
            {
                readinessState.MarkFailed(ex.Message);
                app.Logger.LogError(ex, "Failed to apply ManagementService migrations after {RetryCount} attempts.", maxRetries);
                throw;
            }

            app.Logger.LogWarning(
                ex,
                "Attempt {Attempt}/{MaxRetries} to apply ManagementService migrations failed. Retrying in {DelaySeconds} seconds...",
                attempt,
                maxRetries,
                delayMs / 1000);

            await Task.Delay(delayMs);
        }
    }
}

app.Run();

public partial class Program;
