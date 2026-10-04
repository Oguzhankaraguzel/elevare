using SharedKernel.Concrete;

namespace Application.Abstraction.Services;

/// <summary>
/// Failures from the request-time render pipeline — the services that rewrite
/// GrapeJS-authored HTML just before it is sent (linked templates, the language
/// switcher, page listings, system pages).
/// </summary>
public static class RenderErrors
{
    /// <summary>
    /// A resolution step could not complete. <paramref name="stage"/> names which one,
    /// because the pipeline runs several in sequence over the same HTML and knowing
    /// which link broke is the difference between a one-minute and a one-hour diagnosis.
    /// </summary>
    public static Error ResolutionFailed(string stage, string detail) =>
        Error.Problem("Render.ResolutionFailed", $"Resolving {stage} failed: {detail}");
}
