namespace Application.Features.Queries.Pages.GetListingPreview;

/// <param name="PublishedCount">Pages the listing will show, across all its pages.</param>
/// <param name="Items">The first page of them, as their cards will read.</param>
/// <param name="Hidden">Pages under the same source that are not live (draft, archived, awaiting approval, inactive).</param>
public sealed record ListingPreviewResponse(
    int PublishedCount, IReadOnlyList<ListingPreviewItem> Items, IReadOnlyList<ListingPreviewHidden> Hidden);
