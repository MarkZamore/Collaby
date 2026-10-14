namespace ManagementService.Platform.InternalAuth;

public enum InternalRequestAuthenticationFailure
{
    MissingConfiguration,
    MissingHeaders,
    UnknownCaller,
    InvalidCredential
}

public readonly record struct InternalRequestAuthenticationResult(
    bool IsAuthorized,
    string? CallerService,
    InternalRequestAuthenticationFailure? Failure);
