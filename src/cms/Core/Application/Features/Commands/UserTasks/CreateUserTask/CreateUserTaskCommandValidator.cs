using FluentValidation;

namespace Application.Features.Commands.UserTasks.CreateUserTask;

internal sealed class CreateUserTaskCommandValidator : AbstractValidator<CreateUserTaskCommand>
{
    public CreateUserTaskCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(2000);
        // No rule on AssignedToUserId: a task may start with nobody on it and get
        // an owner later.
        RuleFor(x => x.Priority).IsInEnum();
        RuleFor(x => x.DueDate)
            .GreaterThan(x => x.StartDate)
            .When(x => x.StartDate.HasValue && x.DueDate.HasValue)
            .WithMessage("Due date must be after start date.");
    }
}
