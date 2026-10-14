namespace UserService.Contracts.Requests;

public class UpdateOnboardingRequest
{
    public string? Status { get; set; }
    public int? CurrentStep { get; set; }
}

