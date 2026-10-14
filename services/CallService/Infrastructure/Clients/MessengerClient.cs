using System.Net.Http.Json;
using Collaby.ServiceDefaults.InternalAuth;
using CallService.Application.Interfaces;
using CallService.Configuration;
using Microsoft.Extensions.Options;

namespace CallService.Infrastructure.Clients;

public class MessengerClient : IMessengerClient
{
    private readonly HttpClient _httpClient;
    private readonly MessengerServiceOptions _options;
    private readonly ILogger<MessengerClient> _logger;

    public MessengerClient(HttpClient httpClient, IOptions<MessengerServiceOptions> options, ILogger<MessengerClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendSystemMessageAsync(string chatId, string text, CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/messenger/chats/{chatId}/messages")
            {
                Content = JsonContent.Create(new
                {
                    text,
                    type = "system"
                })
            };

            request.AddInternalServiceAuthentication(_options.CallerService, _options.InternalApiKey);

            await _httpClient.SendAsync(request, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send system message to MessengerService for chat {ChatId}", chatId);
        }
    }
}
