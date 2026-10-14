using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ManagementService.Platform.InternalAuth;

public sealed class InternalServiceRequestAuthenticator
{
    private readonly IOptionsMonitor<InternalServiceAuthOptions> _optionsMonitor;
    private readonly ILogger<InternalServiceRequestAuthenticator> _logger;

    public InternalServiceRequestAuthenticator(
        IOptionsMonitor<InternalServiceAuthOptions> optionsMonitor,
        ILogger<InternalServiceRequestAuthenticator> logger)
    {
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public InternalRequestAuthenticationResult Authenticate(HttpRequest request)
    {
        var allowedCallers = _optionsMonitor.CurrentValue.AllowedCallers;
        if (allowedCallers.Count == 0)
        {
            _logger.LogWarning("Internal service authentication is not configured. Rejecting internal request.");
            return new InternalRequestAuthenticationResult(false, null, InternalRequestAuthenticationFailure.MissingConfiguration);
        }

        var callerService = request.Headers[InternalAuthHeaderNames.CallerService].FirstOrDefault()?.Trim();
        var providedCredential = request.Headers[InternalAuthHeaderNames.Credential].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(callerService) || string.IsNullOrWhiteSpace(providedCredential))
        {
            return new InternalRequestAuthenticationResult(false, callerService, InternalRequestAuthenticationFailure.MissingHeaders);
        }

        if (!allowedCallers.TryGetValue(callerService, out var expectedCredential))
        {
            _logger.LogWarning("Rejected internal request from unknown caller service {CallerService}.", callerService);
            return new InternalRequestAuthenticationResult(false, callerService, InternalRequestAuthenticationFailure.UnknownCaller);
        }

        if (!AreEqual(expectedCredential, providedCredential))
        {
            _logger.LogWarning("Rejected internal request from {CallerService} due to invalid credential.", callerService);
            return new InternalRequestAuthenticationResult(false, callerService, InternalRequestAuthenticationFailure.InvalidCredential);
        }

        return new InternalRequestAuthenticationResult(true, callerService, null);
    }

    private static bool AreEqual(string expected, string provided)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var providedBytes = Encoding.UTF8.GetBytes(provided);

        if (expectedBytes.Length != providedBytes.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }
}
