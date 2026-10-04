namespace Application.Features.Queries.Redirects.GetRedirects;

/// <summary>
/// What is wrong with a redirect rule, if anything. Flags rather than a single
/// verdict: one rule can be both a long chain and point at nothing, and hiding the
/// second problem behind the first is how half-fixes happen.
/// </summary>
[Flags]
public enum RedirectHealth
{
    None = 0,

    /// <summary>Sends visitors to a path that is not a published page — a redirect into a 404.</summary>
    BrokenTarget = 1,

    /// <summary>
    /// The target is itself the source of another rule, so a visitor is bounced twice
    /// or more. Works, but every hop costs a round trip and search engines stop
    /// following after a handful.
    /// </summary>
    Chained = 2,

    /// <summary>The chain comes back to where it started — a visitor would loop forever.</summary>
    Loop = 4,

    /// <summary>Answers 410 Gone. Deliberate, not a fault; surfaced so it is not mistaken for a broken rule.</summary>
    Gone = 8,

    /// <summary>Points at an absolute URL on another host, which this CMS cannot verify.</summary>
    External = 16,
}
