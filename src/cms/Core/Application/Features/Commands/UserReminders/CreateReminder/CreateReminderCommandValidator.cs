using FluentValidation;

namespace Application.Features.Commands.UserReminders.CreateReminder;

internal sealed class CreateReminderCommandValidator : AbstractValidator<CreateReminderCommand>
{
    public CreateReminderCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Message).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.RemindAt).GreaterThan(DateTime.UtcNow).WithMessage("Reminder time must be in the future.");
        RuleFor(x => x.Channel).IsInEnum();
    }
}
