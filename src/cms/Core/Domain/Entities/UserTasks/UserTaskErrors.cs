using SharedKernel.Concrete;

namespace Domain.Entities.UserTasks;

public static class UserTaskErrors
{
    public static readonly Error NotFound = Error.NotFound("UserTask.NotFound", "The task was not found.");
    public static readonly Error Unauthorized = Error.Failure("UserTask.Unauthorized", "You are not authorized to access this task.");
}
