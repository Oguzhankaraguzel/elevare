using SharedKernel.Concrete;

namespace Domain.Entities.Tags;

public static class TagErrors
{
    public static readonly Error InvalidName = Error.Failure("Tag.InvalidName", "A valid tag name is required.");

    public static readonly Error NotFound = Error.NotFound("Tag.NotFound", "The tag was not found.");

    /// <summary>
    /// Refused rather than merged: merging two tags silently re-labels every page
    /// carrying the loser, and nothing on screen would say it happened.
    /// </summary>
    public static readonly Error NameAlreadyExists = Error.Conflict(
        "Tag.NameAlreadyExists", "Another tag already uses this name.");
}
