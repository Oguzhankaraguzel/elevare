using FluentValidation;

namespace Application.Features.Commands.UserReminders.UpdateReminder;

internal sealed class UpdateReminderCommandValidator : AbstractValidator<UpdateReminderCommand>
{
    public UpdateReminderCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Message).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.Channel).IsInEnum();
        RuleFor(x => x.RemindAt)
            .Must(dt => dt == default || dt > DateTime.UtcNow)
            .WithMessage("The reminder time cannot be in the past.")
            .When(x => !x.IsCompleted && !x.IsDismissed);
    }
}
