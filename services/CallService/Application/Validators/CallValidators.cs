using FluentValidation;
using CallService.Contracts.Requests;

namespace CallService.Application.Validators;

public class StartCallRequestValidator : AbstractValidator<StartCallRequest>
{
    public StartCallRequestValidator()
    {
        RuleFor(x => x.Type).Must(t => t is "audio" or "video").WithMessage("Type must be audio or video");
        RuleFor(x => x.ParticipantIds).NotEmpty().WithMessage("At least one participant is required");
    }
}

public class SignalMessageValidator : AbstractValidator<SignalMessage>
{
    public SignalMessageValidator()
    {
        RuleFor(x => x.CallId).NotEmpty();
        RuleFor(x => x.TargetUserId).NotEmpty();
        RuleFor(x => x.Type).Must(t => t is "offer" or "answer" or "ice-candidate")
            .WithMessage("Type must be offer, answer, or ice-candidate");
        RuleFor(x => x.Payload).NotNull();
    }
}
