using SharedKernel.Concrete;

namespace Domain.Entities.PublicRedirects;

public static class PublicRedirectErrors
{
    public static Error NotFound(string fullSlug) =>
        Error.NotFound("Redirect.NotFound", $"No redirect rule exists for '{fullSlug}'.");

    public static Error NoTarget(string fullSlug) =>
        Error.NotFound("Redirect.NoTarget", $"Redirect rule for '{fullSlug}' has no resolvable target.");

    public static Error Loop(string fullSlug) =>
        Error.NotFound("Redirect.Loop", $"Redirect chain starting at '{fullSlug}' loops back on itself.");

    public static Error TooLong(string fullSlug) =>
        Error.NotFound("Redirect.Loop", $"Redirect chain starting at '{fullSlug}' is too long to resolve.");
}
