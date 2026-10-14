using ManagementService.Application.Interfaces;
using ManagementService.Domain.Entities;
using ManagementService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ManagementService.Application.Services;

public sealed class ServiceRestartWorker(IServiceScopeFactory scopes, ILogger<ServiceRestartWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ManagementDbContext>();
                var client = scope.ServiceProvider.GetRequiredService<IServiceRestartClient>();
                var operations = await db.ServiceRestartOperations.Where(o => o.Status == "queued" || o.Status == "triggered" || o.Status == "running").OrderBy(o => o.RequestedAtUtc).ToListAsync(stoppingToken);
                foreach (var operation in operations)
                {
                    try { await client.TrySyncAsync(operation, stoppingToken); }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                    catch (HttpRequestException ex) when (ex.StatusCode is null or System.Net.HttpStatusCode.Conflict or System.Net.HttpStatusCode.TooManyRequests || (int)ex.StatusCode >= 500)
                    {
                        logger.LogWarning("Kubernetes API временно недоступен для операции {OperationId}", operation.Id);
                    }
                    catch (OperationCanceledException)
                    {
                        logger.LogWarning("Kubernetes API превысил время ожидания для операции {OperationId}", operation.Id);
                    }
                    catch (Exception ex)
                    {
                        operation.Status = ServiceRestartOperationStatuses.Failed;
                        operation.FailureMessage = ex.Message[..Math.Min(ex.Message.Length, 2000)];
                        operation.UpdatedAtUtc = DateTime.UtcNow;
                        operation.CompletedAtUtc = DateTime.UtcNow;
                        logger.LogWarning(ex, "Ошибка операции перезапуска {OperationId}", operation.Id);
                    }
                    await db.SaveChangesAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex) { logger.LogWarning(ex, "Обработчик перезапуска ожидает готовности БД"); }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
