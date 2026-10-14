namespace CallService.Application.Interfaces;

public interface IMessengerClient
{
    Task SendSystemMessageAsync(string chatId, string text, CancellationToken ct = default);
}
