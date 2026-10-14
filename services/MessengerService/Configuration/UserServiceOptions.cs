namespace MessengerService.Configuration;

public sealed class UserServiceOptions
{
    public string BaseUrl { get; init; } = string.Empty;
    public string CallerService { get; init; } = string.Empty;
    public string InternalApiKey { get; init; } = string.Empty;
}
