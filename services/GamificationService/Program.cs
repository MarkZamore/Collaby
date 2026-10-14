using System.Text;
using Collaby.EventBus;
using Collaby.ServiceDefaults.Correlation;
using Collaby.ServiceDefaults.Health;
using Collaby.ServiceDefaults.InternalAuth;
using Collaby.ServiceDefaults.Observability;
using GamificationService.Application.Interfaces;
using GamificationService.Application.Services;
using GamificationService.Configuration;
using GamificationService.Hubs;
using GamificationService.Infrastructure.Data;
using GamificationService.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using MongoDB.Bson;
using MongoDB.Driver;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
builder.AddCollabyServiceObservability("gamification-service");

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase));
    });

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Collaby Gamification API",
        Version = "v1",
        Description = "XP, levels, quests, leaderboards, shop, and skill trees"
    });

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
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

builder.Services.AddAuthorization();
builder.Services.AddCollabyEventBus(builder.Configuration, "gamification-service");
builder.Services.AddHttpContextAccessor();
builder.Services.AddInternalServiceAuthentication(builder.Configuration);
builder.Services.AddSingleton<ServiceReadinessState>();

var pgConnStr = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("ConnectionStrings:Postgres not configured");
builder.Services.AddDbContext<GamificationDbContext>(options => options.UseNpgsql(pgConnStr));

var mongoConnStr = builder.Configuration.GetConnectionString("Mongo");
if (string.IsNullOrWhiteSpace(mongoConnStr))
{
    throw new InvalidOperationException("ConnectionStrings:Mongo not configured");
}

var mongoUrl = MongoUrl.Create(mongoConnStr);
var mongoClient = new MongoClient(mongoUrl);
var mongoDbName = string.IsNullOrWhiteSpace(mongoUrl.DatabaseName) ? "collaby_gamification" : mongoUrl.DatabaseName;

builder.Services.AddSingleton<IMongoClient>(mongoClient);
builder.Services.AddSingleton(sp => sp.GetRequiredService<IMongoClient>().GetDatabase(mongoDbName));

var redisConnStr = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrWhiteSpace(redisConnStr))
{
    builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnStr));
}

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

builder.Services.AddHttpClient<IProjectProgressBootstrapClient, ProjectProgressBootstrapClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ManagementServiceOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddHttpClient<IProjectAuthorizationClient, ProjectAuthorizationClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ManagementServiceOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddScoped<IXpService, XpService>();
builder.Services.AddScoped<ILeaderboardService, LeaderboardService>();
builder.Services.AddScoped<IQuestService, QuestService>();
builder.Services.AddScoped<IStreakService, StreakService>();
builder.Services.AddScoped<IShopService, ShopService>();
builder.Services.AddScoped<IProjectProgressProjectionWriter, ProjectProgressProjectionWriter>();
builder.Services.AddScoped<IProjectProgressBootstrapService, ProjectProgressBootstrapService>();
builder.Services.AddScoped<IGamificationEventJournal, MongoGamificationEventJournal>();
builder.Services.AddScoped<IAnalyticsRewardOutbox, AnalyticsRewardOutbox>();
builder.Services.AddScoped<ManagementTaskEventProcessor>();
builder.Services.AddScoped<AdminUserDeletionPurgeService>();
builder.Services.AddHostedService<ManagementTaskEventsConsumer>();
builder.Services.AddHostedService<ManagementProjectProgressSnapshotConsumer>();
builder.Services.AddHostedService<AnalyticsOutboxDispatcher>();
builder.Services.AddHostedService<AdminUserDeletionCommandConsumer>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .WithOrigins("http://localhost:3000", "http://localhost:80", "http://localhost", "https://collaby.org")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();
var readinessState = app.Services.GetRequiredService<ServiceReadinessState>();

app.UseCollabyCorrelation();
app.UseMiddleware<ErrorHandlingMiddleware>();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UsePathBase("/api/gamification");
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("./v1/swagger.json", "Collaby Gamification API v1");
    options.RoutePrefix = "swagger";
});

app.MapGet("/health/live", () => Results.Ok(new { status = "alive", service = "gamification" }));
app.MapGet("/health/ready", async (ServiceReadinessState state, GamificationDbContext dbContext, IServiceProvider services) =>
{
    if (!state.IsReady)
    {
        return Results.Json(
            new
            {
                status = "unhealthy",
                service = "gamification",
                reason = state.FailureReason ?? "Startup checks have not completed yet"
            },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    try
    {
        if (!await dbContext.Database.CanConnectAsync())
        {
            return Results.Json(
                new { status = "unhealthy", service = "gamification", reason = "PostgreSQL is unavailable" },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var mongoDatabase = services.GetRequiredService<IMongoDatabase>();
        await mongoDatabase.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));

        var redis = services.GetService<IConnectionMultiplexer>();
        if (redis is not null)
        {
            await redis.GetDatabase().PingAsync();
        }

        return Results.Ok(new { status = "ready", service = "gamification" });
    }
    catch (Exception ex)
    {
        return Results.Json(
            new { status = "unhealthy", service = "gamification", reason = ex.Message },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});
app.MapGet("/health", async (ServiceReadinessState state, GamificationDbContext dbContext, IServiceProvider services) =>
{
    if (!state.IsReady)
    {
        return Results.Json(
            new
            {
                status = "unhealthy",
                service = "gamification",
                reason = state.FailureReason ?? "Startup checks have not completed yet"
            },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    try
    {
        if (!await dbContext.Database.CanConnectAsync())
        {
            return Results.Json(
                new { status = "unhealthy", service = "gamification", reason = "PostgreSQL is unavailable" },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var mongoDatabase = services.GetRequiredService<IMongoDatabase>();
        await mongoDatabase.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));

        var redis = services.GetService<IConnectionMultiplexer>();
        if (redis is not null)
        {
            await redis.GetDatabase().PingAsync();
        }

        return Results.Ok(new { status = "ready", service = "gamification" });
    }
    catch (Exception ex)
    {
        return Results.Json(
            new { status = "unhealthy", service = "gamification", reason = ex.Message },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapControllers();
app.MapHub<GamificationHub>("/hubs/gamification");

try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<GamificationDbContext>();
    db.Database.Migrate();
    await scope.ServiceProvider.GetRequiredService<IProjectProgressBootstrapService>().EnsureSeededAsync();

    var mongo = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();
    mongo.RunCommand<BsonDocument>(new BsonDocument("ping", 1));

    var redis = scope.ServiceProvider.GetService<IConnectionMultiplexer>();
    if (redis is not null)
    {
        redis.GetDatabase().Ping();
    }

    readinessState.MarkReady();
    app.Logger.LogInformation("GamificationService startup checks completed. Service is ready.");
}
catch (Exception ex)
{
    readinessState.MarkFailed(ex.Message);
    app.Logger.LogCritical(ex, "GamificationService failed startup checks.");
    throw;
}

app.Run();

public partial class Program;
