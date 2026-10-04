using SharedKernel.Concrete;

namespace Domain.Entities.ContentBulkEdits;

public static class ContentBulkEditErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "ContentBulkEdit.NotFound", "The bulk edit history entry was not found.");

    public static readonly Error AlreadyReverted = Error.Failure(
        "ContentBulkEdit.AlreadyReverted", "This bulk edit has already been reverted.");

    public static readonly Error NoPagesSelected = Error.Failure(
        "ContentBulkEdit.NoPagesSelected", "Select at least one page to update.");

    public static readonly Error SearchTextRequired = Error.Failure(
        "ContentBulkEdit.SearchTextRequired", "Enter the text to search for.");
}
