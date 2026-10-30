using FluentValidation;

namespace Application.Features.Commands.UserReminders.CreateReminder;

internal sealed class CreateReminderCommandValidator : AbstractValidator<CreateReminderCommand>
{
    public CreateReminderCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Message).NotEmpty().MaximumLength(1000);
        // Read at validation time; GreaterThan(DateTime.UtcNow) would fix "now" when the validator is built.
        RuleFor(x => x.RemindAt).Must(dt => dt > DateTime.UtcNow).WithMessage("Reminder time must be in the future.");
        RuleFor(x => x.Channel).IsInEnum();
    }
}
