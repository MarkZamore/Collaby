using System.ComponentModel.DataAnnotations;

namespace IdentityService.Contracts.Requests;

public sealed class CompleteGoogleRegistrationRequest
{
    [Required]
    public string RegistrationToken { get; set; } = string.Empty;

    [Required]
    [MinLength(3)]
    [MaxLength(50)]
    public string Nickname { get; set; } = string.Empty;
}
