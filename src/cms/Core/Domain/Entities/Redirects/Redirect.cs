using Domain.Entities.Abstractions;
using Domain.Entities.PageInfos;

namespace Domain.Entities.Redirects;

/// <summary>
/// Maps a path that no longer resolves to a page (old slug, deleted/archived page)
/// to a replacement target, so public visitors never dead-end on a 404.
/// </summary>
public class Redirect : BaseEntity
{
    /// <summary>The path that should no longer 404, in <c>FullSlug</c> form (no leading slash).</summary>
    public string OldPath { get; set; } = null!;

    /// <summary>
    /// Fallback target when <see cref="SourcePageId"/> is null, or when that page is
    /// itself no longer resolvable. Null means "410 Gone" (intentionally removed, no replacement).
    /// Can be an internal relative path or an absolute external URL.
    /// </summary>
    public string? NewPath { get; set; }

    /// <summary>
    /// When set, the redirect always resolves to THIS page's current <c>FullSlug</c>
    /// instead of the frozen <see cref="NewPath"/> snapshot — so a page renamed twice
    /// still redirects visitors correctly without a stale multi-hop chain.
    /// </summary>
    public int? SourcePageId { get; set; }

    public RedirectReason Reason { get; set; }

    /// <summary>
    /// Answer with 302 instead of 301. Only a rule that stands in for something
    /// expected to come back is temporary — a language taken off the site points its
    /// pages at the default language until it is published again — so search engines
    /// keep the original address rather than replacing it with the target.
    /// </summary>
    public bool IsTemporary { get; set; }

    public virtual PageInfo? SourcePage { get; set; }
}
