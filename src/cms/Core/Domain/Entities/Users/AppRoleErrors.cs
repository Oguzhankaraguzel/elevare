using SharedKernel.Concrete;

namespace Domain.Entities.Users;

public static class AppRoleErrors
{
    public static readonly Error NotFound = Error.NotFound("AppRole.NotFound", "The role was not found.");
    public static readonly Error NameAlreadyExists = Error.Conflict("AppRole.NameAlreadyExists", "A role with this name already exists.");
    public static readonly Error CannotDeleteBuiltInRole = Error.Failure("AppRole.CannotDeleteBuiltInRole", "Built-in roles cannot be deleted.");
    public static readonly Error CannotDeleteRoleInUse = Error.Conflict("AppRole.CannotDeleteRoleInUse", "This role cannot be deleted because one or more users are assigned to it.");
    public static readonly Error CannotDeleteRoleInWorkflow = Error.Conflict("AppRole.CannotDeleteRoleInWorkflow", "This role cannot be deleted because an approval workflow step requires it.");

    public static Error CreateFailed(string details) => Error.Failure("Role.CreateFailed", details);
    public static Error DeleteFailed(string details) => Error.Failure("Role.DeleteFailed", details);
}
