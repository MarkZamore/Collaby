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
using MessengerService.Middleware;
using MessengerService.Application.Interfaces;
using MessengerService.Application.Services;
using MessengerService.Application.Validators;
using MessengerService.Configuration;
using MessengerService.Infrastructure.Clients;
using MessengerService.Infrastructure.Data;
using MessengerService.Hubs;

var builder = WebApplication.CreateBuilder(args);
builder.AddCollabyServiceObservability("messenger-service");

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase));
    });

builder.Services.AddValidatorsFromAssemblyContaining<CreatePrivateChatRequestValidator>();
builder.Services.AddFluentValidationAutoValidation();

// Swagger остаётся удобной точкой ручной диагностики API и контрактов сервиса.
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Collaby Messenger API",
        Version = "v1",
        Description = "Real-time messaging, chats, channels, and presence service"
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

// JWT-аутентификация используется и для HTTP, и для SignalR-подключений.
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

        // SignalR WebSocket-клиенты передают токен через query string, поэтому его нужно подхватить вручную.
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

// SignalR обслуживает realtime-доставку сообщений, presence и typing-события.
builder.Services.AddSignalR()
    .AddJsonProtocol(o =>
    {
        o.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

builder.Services.AddAuthorization();
builder.Services.AddCollabyEventBus(builder.Configuration, "messenger-service");
builder.Services.AddHttpContextAccessor();
builder.Services.AddInternalServiceAuthentication(builder.Configuration);
builder.Services.AddSingleton<ServiceReadinessState>();

// MongoDB хранит чаты, сообщения и связанные документы мессенджера.
var mongoConnectionString = builder.Configuration.GetConnectionString("Mongo")
    ?? throw new InvalidOperationException("Connection string 'Mongo' not found");

var mongoClient = new MongoClient(mongoConnectionString);
var mongoDatabase = mongoClient.GetDatabase("collaby_messenger");

builder.Services.AddSingleton<IMongoClient>(mongoClient);
builder.Services.AddSingleton(mongoDatabase);
builder.Services.AddSingleton(new MessengerService.Infrastructure.Data.MessengerMongoDatabaseAccessor(mongoDatabase));

// Redis нужен для presence и другого краткоживущего runtime-state.
var redisConnectionString = builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("Connection string 'Redis' not found");

var redis = ConnectionMultiplexer.Connect(redisConnectionString);
builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
builder.Services.AddSingleton(new MessengerService.Infrastructure.Data.MessengerRedisConnectionAccessor(redis));

// Основные application services инкапсулируют бизнес-операции мессенджера.
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IMessageService, MessageService>();
builder.Services.AddSingleton<IPresenceService, PresenceService>();
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

// UserService нужен только для enrichment данных участников и lookup профилей.
builder.Services.AddHttpClient<IUserLookupClient, UserLookupClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<UserServiceOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});

// CORS разрешает production frontend и локальные dev-origin.
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
app.UsePathBase("/api/messenger");
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("./v1/swagger.json", "Collaby Messenger API v1");
    c.RoutePrefix = "swagger";
});

app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");
app.MapGet("/health/live", () => Results.Ok(new { status = "alive", service = "messenger" }));
app.MapGet("/health/ready", async (ServiceReadinessState state, IMongoDatabase mongoDatabase, IConnectionMultiplexer redisMultiplexer) =>
{
    if (!state.IsReady)
    {
        return Results.Json(
            new
            {
                status = "unhealthy",
                service = "messenger",
                reason = state.FailureReason ?? "Startup checks have not completed yet"
            },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    try
    {
        await mongoDatabase.RunCommandAsync<MongoDB.Bson.BsonDocument>(new MongoDB.Bson.BsonDocument("ping", 1));
        await redisMultiplexer.GetDatabase().PingAsync();
        return Results.Ok(new { status = "ready", service = "messenger" });
    }
    catch (Exception ex)
    {
        return Results.Json(
            new { status = "unhealthy", service = "messenger", reason = ex.Message },
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
                service = "messenger",
                reason = state.FailureReason ?? "Startup checks have not completed yet"
            },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    try
    {
        await mongoDatabase.RunCommandAsync<MongoDB.Bson.BsonDocument>(new MongoDB.Bson.BsonDocument("ping", 1));
        await redisMultiplexer.GetDatabase().PingAsync();
        return Results.Ok(new { status = "ready", service = "messenger" });
    }
    catch (Exception ex)
    {
        return Results.Json(
            new { status = "unhealthy", service = "messenger", reason = ex.Message },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

// РРЅРґРµРєСЃС‹ Mongo РїРѕРґРЅРёРјР°СЋС‚СЃСЏ РЅР° СЃС‚Р°СЂС‚Рµ, С‡С‚РѕР±С‹ СѓСЃРєРѕСЂРёС‚СЊ РѕСЃРЅРѕРІРЅС‹Рµ Р·Р°РїСЂРѕСЃС‹ СЃРµСЂРІРёСЃР°.
try
{
    await MongoDbInitializer.InitializeAsync(mongoDatabase);
    app.Logger.LogInformation("MongoDB indexes created for MessengerService.");
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
