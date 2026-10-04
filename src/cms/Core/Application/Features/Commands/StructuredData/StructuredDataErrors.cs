using SharedKernel.Concrete;

namespace Application.Features.Commands.StructuredData;

/// <summary>
/// Failures the structured-data feature can report. Each says what went wrong AND
/// what the operator has to do about it — a code alone sends them hunting.
/// </summary>
public static class StructuredDataErrors
{
    /// <summary>
    /// The page the draft was requested for no longer exists (deleted in another
    /// tab, most likely). Nothing to build against.
    /// </summary>
    public static Error PageNotFound(int pageId) => Error.NotFound(
        "StructuredData.PageNotFound",
        $"Page {pageId} was not found. It may have been deleted in another tab — refresh the page list.");

    /// <summary>
    /// Every absolute URL in the graph (@id values, page URL, breadcrumb links)
    /// is built from the public site address, so without it the draft could only
    /// contain relative paths — which JSON-LD consumers cannot resolve.
    /// </summary>
    public static readonly Error PublicSiteBaseUrlMissing = Error.Failure(
        "StructuredData.PublicSiteBaseUrlMissing",
        "The structured-data draft could not be built: Public Site URL is empty. "
        + "Every address in the schema is derived from it, so it has to be filled in.");
}
