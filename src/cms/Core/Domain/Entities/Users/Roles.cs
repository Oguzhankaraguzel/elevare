namespace Domain.Entities.Users;

/// <summary>
/// Application-wide role name constants.
/// Keep in sync with the seed data in <c>DatabaseSeeder</c>.
/// </summary>
public static class Roles
{
    /// <summary>Unrestricted access to every feature including system configuration.</summary>
    public const string SuperAdmin = "SuperAdmin";

    /// <summary>Content and user management, cannot touch system-level settings.</summary>
    public const string Admin = "Admin";

    /// <summary>Trusted technical role; may author raw script/custom-code blocks in the page builder.</summary>
    public const string Developer = "Developer";

    /// <summary>Can edit, review and publish any content item.</summary>
    public const string Editor = "Editor";

    /// <summary>Can create and submit content for review; cannot publish directly.</summary>
    public const string Author = "Author";

    /// <summary>Read-only access to published content.</summary>
    public const string Viewer = "Viewer";
}
