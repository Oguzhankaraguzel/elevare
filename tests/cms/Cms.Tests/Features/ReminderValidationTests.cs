using Application.Features.Commands.UserReminders.UpdateReminder;
using Domain.Entities.UserReminders;
using FluentValidation.Results;
using Shouldly;

namespace Cms.Tests.Features;

/// <summary>
/// The update validator used to let an unset time through ("dt == default"), and the
/// handler stored it as 0001-01-01, which the reminder job fires at once.
/// </summary>
public sealed class ReminderValidationTests
{
    private static ValidationResult Validate(DateTime remindAt, bool isCompleted = false, bool isDismissed = false) =>
        new UpdateReminderCommandValidator().Validate(new UpdateReminderCommand(
            1, "Başlık", "Mesaj", remindAt, ReminderChannel.InApp, isCompleted, isDismissed, IsActive: true));

    [Fact]
    public void An_unset_time_is_refused()
    {
        Validate(default).IsValid.ShouldBeFalse();
        Validate(default, isCompleted: true).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void An_active_reminder_needs_a_future_time()
    {
        Validate(DateTime.UtcNow.AddMinutes(-5)).IsValid.ShouldBeFalse();
        Validate(DateTime.UtcNow.AddMinutes(5)).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Completing_or_dismissing_keeps_the_reminders_past_time()
    {
        Validate(DateTime.UtcNow.AddMinutes(-5), isCompleted: true).IsValid.ShouldBeTrue();
        Validate(DateTime.UtcNow.AddMinutes(-5), isDismissed: true).IsValid.ShouldBeTrue();
    }
}
