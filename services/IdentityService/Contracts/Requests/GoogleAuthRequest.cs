using System.ComponentModel.DataAnnotations;

namespace IdentityService.Contracts.Requests;

public class GoogleAuthRequest
{
    [Required]
    public string IdToken { get; set; } = string.Empty;
}
