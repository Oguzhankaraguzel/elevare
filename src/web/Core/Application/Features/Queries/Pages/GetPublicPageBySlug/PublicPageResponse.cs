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
    SocialMeta Social);
