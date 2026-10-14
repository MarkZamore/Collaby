namespace MessengerService.Application.Interfaces;

public enum ClearChatStatus
{
    Cleared,
    NotFound,
    Forbidden,
}

public sealed record ClearChatResult(ClearChatStatus Status)
{
    public static readonly ClearChatResult Cleared = new(ClearChatStatus.Cleared);
    public static readonly ClearChatResult NotFound = new(ClearChatStatus.NotFound);
    public static readonly ClearChatResult Forbidden = new(ClearChatStatus.Forbidden);
}
