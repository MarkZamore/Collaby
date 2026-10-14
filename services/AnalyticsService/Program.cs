using System.Text;
using AnalyticsService.Application.Interfaces;
using AnalyticsService.Application.Services;
using AnalyticsService.Configuration;
using AnalyticsService.Infrastructure.Data;
using Collaby.EventBus;
using Collaby.ServiceDefaults.Correlation;
using Collaby.ServiceDefaults.Health;
using Collaby.ServiceDefaults.InternalAuth;
using Collaby.ServiceDefaults.Observability;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);
builder.AddCollabyServiceObservability("analytics-service");

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Collaby Analytics API",
        Version = "v1",
        Description = "Read-only analytics and reporting service"
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
    ?? throw new InvalidOperationException("Connection string 'Postgres' not found.");

builder.Services.AddDbContext<AnalyticsDbContext>(options =>
{
    options.UseNpgsql(pgConnectionString);
});

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
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddCollabyEventBus(builder.Configuration, "analytics-service");
builder.Services.AddInternalServiceAuthentication(builder.Configuration);
builder.Services.AddSingleton<ServiceReadinessState>();
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
builder.Services.AddOptions<ManagementServiceOptions>()
    .Bind(builder.Configuration.GetSection("ManagementService"))
    .Validate(
        options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _),
        "ManagementService:BaseUrl must be an absolute URI.")
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.CallerService),
        "ManagementService:CallerService must be configured.")
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.InternalApiKey),
        "ManagementService:InternalApiKey must be configured.")
    .ValidateOnStart();
builder.Services.AddHttpClient<IUserLookupClient, UserLookupClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<UserServiceOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddHttpClient<IManagementProjectAnalyticsSnapshotClient, ManagementProjectAnalyticsSnapshotClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ManagementServiceOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddScoped<AnalyticsProjectionService>();
builder.Services.AddScoped<AdminUserDeletionPurgeService>();
builder.Services.AddScoped<IProjectAnalyticsOverviewService, ProjectAnalyticsOverviewService>();
builder.Services.AddHostedService<ManagementTaskEventAnalyticsConsumer>();
builder.Services.AddHostedService<GamificationProgressAnalyticsConsumer>();
builder.Services.AddHostedService<AdminUserDeletionCommandConsumer>();

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
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();
var readinessState = app.Services.GetRequiredService<ServiceReadinessState>();

app.UseCollabyCorrelation();
app.UseForwardedHeaders();
app.UsePathBase("/api/analytics");
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("./v1/swagger.json", "Collaby Analytics API v1");
    c.RoutePrefix = "swagger";
});

app.MapControllers();

app.MapGet("/", () => Results.Ok(new { status = "alive", service = "analytics" }));
app.MapGet("/health/live", () => Results.Ok(new { status = "alive", service = "analytics" }));

app.MapGet("/health/ready", async (ServiceReadinessState state, AnalyticsDbContext dbContext) =>
{
    if (!state.IsReady)
    {
        return Results.Json(
            new
            {
                status = "unhealthy",
                service = "analytics",
                reason = state.FailureReason ?? "Startup checks have not completed yet"
            },
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
});

app.MapGet("/health", async (ServiceReadinessState state, AnalyticsDbContext dbContext) =>
{
    if (!state.IsReady)
    {
        return Results.Json(
            new
            {
                status = "unhealthy",
                service = "analytics",
                reason = state.FailureReason ?? "Startup checks have not completed yet"
            },
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
});

try
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();

    if (app.Environment.IsEnvironment("Testing"))
    {
        await dbContext.Database.EnsureCreatedAsync();
    }
    else
    {
        await dbContext.Database.MigrateAsync();
    }

    readinessState.MarkReady();
    app.Logger.LogInformation("AnalyticsService startup checks completed. Service is ready.");
}
catch (Exception ex)
{
    readinessState.MarkFailed(ex.Message);
    app.Logger.LogCritical(ex, "AnalyticsService failed startup checks.");
    throw;
}

app.Run();

public partial class Program;
