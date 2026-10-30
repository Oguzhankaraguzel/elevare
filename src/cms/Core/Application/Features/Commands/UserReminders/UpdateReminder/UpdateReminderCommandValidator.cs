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
        // The command replaces every field, so an unset time would be stored as
        // 0001-01-01 and the reminder job would fire it at once.
        RuleFor(x => x.RemindAt)
            .NotEqual(default(DateTime))
            .WithMessage("A reminder time is required.");

        // Completing or dismissing sends the reminder's own, by then past, time.
        RuleFor(x => x.RemindAt)
            .Must(dt => dt > DateTime.UtcNow)
            .WithMessage("The reminder time cannot be in the past.")
            .When(x => !x.IsCompleted && !x.IsDismissed && x.RemindAt != default);
    }
}
