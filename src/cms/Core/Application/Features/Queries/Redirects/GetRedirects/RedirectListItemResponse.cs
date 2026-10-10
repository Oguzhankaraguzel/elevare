using Domain.Entities.Redirects;

namespace Application.Features.Queries.Redirects.GetRedirects;

/// <summary>
/// One redirect rule, already resolved and diagnosed. The health flags are computed
/// server-side because deciding them needs the whole rule set plus the live page
/// table — the screen cannot work that out from a page of rows.
/// </summary>
/// <param name="ResolvedTarget">
/// Where a visitor actually ends up today. For a rule bound to a page this is that
/// page's current FullSlug, not the frozen <see cref="Redirect.NewPath"/> snapshot.
/// Null means the rule answers 410 Gone.
/// </param>
/// <param name="SourcePageTitle">Title of the bound page, when the rule follows one.</param>
/// <param name="IsTemporary">Answers 302 rather than 301.</param>
public sealed record RedirectListItemResponse(
    int Id,
    string OldPath,
    string? NewPath,
    string? ResolvedTarget,
    int? SourcePageId,
    string? SourcePageTitle,
    RedirectReason Reason,
    bool IsTemporary,
    RedirectHealth Health,
    int ChainLength,
    DateTime CreateDate,
    DateTime? UpdateDate);
