namespace Domain.Entities.SiteCodeSnippets;

/// <summary>
/// Where in the public page a snippet is written out. The order of the values is
/// the order the page is assembled in, which is also the order the CMS lists them.
/// </summary>
public enum SiteCodePlacement
{
    /// <summary>
    /// Top of <c>&lt;head&gt;</c>, before analytics. Consent tools belong here: they
    /// work by blocking other scripts until the visitor agrees, which they can only
    /// do if the browser parses them first.
    /// </summary>
    HeadStart = 1,

    /// <summary>End of <c>&lt;head&gt;</c>. The default for ordinary tags.</summary>
    HeadEnd,

    /// <summary>
    /// Immediately after <c>&lt;body&gt;</c>. Required by Google Tag Manager's
    /// <c>&lt;noscript&gt;</c> half, which does nothing anywhere else.
    /// </summary>
    BodyStart,

    /// <summary>
    /// End of <c>&lt;body&gt;</c>. Best place for anything not needed to render:
    /// it cannot block first paint from here.
    /// </summary>
    BodyEnd
}
