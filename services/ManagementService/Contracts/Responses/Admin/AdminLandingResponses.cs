using ManagementService.Contracts.Landing;

namespace ManagementService.Contracts.Responses.Admin;

public sealed class AdminLandingLocaleContentResponse
{
    public string Locale { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public string? UpdatedByEmail { get; set; }
    public LandingContentDocument Content { get; set; } = new();
}
