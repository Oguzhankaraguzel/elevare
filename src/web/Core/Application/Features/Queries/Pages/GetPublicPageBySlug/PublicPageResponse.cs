using SharedKernel.Social;

namespace Application.Features.Queries.Pages.GetPublicPageBySlug;

public sealed record PublicPageResponse(
    int Id,
    string FullSlug,
    string LanguageCode,
    string SeoTitle,
    string SeoMetaDescription,
    string SeoMetaAuthor,
    bool SeoIsCanonical,
    string? SeoCanonicalLink,
    bool SeoNoIndex,
    bool SeoNoFollow,
    string? SeoStructuredData,
    string OgTitle,
    string OgDescription,
    string OgType,
    string? OgImage,
    string? OgLink,
    string? TwitterCard,
    string? TwitterSite,
    string? GjsHtml,
    string? GjsCss,
    bool IsDefaultLanguage,
    List<PageAlternateResponse> Alternates,
    SocialMeta Social,
    // The translation group the hreflang alternates come from — the rendered
    // page is tagged with it, so a translation's change reaches its <head>.
    int? PageGroupId = null);
