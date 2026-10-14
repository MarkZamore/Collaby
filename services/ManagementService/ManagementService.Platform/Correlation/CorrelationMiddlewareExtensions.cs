using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace ManagementService.Platform.Correlation;

public static class CorrelationMiddlewareExtensions
{
    public static IApplicationBuilder UseCollabyCorrelation(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var incomingCorrelationId = context.Request.Headers[CorrelationHeaderNames.CorrelationId].FirstOrDefault();

            using var scope = CorrelationContext.BeginScope(incomingCorrelationId);
            var correlationId = CorrelationContext.GetOrCreate();

            context.TraceIdentifier = correlationId;
            // Заголовок нужно выставлять перед отправкой ответа, иначе middleware может упасть на уже стартовавшем response.
            context.Response.OnStarting(static state =>
            {
                var (httpContext, responseCorrelationId) = ((HttpContext HttpContext, string CorrelationId))state;
                httpContext.Response.Headers[CorrelationHeaderNames.CorrelationId] = responseCorrelationId;
                return Task.CompletedTask;
            }, (context, correlationId));

            var activity = Activity.Current;
            activity?.SetTag("collaby.correlation_id", correlationId);
            activity?.SetBaggage("collaby.correlation_id", correlationId);

            await next(context);
        });
    }
}
