using IdentityService.Application.Interfaces;

namespace IdentityService.Application.Services;

public sealed class UserDeletionBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<UserDeletionBackgroundService> _logger;

    public UserDeletionBackgroundService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<UserDeletionBackgroundService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var orchestrator = scope.ServiceProvider.GetRequiredService<IUserDeletionOrchestrator>();
                await orchestrator.ProcessPendingOperationsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background processing for user deletion operations failed.");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }
}
