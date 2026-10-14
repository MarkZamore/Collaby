namespace MessengerService.Application.Interfaces;

public enum DeleteChatStatus
{
    Deleted,
    NotFound,
    Forbidden,
}

public sealed record DeleteChatResult(DeleteChatStatus Status)
{
    public static readonly DeleteChatResult Deleted = new(DeleteChatStatus.Deleted);
    public static readonly DeleteChatResult NotFound = new(DeleteChatStatus.NotFound);
    public static readonly DeleteChatResult Forbidden = new(DeleteChatStatus.Forbidden);
}
