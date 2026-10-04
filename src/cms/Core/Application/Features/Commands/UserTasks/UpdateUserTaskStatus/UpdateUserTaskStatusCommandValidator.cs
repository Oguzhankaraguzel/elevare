using FluentValidation;

namespace Application.Features.Commands.UserTasks.UpdateUserTaskStatus;

internal sealed class UpdateUserTaskStatusCommandValidator : AbstractValidator<UpdateUserTaskStatusCommand>
{
    public UpdateUserTaskStatusCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Status).IsInEnum();
    }
}
