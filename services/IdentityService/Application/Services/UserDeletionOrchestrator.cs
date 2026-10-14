using Collaby.EventBus;
using Collaby.EventBus.Abstractions;
using Collaby.InternalContracts.Events;
using Collaby.ServiceDefaults.Correlation;
using IdentityService.Application.Interfaces;
using IdentityService.Domain.Entities;
using IdentityService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Application.Services;

public sealed class UserDeletionOrchestrator : IUserDeletionOrchestrator
{
    private const string SystemRequestedByEmail = "system@collaby.local";

    private readonly IdentityDbContext _dbContext;
    private readonly IUserDeletionTargetCatalog _targetCatalog;
    private readonly IEventPublisher _eventPublisher;
    private readonly IIdentityDirectoryOutboxWriter _identityDirectoryOutboxWriter;
    private readonly ILogger<UserDeletionOrchestrator> _logger;

    public UserDeletionOrchestrator(
        IdentityDbContext dbContext,
        IUserDeletionTargetCatalog targetCatalog,
        IEventPublisher eventPublisher,
        IIdentityDirectoryOutboxWriter identityDirectoryOutboxWriter,
        ILogger<UserDeletionOrchestrator> logger)
    {
        _dbContext = dbContext;
        _targetCatalog = targetCatalog;
        _eventPublisher = eventPublisher;
        _identityDirectoryOutboxWriter = identityDirectoryOutboxWriter;
        _logger = logger;
    }

    public async Task<UserDeletionOperation> StartDeletionAsync(
        Guid targetUserId,
        Guid? requestedByUserId,
        string? requestedByEmail,
        CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .Include(entity => entity.RefreshTokens)
            .FirstOrDefaultAsync(entity => entity.Id == targetUserId, cancellationToken);

        if (user is null)
        {
            throw new AdminUserGovernanceException(StatusCodes.Status404NotFound, "user_not_found", "User not found.");
        }

        PrepareUserForDeletion(user);

        var existingOperation = await _dbContext.UserDeletionOperations
            .Include(entity => entity.Steps)
            .Where(operation => operation.UserId == targetUserId)
            .OrderByDescending(operation => operation.RequestedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingOperation is not null)
        {
            if (!UserDeletionRecoveryPolicy.CanRetry(existingOperation))
            {
                throw new AdminUserGovernanceException(
                    StatusCodes.Status409Conflict,
                    "hard_delete_already_started",
                    "Hard delete was already started for this user.");
            }

            UserDeletionRecoveryPolicy.PrepareForRetry(existingOperation);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return existingOperation;
        }

        var correlationId = CorrelationContext.GetOrCreate();
        var operation = new UserDeletionOperation
        {
            UserId = user.Id,
            UserEmail = user.Email,
            RequestedByUserId = requestedByUserId,
            RequestedByEmail = requestedByEmail,
            CorrelationId = correlationId,
            Status = UserDeletionOperationStatuses.Pending,
            RequestedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            Steps = _targetCatalog.GetEnabledTargets()
                .Select((target, index) => new UserDeletionOperationStep
                {
                    ServiceName = target.ServiceName,
                    StepOrder = index,
                    Status = UserDeletionOperationStepStatuses.Pending
                })
                .ToList()
        };

        _dbContext.UserDeletionOperations.Add(operation);
        _identityDirectoryOutboxWriter.Enqueue(user, "delete-started");
        await _dbContext.SaveChangesAsync(cancellationToken);

        return operation;
    }

    public Task<UserDeletionOperation?> GetOperationAsync(Guid operationId, CancellationToken cancellationToken = default)
    {
        return _dbContext.UserDeletionOperations
            .AsNoTracking()
            .Include(operation => operation.Steps)
            .FirstOrDefaultAsync(operation => operation.Id == operationId, cancellationToken);
    }

    public async Task<UserDeletionOperation> RetryAsync(Guid operationId, CancellationToken cancellationToken = default)
    {
        var operation = await _dbContext.UserDeletionOperations
            .Include(entity => entity.Steps)
            .FirstOrDefaultAsync(entity => entity.Id == operationId, cancellationToken);

        if (operation is null)
        {
            throw new AdminUserGovernanceException(StatusCodes.Status404NotFound, "operation_not_found", "Deletion operation not found.");
        }

        if (!UserDeletionRecoveryPolicy.CanRetry(operation))
        {
            throw new AdminUserGovernanceException(StatusCodes.Status409Conflict, "operation_not_retryable", "Only failed or stale deletion operations can be retried.");
        }

        UserDeletionRecoveryPolicy.PrepareForRetry(operation);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return operation;
    }

    public async Task ProcessPendingOperationsAsync(CancellationToken cancellationToken = default)
    {
        var operationIds = await _dbContext.UserDeletionOperations
            .AsNoTracking()
            .Where(operation =>
                operation.Status == UserDeletionOperationStatuses.Pending ||
                operation.Status == UserDeletionOperationStatuses.Running ||
                operation.Status == UserDeletionOperationStatuses.RetryRequested)
            .OrderBy(operation => operation.RequestedAtUtc)
            .Select(operation => operation.Id)
            .ToListAsync(cancellationToken);

        foreach (var operationId in operationIds)
        {
            await ProcessOperationAsync(operationId, cancellationToken);
        }
    }

    public async Task ApplyDeletionResultAsync(
        EventMetadata metadata,
        AdminUserDeletionResultV1 result,
        CancellationToken cancellationToken = default)
    {
        var operation = await _dbContext.UserDeletionOperations
            .Include(entity => entity.Steps)
            .FirstOrDefaultAsync(entity => entity.Id == result.OperationId, cancellationToken);

        if (operation is null)
        {
            _logger.LogWarning(
                "Received user deletion result for unknown operation {OperationId}.",
                result.OperationId);
            return;
        }

        using var correlationScope = CorrelationContext.BeginScope(metadata.CorrelationId);

        var step = operation.Steps.FirstOrDefault(entity =>
            string.Equals(entity.ServiceName, result.TargetService, StringComparison.OrdinalIgnoreCase));
        if (step is null)
        {
            _logger.LogWarning(
                "Received user deletion result for unknown target {TargetService} in operation {OperationId}.",
                result.TargetService,
                result.OperationId);
            return;
        }

        var now = DateTime.UtcNow;
        switch (result.Status)
        {
            case "completed":
                step.Status = UserDeletionOperationStepStatuses.Succeeded;
                step.LastError = null;
                break;
            case "noop":
                step.Status = UserDeletionOperationStepStatuses.Noop;
                step.LastError = null;
                break;
            case "failed":
                step.Status = UserDeletionOperationStepStatuses.Failed;
                step.LastError = result.FailureCode ?? "Deletion target reported a failure.";
                break;
            default:
                step.Status = UserDeletionOperationStepStatuses.Failed;
                step.LastError = $"Unknown deletion result status '{result.Status}'.";
                break;
        }

        step.CompletedAtUtc = now;
        operation.UpdatedAtUtc = now;

        if (string.Equals(step.Status, UserDeletionOperationStepStatuses.Failed, StringComparison.OrdinalIgnoreCase))
        {
            operation.Status = UserDeletionOperationStatuses.Failed;
            operation.FailureMessage = step.LastError;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        if (operation.Steps.All(stepEntity =>
                string.Equals(stepEntity.Status, UserDeletionOperationStepStatuses.Succeeded, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(stepEntity.Status, UserDeletionOperationStepStatuses.Noop, StringComparison.OrdinalIgnoreCase)))
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(entity => entity.Id == operation.UserId, cancellationToken);
            if (user is not null)
            {
                _identityDirectoryOutboxWriter.Enqueue(user, "delete-completed");
                _dbContext.Users.Remove(user);
            }

            operation.Status = UserDeletionOperationStatuses.Succeeded;
            operation.CompletedAtUtc = now;
            operation.FailureMessage = null;
        }
        else
        {
            operation.Status = UserDeletionOperationStatuses.Running;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ProcessOperationAsync(Guid operationId, CancellationToken cancellationToken)
    {
        var operation = await _dbContext.UserDeletionOperations
            .Include(entity => entity.Steps)
            .FirstOrDefaultAsync(entity => entity.Id == operationId, cancellationToken);

        if (operation is null)
        {
            return;
        }

        operation.Status = UserDeletionOperationStatuses.Running;
        operation.StartedAtUtc ??= DateTime.UtcNow;
        operation.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        using var correlationScope = CorrelationContext.BeginScope(operation.CorrelationId);

        foreach (var step in operation.Steps.OrderBy(step => step.StepOrder))
        {
            if (!string.Equals(step.Status, UserDeletionOperationStepStatuses.Pending, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var target = _targetCatalog.Find(step.ServiceName);
            if (target is null || !target.Enabled)
            {
                await MarkOperationFailedAsync(
                    operation,
                    step,
                    $"Target '{step.ServiceName}' is not configured for user purge.",
                    cancellationToken);
                return;
            }

            step.Status = UserDeletionOperationStepStatuses.Running;
            step.StartedAtUtc ??= DateTime.UtcNow;
            step.AttemptCount += 1;
            step.LastError = null;
            operation.UpdatedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            await _eventPublisher.PublishAsync(
                new EventPublishRequest<AdminUserDeletionCommandV1>(
                    EventTopicNames.AdminUserDeletionCommandsV1,
                    operation.UserId.ToString("D"),
                    "user-deletion-requested",
                    new AdminUserDeletionCommandV1(
                        operation.Id,
                        operation.UserId,
                        step.ServiceName,
                        operation.RequestedByEmail ?? SystemRequestedByEmail))
                {
                    CorrelationId = operation.CorrelationId,
                    CausationId = operation.Id.ToString("N"),
                    OccurredAtUtc = DateTime.UtcNow
                },
                cancellationToken);
        }
    }

    private async Task MarkOperationFailedAsync(
        UserDeletionOperation operation,
        UserDeletionOperationStep step,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        step.Status = UserDeletionOperationStepStatuses.Failed;
        step.LastError = failureMessage;
        step.CompletedAtUtc = DateTime.UtcNow;

        operation.Status = UserDeletionOperationStatuses.Failed;
        operation.FailureMessage = failureMessage;
        operation.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void PrepareUserForDeletion(User user)
    {
        user.IsActive = false;
        foreach (var refreshToken in user.RefreshTokens.Where(token => !token.RevokedAt.HasValue))
        {
            refreshToken.RevokedAt = DateTime.UtcNow;
        }
    }
}
