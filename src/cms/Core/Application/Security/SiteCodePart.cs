using Domain.Entities.SiteCodeSnippets;

namespace Application.Security;

/// <summary>
/// One row a preset wants created. A preset yields several of these when the tag it
/// represents genuinely has several halves that live in different places.
/// </summary>
/// <param name="Suffix">
/// Appended to the author's name when a preset produces more than one row, so the
/// list reads "Tag Manager" and "Tag Manager (noscript)" rather than two identical
/// entries nobody can tell apart.
/// </param>
public sealed record SiteCodePart(
    SiteCodeKind Kind,
    SiteCodePlacement Placement,
    string Content,
    string? Suffix);
