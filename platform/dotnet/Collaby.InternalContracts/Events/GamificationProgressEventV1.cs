namespace Collaby.InternalContracts.Events;

public sealed record GamificationProgressEventV1(
    Guid UserId,
    int XpDelta,
    int CoinsDelta,
    int LevelAfterEvent,
    string? RewardCode = null);
