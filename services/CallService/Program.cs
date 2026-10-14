using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Text;
using Collaby.EventBus;
using Collaby.ServiceDefaults.Correlation;
using Collaby.ServiceDefaults.Health;
using Collaby.ServiceDefaults.Observability;
using StackExchange.Redis;
using MongoDB.Driver;
using FluentValidation;
using FluentValidation.AspNetCore;
using Collaby.ServiceDefaults.InternalAuth;
using CallService.Middleware;
using CallService.Application.Interfaces;
using CallService.Application.Services;
using CallService.Application.Validators;
using CallService.Configuration;
using CallService.Infrastructure.Clients;
using CallService.Infrastructure.Services;
using CallService.Infrastructure.Data;
using CallService.Hubs;

var builder = WebApplication.CreateBuilder(args);
builder.AddCollabyServiceObservability("call-service");

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase));
    });

builder.Services.AddValidatorsFromAssemblyContaining<StartCallRequestValidator>();
builder.Services.AddFluentValidationAutoValidation();

// Swagger Р Р…РЎС“Р В¶Р ВµР Р… Р С”Р В°Р С” РЎвЂЎР ВµР В»Р С•Р Р†Р ВµР С”Р С•РЎвЂЎР С‘РЎвЂљР В°Р ВµР СР В°РЎРЏ Р Р†Р С‘РЎвЂљРЎР‚Р С‘Р Р…Р В° Р С—РЎС“Р В±Р В»Р С‘РЎвЂЎР Р…Р С•Р С–Р С• API Р В·Р Р†Р С•Р Р…Р С”Р С•Р Р†.
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Collaby Call API",
        Version = "v1",
        Description = "Audio/video calls, screen sharing, and WebRTC signaling service"
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

// JWT используется и для HTTP-запросов, и для SignalR-подключений к хабу звонков.
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

        // SignalR WebSocket-Р С”Р В»Р С‘Р ВµР Р…РЎвЂљРЎвЂ№ Р С—Р ВµРЎР‚Р ВµР Т‘Р В°РЎР‹РЎвЂљ JWT РЎвЂЎР ВµРЎР‚Р ВµР В· query string, Р С—Р С•РЎРЊРЎвЂљР С•Р СРЎС“ РЎвЂљР С•Р С”Р ВµР Р… Р Р…РЎС“Р В¶Р Р…Р С• Р С—Р С•Р Т‘РЎвЂ¦Р Р†Р В°РЎвЂљР С‘РЎвЂљРЎРЉ Р Р†РЎР‚РЎС“РЎвЂЎР Р…РЎС“РЎР‹.
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

// SignalR Р С•Р В±РЎРѓР В»РЎС“Р В¶Р С‘Р Р†Р В°Р ВµРЎвЂљ signaling-РЎвЂЎР В°РЎРѓРЎвЂљРЎРЉ Р В·Р Р†Р С•Р Р…Р С”Р В°, Р В° Р СР ВµР Т‘Р С‘Р В°Р С—Р С•РЎвЂљР С•Р С” Р С‘Р Т‘РЎвЂРЎвЂљ Р СР ВµР В¶Р Т‘РЎС“ Р С”Р В»Р С‘Р ВµР Р…РЎвЂљР В°Р СР С‘ РЎвЂЎР ВµРЎР‚Р ВµР В· WebRTC.
builder.Services.AddSignalR()
    .AddJsonProtocol(o =>
    {
        o.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

builder.Services.AddAuthorization();
builder.Services.AddCollabyEventBus(builder.Configuration, "call-service");
builder.Services.AddHttpContextAccessor();
builder.Services.AddInternalServiceAuthentication(builder.Configuration);
builder.Services.AddSingleton<ServiceReadinessState>();

// MongoDB РЎвЂ¦РЎР‚Р В°Р Р…Р С‘РЎвЂљ РЎРѓР С•РЎРѓРЎвЂљР С•РЎРЏР Р…Р С‘Р Вµ РЎРѓР ВµРЎРѓРЎРѓР С‘Р в„– Р В·Р Р†Р С•Р Р…Р С”Р С•Р Р† Р С‘ РЎРѓР Р†РЎРЏР В·Р В°Р Р…Р Р…РЎвЂ№Р Вµ Р Т‘Р С•Р С”РЎС“Р СР ВµР Р…РЎвЂљРЎвЂ№.
var mongoConnectionString = builder.Configuration.GetConnectionString("Mongo")
    ?? throw new InvalidOperationException("Connection string 'Mongo' not found");

var mongoClient = new MongoClient(mongoConnectionString);
var mongoDatabase = mongoClient.GetDatabase("collaby_calls");

builder.Services.AddSingleton<IMongoClient>(mongoClient);
builder.Services.AddSingleton(mongoDatabase);
builder.Services.AddSingleton(new CallService.Infrastructure.Data.CallMongoDatabaseAccessor(mongoDatabase));

// Redis используется для отслеживания активных участников звонков и краткоживущего runtime-state.
var redisConnectionString = builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("Connection string 'Redis' not found");

var redis = ConnectionMultiplexer.Connect(redisConnectionString);
builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
builder.Services.AddSingleton(new CallService.Infrastructure.Data.CallRedisConnectionAccessor(redis));

// Р С›РЎРѓР Р…Р С•Р Р†Р Р…РЎвЂ№Р Вµ Р С—РЎР‚Р С‘Р С”Р В»Р В°Р Т‘Р Р…РЎвЂ№Р Вµ РЎРѓР ВµРЎР‚Р Р†Р С‘РЎРѓРЎвЂ№ Р Т‘Р С•Р СР ВµР Р…Р В° Р В·Р Р†Р С•Р Р…Р С”Р С•Р Р†.
builder.Services.AddScoped<ICallService, CallSessionService>();
builder.Services.AddSingleton<ICallParticipantTracker, RedisCallParticipantTracker>();
builder.Services.AddScoped<AdminUserDeletionPurgeService>();
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

builder.Services.AddOptions<MessengerServiceOptions>()
    .Bind(builder.Configuration.GetSection("MessengerService"))
    .Validate(
        options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _),
        "MessengerService:BaseUrl must be an absolute URI.")
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.CallerService),
        "MessengerService:CallerService must be configured.")
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.InternalApiKey),
        "MessengerService:InternalApiKey must be configured.")
    .ValidateOnStart();

// UserService нужен для обогащения данных участников.
builder.Services.AddHttpClient<IUserLookupClient, UserLookupClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<UserServiceOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});

// MessengerService получает system messages о звонках в чатах.
builder.Services.AddHttpClient<IMessengerClient, MessengerClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<MessengerServiceOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});

// Р СџРЎС“Р В±Р В»Р С‘РЎвЂЎР Р…РЎвЂ№Р в„– frontend Р С‘ Р В»Р С•Р С”Р В°Р В»РЎРЉР Р…Р В°РЎРЏ РЎР‚Р В°Р В·РЎР‚Р В°Р В±Р С•РЎвЂљР С”Р В° Р С‘РЎРѓР С—Р С•Р В»РЎРЉР В·РЎС“РЎР‹РЎвЂљ Р С•Р Т‘Р С‘Р Р… Р С‘ РЎвЂљР С•РЎвЂљ Р В¶Р Вµ Р Т‘Р С•Р СР ВµР Р…Р Р…РЎвЂ№Р в„– CORS-Р С”Р С•Р Р…РЎвЂљРЎР‚Р В°Р С”РЎвЂљ.
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
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();
var readinessState = app.Services.GetRequiredService<ServiceReadinessState>();

app.UseCollabyCorrelation();
app.UseErrorHandling();
app.UseForwardedHeaders();
app.UsePathBase("/api/calls");
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("./v1/swagger.json", "Collaby Call API v1");
    c.RoutePrefix = "swagger";
});

app.MapControllers();
app.MapHub<CallHub>("/hubs/call");
app.MapGet("/health/live", () => Results.Ok(new { status = "alive", service = "calls" }));
app.MapGet("/health/ready", async (ServiceReadinessState state, IMongoDatabase mongoDatabase, IConnectionMultiplexer redisMultiplexer) =>
{
    if (!state.IsReady)
    {
        return Results.Json(
            new
            {
                status = "unhealthy",
                service = "calls",
                reason = state.FailureReason ?? "Startup checks have not completed yet"
            },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    try
    {
        await mongoDatabase.RunCommandAsync<MongoDB.Bson.BsonDocument>(new MongoDB.Bson.BsonDocument("ping", 1));
        await redisMultiplexer.GetDatabase().PingAsync();
        return Results.Ok(new { status = "ready", service = "calls" });
    }
    catch (Exception ex)
    {
        return Results.Json(
            new { status = "unhealthy", service = "calls", reason = ex.Message },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});
app.MapGet("/health", async (ServiceReadinessState state, IMongoDatabase mongoDatabase, IConnectionMultiplexer redisMultiplexer) =>
{
    if (!state.IsReady)
    {
        return Results.Json(
            new
            {
                status = "unhealthy",
                service = "calls",
                reason = state.FailureReason ?? "Startup checks have not completed yet"
            },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    try
    {
        await mongoDatabase.RunCommandAsync<MongoDB.Bson.BsonDocument>(new MongoDB.Bson.BsonDocument("ping", 1));
        await redisMultiplexer.GetDatabase().PingAsync();
        return Results.Ok(new { status = "ready", service = "calls" });
    }
    catch (Exception ex)
    {
        return Results.Json(
            new { status = "unhealthy", service = "calls", reason = ex.Message },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

// Р ВР Р…Р Т‘Р ВµР С”РЎРѓРЎвЂ№ РЎС“РЎРѓР С”Р С•РЎР‚РЎРЏРЎР‹РЎвЂљ РЎвЂЎРЎвЂљР ВµР Р…Р С‘Р Вµ РЎРѓР С—Р С‘РЎРѓР С”Р В° Р В·Р Р†Р С•Р Р…Р С”Р С•Р Р† Р С‘ РЎРѓР Р†РЎРЏР В·Р В°Р Р…Р Р…РЎвЂ№РЎвЂ¦ РЎРѓР С•Р В±РЎвЂ№РЎвЂљР С‘Р в„–, Р Р…Р С• Р С‘РЎвЂ¦ Р С•РЎвЂљРЎРѓРЎС“РЎвЂљРЎРѓРЎвЂљР Р†Р С‘Р Вµ Р Р…Р Вµ Р Т‘Р С•Р В»Р В¶Р Р…Р С• Р В±Р В»Р С•Р С”Р С‘РЎР‚Р С•Р Р†Р В°РЎвЂљРЎРЉ РЎРѓРЎвЂљР В°РЎР‚РЎвЂљ РЎРѓР ВµРЎР‚Р Р†Р С‘РЎРѓР В°.
try
{
    await MongoDbInitializer.InitializeAsync(mongoDatabase);
    app.Logger.LogInformation("MongoDB indexes created for CallService.");
    await mongoDatabase.RunCommandAsync<MongoDB.Bson.BsonDocument>(new MongoDB.Bson.BsonDocument("ping", 1));
    await redis.GetDatabase().PingAsync();
    readinessState.MarkReady();
}
catch (Exception ex)
{
    readinessState.MarkFailed(ex.Message);
    app.Logger.LogWarning(ex, "Failed to create MongoDB indexes. Service will continue without them.");
}

app.Run();
