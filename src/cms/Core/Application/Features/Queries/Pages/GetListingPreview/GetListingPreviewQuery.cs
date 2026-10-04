using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.Pages.GetListingPreview;

/// <summary>
/// What a "Sayfa Listesi" block on <paramref name="PageId"/> will list on the public
/// site — the same pages, in the same order, as PageListingResolutionService there —
/// plus the pages it will NOT list because they are not live, so the editor can say
/// why a page is missing instead of leaving the author to guess.
/// </summary>
/// <param name="PageId">The page the block is on; null in the template editor, where "self" has no meaning yet.</param>
/// <param name="Source">"self", "siblings", "specific" or "all" — the block's data-elevare-source.</param>
/// <param name="SourcePageId">The parent page for "specific".</param>
/// <param name="Sort">"newest", "oldest", "title-asc" or "title-desc".</param>
/// <param name="TagSlug">The block's default tag, if any.</param>
/// <param name="Take">How many to return — the block's items per page (capped).</param>
/// <param name="RelatedTags">Only pages sharing a tag with this one (data-elevare-related-tags).</param>
public sealed record GetListingPreviewQuery(
    int? PageId, string Source, int? SourcePageId, string Sort, string? TagSlug, int Take, bool RelatedTags = false)
    : IQuery<ListingPreviewResponse>;
