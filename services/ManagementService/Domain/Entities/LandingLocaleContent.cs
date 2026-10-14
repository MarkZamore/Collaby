namespace ManagementService.Domain.Entities;

public sealed class LandingLocaleContent
{
    public string Locale { get; set; } = string.Empty;
    public string ContentJson { get; set; } = "{}";
    public DateTime UpdatedAtUtc { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public string? UpdatedByEmail { get; set; }
}
