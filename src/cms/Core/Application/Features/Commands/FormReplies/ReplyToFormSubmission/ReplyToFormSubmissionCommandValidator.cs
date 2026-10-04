using FluentValidation;

namespace Application.Features.Commands.FormReplies.ReplyToFormSubmission;

internal sealed class ReplyToFormSubmissionCommandValidator : AbstractValidator<ReplyToFormSubmissionCommand>
{
    public ReplyToFormSubmissionCommandValidator()
    {
        RuleFor(x => x.ToEmail).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Body).NotEmpty();
    }
}
