using Microsoft.AspNetCore.Identity;

namespace Application.Features.Commands.Users;

internal static class IdentityResultExtensions
{
    /// <summary>
    /// Identity's own messages, joined — "Passwords must have at least one digit" is
    /// actionable; "Update failed" sends the user to support. Carried as the detail
    /// of the entity's own error (AppUserErrors, AppRoleErrors, PasswordSetupErrors).
    /// </summary>
    public static string Describe(this IdentityResult result) =>
        string.Join(" ", result.Errors.Select(e => e.Description));
}
