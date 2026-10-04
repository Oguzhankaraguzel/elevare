using SharedKernel.Concrete;

namespace Domain.Entities.UserReminders;

public static class UserReminderErrors
{
    public static readonly Error NotFound = Error.NotFound("UserReminder.NotFound", "The reminder was not found.");
    public static readonly Error Unauthorized = Error.Failure("UserReminder.Unauthorized", "You are not authorized to access this reminder.");
}
