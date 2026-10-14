using FluentValidation;
using MessengerService.Contracts.Requests;

namespace MessengerService.Application.Validators;

public class CreatePrivateChatRequestValidator : AbstractValidator<CreatePrivateChatRequest>
{
    public CreatePrivateChatRequestValidator()
    {
        RuleFor(x => x.ParticipantId).NotEmpty();
    }
}

public class CreateProjectChatRequestValidator : AbstractValidator<CreateProjectChatRequest>
{
    public CreateProjectChatRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Description).MaximumLength(1000).When(x => x.Description != null);
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.WorkspaceId).NotEmpty();
    }
}

public class CreateChannelRequestValidator : AbstractValidator<CreateChannelRequest>
{
    public CreateChannelRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Description).MaximumLength(1000).When(x => x.Description != null);
        RuleFor(x => x.WorkspaceId).NotEmpty();
    }
}

public class SendMessageRequestValidator : AbstractValidator<SendMessageRequest>
{
    public SendMessageRequestValidator()
    {
        RuleFor(x => x.Text).NotEmpty().MaximumLength(10000).When(x => x.Type == "text");
        RuleFor(x => x.Type).Must(t => t is "text" or "file" or "system").WithMessage("Type must be text, file, or system");
        RuleFor(x => x.File).NotNull().When(x => x.Type == "file").WithMessage("File is required for file messages");
        RuleFor(x => x.File!.FileName).NotEmpty().MaximumLength(500).When(x => x.File != null);
        RuleFor(x => x.File!.FileUrl).NotEmpty().MaximumLength(2048).When(x => x.File != null);
        RuleFor(x => x.File!.ContentType).NotEmpty().MaximumLength(255).When(x => x.File != null);
    }
}

public class UpdateMessageRequestValidator : AbstractValidator<UpdateMessageRequest>
{
    public UpdateMessageRequestValidator()
    {
        RuleFor(x => x.Text).NotEmpty().MaximumLength(10000);
    }
}

public class ToggleReactionRequestValidator : AbstractValidator<ToggleReactionRequest>
{
    public ToggleReactionRequestValidator()
    {
        RuleFor(x => x.Emoji).NotEmpty().MaximumLength(16);
    }
}
