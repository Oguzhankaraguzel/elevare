namespace Application.Services;

/// <summary>Resolved listing HTML plus the state of the page's own primary listing
/// block, if it has one — the raw material for a page-number-aware canonical,
/// rel=prev/next, robots and ItemList structured data in the response head, which
/// the resolver itself has no business rendering.</summary>
public sealed record PageListingResolution(string? Html, ListingPaginationInfo? Pagination);
