using FluentValidation;

namespace Application.Features.Commands.UserTasks.CreateUserTaskComment;

internal sealed class CreateUserTaskCommentCommandValidator : AbstractValidator<CreateUserTaskCommentCommand>
{
    public CreateUserTaskCommentCommandValidator()
    {
        RuleFor(x => x.UserTaskId).GreaterThan(0);
        RuleFor(x => x.Comment).NotEmpty().MaximumLength(2000);
    }
}
