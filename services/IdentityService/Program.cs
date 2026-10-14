using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using IdentityService.Infrastructure.Data;
using IdentityService.Infrastructure.Health;
using IdentityService.Application.Interfaces;
using IdentityService.Application.Services;
using IdentityService.Application.Settings;
using IdentityService.Middleware;
using Npgsql;
using MongoDB.Driver;
using StackExchange.Redis;
using FluentValidation;
using FluentValidation.AspNetCore;
using IdentityService.Contracts.Requests;
using Collaby.EventBus;
using Collaby.ServiceDefaults.Correlation;
using Microsoft.OpenApi;
using Collaby.ServiceDefaults.InternalAuth;
using Collaby.ServiceDefaults.Observability;
using Collaby.ServiceDefaults.SystemAdministration;

var builder = WebApplication.CreateBuilder(args);
builder.AddCollabyServiceObservability("identity-service");

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Сериализовать свойства в camelCase для совместимости с фронтенд (JavaScript)
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// Валидаторы регистрируются централизованно, чтобы DTO проверялись до входа в прикладную логику.
builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();
builder.Services.AddFluentValidationAutoValidation();

// Swagger нужен как быстрый контрактный срез Identity API для разработки и ручной диагностики.
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Collaby Identity API",
        Version = "v1",
        Description = "Authentication and user management service"
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

// PostgreSQL остаётся источником истины для identity-данных.
builder.Services.AddDbContext<IdentityDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres"));
});

// Mongo используется для вспомогательных коллекций и operational-логов.
builder.Services.AddSingleton<MongoDbContext>();
builder.Services.AddSingleton<IdentityServiceReadinessState>();

// Redis подключается как dependency для rate limiting; без него сервис остаётся рабочим, но без защитного слоя.
var redisConnection = builder.Configuration.GetConnectionString("Redis");
try
{
    if (!string.IsNullOrEmpty(redisConnection))
    {
        var redis = ConnectionMultiplexer.Connect(redisConnection);
        builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Failed to connect to Redis: {ex.Message}. Rate limiting disabled.");
}

// JWT-настройки и сервисы выносятся в DI как единый auth-контракт сервиса.
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<GoogleOptions>(builder.Configuration.GetSection("Google"));

// Аутентификация работает на локальной валидации подписи токена без внешнего authority.
var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>();
var signingKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
    System.Text.Encoding.UTF8.GetBytes(jwtSettings!.SigningKey));

builder.Services
    .AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.Authority = null;
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(5)
        };
    });

builder.Services.AddSystemAdministration(builder.Configuration);
builder.Services.AddCollabyEventBus(builder.Configuration, "identity-service");
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

var app = builder.Build();
var readinessState = app.Services.GetRequiredService<IdentityServiceReadinessState>();
var rateLimitingEnabled = builder.Configuration.GetValue<bool>("RateLimiting:Enabled");

// Порядок middleware важен: сначала error handling, затем rate limiting, затем auth-слой.
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

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost
});

app.UsePathBase("/api/auth");
app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

// Swagger включён и в runtime, чтобы сервис можно было диагностировать отдельно от frontend.
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("./v1/swagger.json", "Collaby Identity API v1");
    c.RoutePrefix = "swagger"; // Swagger UI должен работать и напрямую, и за host-aware swagger proxy.
});

app.MapControllers();

// Критические зависимости проверяются на старте с retry-логикой.
// Сервис считается готовым только после успешных миграций PostgreSQL и проверки Mongo.
using (var scope = app.Services.CreateScope())
{
    const int maxRetries = 5;
    const int delayMs = 3000;

    try
    {
        await ExecuteCriticalStartupActionAsync(
            operationName: "PostgreSQL migrations",
            action: async serviceProvider =>
            {
                var db = serviceProvider.GetRequiredService<IdentityDbContext>();
                await db.Database.MigrateAsync();
            });

        await ExecuteCriticalStartupActionAsync(
            operationName: "MongoDB connectivity",
            action: async serviceProvider =>
            {
                var mongo = serviceProvider.GetRequiredService<MongoDbContext>();
                await mongo.Database.RunCommandAsync<MongoDB.Bson.BsonDocument>(
                    new MongoDB.Bson.BsonDocument("ping", 1));
            });

        readinessState.MarkReady();
        app.Logger.LogInformation("IdentityService startup checks completed. Service is ready to accept requests.");
    }
    catch (Exception ex)
    {
        readinessState.MarkFailed(ex.Message);
        app.Logger.LogCritical(ex, "IdentityService startup failed because a critical dependency is unavailable.");
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
                    app.Logger.LogError(
                        ex,
                        "{OperationName} failed after {RetryCount} attempts.",
                        operationName,
                        maxRetries);
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
}

app.Run();
