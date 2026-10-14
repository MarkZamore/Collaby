using System.Text;
using CallService.Application.Interfaces;
using CallService.Application.Services;
using CallService.Application.Validators;
using CallService.Controllers;
using CallService.Hubs;
using CallService.Infrastructure.Data;
using CallService.Infrastructure.Services;
using Collaby.EventBus;
using Collaby.ServiceDefaults.Correlation;
using Collaby.ServiceDefaults.Health;
using Collaby.ServiceDefaults.InternalAuth;
using Collaby.ServiceDefaults.Observability;
using FluentValidation;
using FluentValidation.AspNetCore;
using MessengerService.Application.Interfaces;
using MessengerService.Application.Services;
using MessengerService.Application.Validators;
using MessengerService.Controllers;
using MessengerService.Hubs;
using MessengerService.Infrastructure.Data;
using MessengerService.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using MongoDB.Bson;
using MongoDB.Driver;
using RealtimeHost;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
builder.AddCollabyServiceObservability("realtime-service");

var messengerAssembly = typeof(ChatsController).Assembly;
var callsAssembly = typeof(CallsController).Assembly;
var modules = new[]
{
    new HostModuleDescriptor(messengerAssembly, "api/messenger", "messenger"),
    new HostModuleDescriptor(callsAssembly, "api/calls", "calls")
};

builder.Services
    .AddControllers(options =>
    {
        options.Conventions.Add(new HostModuleConvention(modules));
    })
    .AddApplicationPart(messengerAssembly)
    .AddApplicationPart(callsAssembly)
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase));
    });

builder.Services.AddValidatorsFromAssemblyContaining<CreatePrivateChatRequestValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<StartCallRequestValidator>();
builder.Services.AddFluentValidationAutoValidation();

builder.Services.AddSwaggerGen(options =>
{
    ConfigureBearerSecurity(options);
    ConfigureModuleSwagger(options, "messenger", "Collaby Messenger API", "Real-time messaging, chats, channels, and presence service");
    ConfigureModuleSwagger(options, "calls", "Collaby Call API", "Audio/video calls, screen sharing, and WebRTC signaling service");
    options.DocInclusionPredicate((documentName, apiDescription) =>
        string.Equals(apiDescription.GroupName, documentName, StringComparison.OrdinalIgnoreCase));
});

var messengerMongoConnectionString = builder.Configuration["ConnectionStrings:MessengerMongo"]
    ?? throw new InvalidOperationException("Connection string 'MessengerMongo' not found.");
var messengerRedisConnectionString = builder.Configuration["ConnectionStrings:MessengerRedis"]
    ?? throw new InvalidOperationException("Connection string 'MessengerRedis' not found.");
var callMongoConnectionString = builder.Configuration["ConnectionStrings:CallsMongo"]
    ?? throw new InvalidOperationException("Connection string 'CallsMongo' not found.");
var callRedisConnectionString = builder.Configuration["ConnectionStrings:CallsRedis"]
    ?? throw new InvalidOperationException("Connection string 'CallsRedis' not found.");

var messengerMongoClient = new MongoClient(messengerMongoConnectionString);
var messengerMongoDatabase = messengerMongoClient.GetDatabase("collaby_messenger");
var callMongoClient = new MongoClient(callMongoConnectionString);
var callMongoDatabase = callMongoClient.GetDatabase("collaby_calls");

var messengerRedis = ConnectionMultiplexer.Connect(messengerRedisConnectionString);
var callRedis = ConnectionMultiplexer.Connect(callRedisConnectionString);

builder.Services.AddSingleton(new MessengerMongoDatabaseAccessor(messengerMongoDatabase));
builder.Services.AddSingleton(new MessengerRedisConnectionAccessor(messengerRedis));
builder.Services.AddSingleton(new CallMongoDatabaseAccessor(callMongoDatabase));
builder.Services.AddSingleton(new CallRedisConnectionAccessor(callRedis));

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
                    (path.StartsWithSegments("/api/messenger/hubs") ||
                     path.StartsWithSegments("/api/calls/hubs") ||
                     path.Value?.Contains("/api/messenger/hubs/") == true ||
                     path.Value?.Contains("/api/calls/hubs/") == true))
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
builder.Services.AddCollabyEventBus(builder.Configuration, "realtime-service");
builder.Services.AddHttpContextAccessor();
builder.Services.AddInternalServiceAuthentication(builder.Configuration);
builder.Services.AddSingleton<ServiceReadinessState>();

builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IMessageService, MessageService>();
builder.Services.AddSingleton<IPresenceService, PresenceService>();
builder.Services.AddScoped<MessengerService.Application.Services.AdminUserDeletionPurgeService>();
builder.Services.AddHostedService<RealtimeMessengerAdminUserDeletionCommandConsumer>();

builder.Services.AddOptions<MessengerService.Configuration.UserServiceOptions>()
    .Bind(builder.Configuration.GetSection("MessengerService:UserService"))
    .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _), "MessengerService:UserService:BaseUrl must be an absolute URI.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.CallerService), "MessengerService:UserService:CallerService must be configured.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.InternalApiKey), "MessengerService:UserService:InternalApiKey must be configured.")
    .ValidateOnStart();

builder.Services.AddHttpClient<MessengerService.Application.Interfaces.IUserLookupClient, MessengerService.Infrastructure.Clients.UserLookupClient>("MessengerUserLookupClient", (serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<MessengerService.Configuration.UserServiceOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddScoped<ICallService, CallSessionService>();
builder.Services.AddSingleton<ICallParticipantTracker, RedisCallParticipantTracker>();
builder.Services.AddScoped<CallService.Application.Services.AdminUserDeletionPurgeService>();
builder.Services.AddScoped<IMessengerClient, InProcessMessengerClient>();
builder.Services.AddHostedService<RealtimeCallAdminUserDeletionCommandConsumer>();

builder.Services.AddOptions<CallService.Configuration.UserServiceOptions>()
    .Bind(builder.Configuration.GetSection("CallService:UserService"))
    .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _), "CallService:UserService:BaseUrl must be an absolute URI.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.CallerService), "CallService:UserService:CallerService must be configured.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.InternalApiKey), "CallService:UserService:InternalApiKey must be configured.")
    .ValidateOnStart();

builder.Services.AddHttpClient<CallService.Application.Interfaces.IUserLookupClient, CallService.Infrastructure.Clients.UserLookupClient>("CallUserLookupClient", (serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<CallService.Configuration.UserServiceOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});

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
    options.SwaggerEndpoint("./messenger/swagger.json", "Collaby Messenger API v1");
    options.SwaggerEndpoint("./calls/swagger.json", "Collaby Call API v1");
});

app.MapControllers();
app.MapHub<ChatHub>("/api/messenger/hubs/chat");
app.MapHub<CallHub>("/api/calls/hubs/call");

app.MapGet("/api/messenger/health/live", () => Results.Ok(new { status = "alive", service = "messenger" }));
app.MapGet("/api/messenger/health/ready", async (ServiceReadinessState state) =>
    await EvaluateMessengerHealthAsync(state, messengerMongoDatabase, messengerRedis));
app.MapGet("/api/messenger/health", async (ServiceReadinessState state) =>
    await EvaluateMessengerHealthAsync(state, messengerMongoDatabase, messengerRedis));

app.MapGet("/api/calls/health/live", () => Results.Ok(new { status = "alive", service = "calls" }));
app.MapGet("/api/calls/health/ready", async (ServiceReadinessState state) =>
    await EvaluateCallsHealthAsync(state, callMongoDatabase, callRedis));
app.MapGet("/api/calls/health", async (ServiceReadinessState state) =>
    await EvaluateCallsHealthAsync(state, callMongoDatabase, callRedis));

app.MapGet("/health/live", () => Results.Ok(new { status = "alive", service = "realtime" }));
app.MapGet("/health/ready", async (ServiceReadinessState state) =>
    await EvaluateRealtimeHealthAsync(state, messengerMongoDatabase, messengerRedis, callMongoDatabase, callRedis));
app.MapGet("/health", async (ServiceReadinessState state) =>
    await EvaluateRealtimeHealthAsync(state, messengerMongoDatabase, messengerRedis, callMongoDatabase, callRedis));

using (var scope = app.Services.CreateScope())
{
    try
    {
        await MessengerService.Infrastructure.Data.MongoDbInitializer.InitializeAsync(messengerMongoDatabase);
        await CallService.Infrastructure.Data.MongoDbInitializer.InitializeAsync(callMongoDatabase);
        readinessState.MarkReady();
        app.Logger.LogInformation("RealtimeHost MongoDB initializers completed successfully.");
    }
    catch (Exception ex)
    {
        readinessState.MarkFailed(ex.Message);
        app.Logger.LogError(ex, "RealtimeHost startup failed.");
        throw;
    }
}

app.Run();

return;

static async Task<IResult> EvaluateMessengerHealthAsync(
    ServiceReadinessState state,
    IMongoDatabase mongoDatabase,
    IConnectionMultiplexer redisMultiplexer)
{
    if (!state.IsReady)
    {
        return Results.Json(
            new { status = "unhealthy", service = "messenger", reason = state.FailureReason ?? "Startup checks have not completed yet" },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    try
    {
        await mongoDatabase.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
        await redisMultiplexer.GetDatabase().PingAsync();
        return Results.Ok(new { status = "ready", service = "messenger" });
    }
    catch (Exception ex)
    {
        return Results.Json(
            new { status = "unhealthy", service = "messenger", reason = ex.Message },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}

static async Task<IResult> EvaluateCallsHealthAsync(
    ServiceReadinessState state,
    IMongoDatabase mongoDatabase,
    IConnectionMultiplexer redisMultiplexer)
{
    if (!state.IsReady)
    {
        return Results.Json(
            new { status = "unhealthy", service = "calls", reason = state.FailureReason ?? "Startup checks have not completed yet" },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    try
    {
        await mongoDatabase.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
        await redisMultiplexer.GetDatabase().PingAsync();
        return Results.Ok(new { status = "ready", service = "calls" });
    }
    catch (Exception ex)
    {
        return Results.Json(
            new { status = "unhealthy", service = "calls", reason = ex.Message },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}

static async Task<IResult> EvaluateRealtimeHealthAsync(
    ServiceReadinessState state,
    IMongoDatabase messengerMongoDatabase,
    IConnectionMultiplexer messengerRedisMultiplexer,
    IMongoDatabase callMongoDatabase,
    IConnectionMultiplexer callRedisMultiplexer)
{
    if (!state.IsReady)
    {
        return Results.Json(
            new { status = "unhealthy", service = "realtime", reason = state.FailureReason ?? "Startup checks have not completed yet" },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    try
    {
        await messengerMongoDatabase.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
        await messengerRedisMultiplexer.GetDatabase().PingAsync();
        await callMongoDatabase.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
        await callRedisMultiplexer.GetDatabase().PingAsync();
        return Results.Ok(new { status = "ready", service = "realtime" });
    }
    catch (Exception ex)
    {
        return Results.Json(
            new { status = "unhealthy", service = "realtime", reason = ex.Message },
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
