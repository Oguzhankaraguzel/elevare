using SharedKernel.Concrete;

namespace Domain.Entities.Redirects;

public static class RedirectErrors
{
    public static readonly Error NotFound = Error.NotFound("Redirect.NotFound", "The redirect rule was not found.");
    public static readonly Error OldPathAlreadyExists = Error.Conflict("Redirect.OldPathAlreadyExists", "A redirect rule for this path already exists.");
    public static readonly Error CircularRedirect = Error.Failure("Redirect.CircularRedirect", "This would create a circular redirect loop.");
    public static readonly Error OldPathCannotMatchNewPath = Error.Failure("Redirect.OldPathCannotMatchNewPath", "The old path and new path cannot be identical.");
    public static readonly Error OldPathRequired = Error.Failure("Redirect.OldPathRequired", "The path to redirect from is required.");

    /// <summary>
    /// Refused rather than allowed-with-a-warning: a rule sitting on a live page's
    /// own URL makes that page unreachable, and nothing on the Pages screen would
    /// hint at why.
    /// </summary>
    public static readonly Error ShadowsLivePage = Error.Conflict("Redirect.ShadowsLivePage", "A published page already lives at this path, so redirecting it would hide that page.");
}
