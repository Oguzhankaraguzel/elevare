using FluentValidation;

namespace Application.Features.Commands.UserTasks.UpdateUserTask;

internal sealed class UpdateUserTaskCommandValidator : AbstractValidator<UpdateUserTaskCommand>
{
    public UpdateUserTaskCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(2000);
        // Assignment stays optional here too — clearing the owner is a legitimate
        // edit, not a validation error.
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Priority).IsInEnum();
        RuleFor(x => x.DueDate)
            .GreaterThan(x => x.StartDate)
            .When(x => x.StartDate.HasValue && x.DueDate.HasValue)
            .WithMessage("Due date must be after start date.");
    }
}
