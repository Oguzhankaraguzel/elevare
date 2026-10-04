namespace Application.Services;

/// <param name="Page">1-based, matching the "page" query parameter.</param>
/// <param name="TotalPages">0 when the listing has nothing at all.</param>
/// <param name="Tag">The tag filter the URL asked for, if any — part of the canonical address.</param>
/// <param name="IsSearch">True when the listing is showing search results: not a page to index.</param>
/// <param name="ItemPaths">The listed pages' paths, in order.</param>
/// <param name="FirstPosition">The 1-based position of the first listed page across all pages.</param>
public sealed record ListingPaginationInfo(
    int Page, int TotalPages, string? Tag = null, bool IsSearch = false,
    IReadOnlyList<string>? ItemPaths = null, int FirstPosition = 1)
{
    /// <summary>A page number past the last page — there is nothing at that address.</summary>
    public bool IsOutOfRange => Page > Math.Max(1, TotalPages);
}
