namespace Collaby.ServiceDefaults.InternalAuth;

public sealed class InternalServiceAuthOptions
{
    public const string SectionName = "InternalAuth";

    public Dictionary<string, string> AllowedCallers { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
