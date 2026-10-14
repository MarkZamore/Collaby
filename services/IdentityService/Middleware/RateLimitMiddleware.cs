using System.Net;
using StackExchange.Redis;

namespace IdentityService.Middleware;

/// <summary>
/// Middleware rate limiting для защиты от brute force-атак.
/// Использует Redis для подсчета количества запросов по IP адресу
/// Если Redis недоступен, продолжает работу без ограничений (graceful degradation)
/// </summary>
public class RateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<RateLimitMiddleware> _logger;
    private readonly int _maxRequests;
    private readonly int _windowSeconds;
    
    // Endpoints с повышенной защитой получают более низкий лимит запросов.
    private readonly string[] _strictEndpoints = new[]
    {
        "/auth/register",
        "/auth/login",
        "/auth/forgot-password",
        "/auth/reset-password",
        "/auth/check-nickname"
    };
    
    private readonly int _strictMaxRequests = 5; // 5 попыток
    private readonly int _strictWindowSeconds = 300; // за 5 минут

    public RateLimitMiddleware(RequestDelegate next, IConnectionMultiplexer? redis, ILogger<RateLimitMiddleware> logger)
    {
        _next = next;
        _redis = redis;
        _logger = logger;
        _maxRequests = 100; // 100 запросов
        _windowSeconds = 60; // за 60 секунд
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Без Redis сервис продолжает работать, но rate limiting временно отключается.
        if (_redis == null)
        {
            _logger.LogWarning("Redis not available, rate limiting disabled");
            await _next(context);
            return;
        }

        var ipAddress = GetClientIpAddress(context);
        var path = context.Request.Path.Value ?? "";
        
        // Определить лимит на основе endpoint
        var (maxRequests, windowSeconds) = IsStrictEndpoint(path) 
            ? (_strictMaxRequests, _strictWindowSeconds)
            : (_maxRequests, _windowSeconds);

        var key = $"identity:rate-limit:{ipAddress}:{path}";

        try
        {
            var db = _redis.GetDatabase();
            var currentCount = await db.StringGetAsync(key);
            var ttl = await db.KeyTimeToLiveAsync(key);

            if (!currentCount.IsNull)
            {
                var count = int.Parse(currentCount.ToString());
                var remainingTime = (int)(ttl?.TotalSeconds ?? 0);

                if (count >= maxRequests)
                {
                    _logger.LogWarning(
                        "Rate limit exceeded for IP: {IpAddress}, Path: {Path}, Limit: {Limit}, Remaining: {Remaining}s",
                        ipAddress, path, maxRequests, remainingTime);
                    
                    context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = "Too many requests",
                        message = $"Rate limit exceeded. Try again in {remainingTime} seconds.",
                        retryAfter = remainingTime
                    });
                    return;
                }

                await db.StringIncrementAsync(key);
                _logger.LogDebug("Rate limit check passed for IP: {IpAddress}, Path: {Path}, Count: {Count}/{Max}",
                    ipAddress, path, count + 1, maxRequests);
            }
            else
            {
                // Первый запрос в текущем окне создаёт базовую запись счётчика.
                await db.StringSetAsync(key, 1, TimeSpan.FromSeconds(windowSeconds));
                _logger.LogDebug("New rate limit window for IP: {IpAddress}, Path: {Path}",
                    ipAddress, path);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in RateLimitMiddleware for IP: {IpAddress}", ipAddress);
            // При сбое Redis не блокируем пользовательский поток, а деградируем без rate limiting.
        }

        await _next(context);
    }

    /// <summary>
    /// Получить IP адрес клиента, учитывая proxy заголовки
    /// </summary>
    private string GetClientIpAddress(HttpContext context)
    {
        // Проверить X-Forwarded-For header (для proxy)
        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
        {
            var ips = forwardedFor.ToString().Split(',');
            return ips[0].Trim();
        }

        // Проверить X-Real-IP header
        if (context.Request.Headers.TryGetValue("X-Real-IP", out var realIp))
        {
            return realIp.ToString();
        }

        // Использовать RemoteIpAddress
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    /// <summary>
    /// Проверить относится ли endpoint к strict endpoints с повышенной защитой
    /// </summary>
    private bool IsStrictEndpoint(string path)
    {
        return _strictEndpoints.Any(endpoint => 
            path.Contains(endpoint, StringComparison.OrdinalIgnoreCase));
    }
}

public static class RateLimitMiddlewareExtensions
{
    public static IApplicationBuilder UseRateLimit(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RateLimitMiddleware>();
    }
}
