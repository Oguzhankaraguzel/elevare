namespace Application.Services;

/// <summary>What <see cref="PageListingResolutionService"/> needs to know about the request.</summary>
/// <param name="CurrentPageId">The page being rendered — the "self" source's parent.</param>
/// <param name="BasePath">The page's own path ("/makaleler"), which every listing link starts from.</param>
/// <param name="LanguageCode">The page's language, for dates and accessible labels.</param>
/// <param name="Query">The request's query string; only listing parameters are read.</param>
public sealed record ListingRequest(
    int CurrentPageId, string BasePath, string LanguageCode, IReadOnlyDictionary<string, string?> Query);
