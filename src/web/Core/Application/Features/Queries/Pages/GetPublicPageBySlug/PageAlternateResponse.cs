namespace Application.Features.Queries.Pages.GetPublicPageBySlug;

/// <summary>One language alternate of the current page, for hreflang tags.</summary>
public sealed record PageAlternateResponse(string TwoLetterCode, string FullSlug, bool IsDefaultLanguage);
