using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.Search.SearchPublicContent;

/// <summary>
/// Live search for the "Arama Kutusu" GrapeJS block. Matches published pages by
/// title, slug AND visible page content (not just metadata) in the given
/// language, returning enough data for the client to render any of its three
/// result layouts (plain list / cards / cards grouped by category) — "category"
/// here is this codebase's established meaning, the page's immediate parent in
/// the page hierarchy, not the separate Tag system.
/// </summary>
public sealed record SearchPublicContentQuery(string Term, string LanguageCode, int MaxResults = 12)
    : IQuery<List<SearchResultItemResponse>>;
