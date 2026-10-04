using SharedKernel.Concrete;

namespace Domain.Entities.PublicLogs;

public static class PublicAppLogErrors
{
    public static readonly Error MissingMessage = Error.Failure("AppLog.MissingMessage", "A log entry needs a message.");
}
