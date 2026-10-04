namespace Application.Services;

/// <summary>
/// Output of <see cref="TemplateResolutionService"/>: the page HTML with every
/// linked-template marker replaced, plus the CSS collected from the templates that
/// were substituted in.
/// </summary>
/// <param name="Html">
/// The resolved markup. Null when the page had no HTML to begin with — the render
/// pipeline passes that straight through rather than treating it as an error.
/// </param>
/// <param name="Css">Concatenated CSS of the resolved templates; empty when none applied.</param>
public sealed record ResolvedTemplateContent(string? Html, string Css);
