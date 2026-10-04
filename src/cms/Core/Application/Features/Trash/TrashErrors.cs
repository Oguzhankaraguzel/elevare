using SharedKernel.Concrete;

namespace Application.Features.Trash;

public static class TrashErrors
{
    public static readonly Error NotFound = Error.NotFound("Trash.NotFound", "The item was not found in the Trash.");
    public static readonly Error UnknownType = Error.Failure("Trash.UnknownType", "Unknown trash entity type.");

    /// <summary>
    /// Purging is refused rather than cascaded: form submissions are visitor-entered
    /// records, not content, and destroying them to tidy up the Trash would be the
    /// worst possible trade.
    /// </summary>
    public static readonly Error CannotPurgeHasFormSubmissions = Error.Conflict(
        "Trash.CannotPurgeHasFormSubmissions",
        "This page still holds form submissions. Deleting it permanently would delete those enquiries with it.");

    /// <summary>
    /// A child row's ParentPageId is NOT NULL and non-cascading, so purging a parent
    /// out from under it would fail at the database anyway — refused here with a
    /// sentence that says what to do instead.
    /// </summary>
    public static readonly Error CannotPurgeHasChildren = Error.Conflict(
        "Trash.CannotPurgeHasChildren",
        "This page still has sub-pages. Delete those permanently first.");
}
