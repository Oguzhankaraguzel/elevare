using Application.Abstraction.Data;
using Application.Abstraction.Services;
using Domain.Entities.PublicLanguages;
using Domain.Entities.PublicPages;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Social;
using SharedKernel.Concrete;

namespace Application.Features.Queries.Pages.GetPublicPageBySlug;

internal sealed class GetPublicPageBySlugQueryHandler(IPublicReadDbContext db, ICacheService cache)
    : IQueryHandler<GetPublicPageBySlugQuery, PublicPageResponse>
{
    // Short on purpose: the rendered page is output-cached far longer and dropped
    // together with this entry by /api/cache/clear; this only bounds how stale a
    // cache miss can be if that call never arrives.
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    public async Task<Result<PublicPageResponse>> Handle(
        GetPublicPageBySlugQuery request,
        CancellationToken cancellationToken)
    {
        // An empty slug is not an error — PageController never rewrites a bare "/"
        // (or "/{languageCode}") to anything else, so this IS the homepage request,
        // and PageInfo.ComputeFullSlug stores the homepage's own FullSlug the same
        // way: "" for the default language, or just the language code otherwise.

        // Only the successful/resolved case is cached — every failure path below
        // carries a cheap-to-recompute diagnostic message, and caching a "not
        // found" would delay a newly-published page from appearing for up to
        // CacheTtl for no real benefit.
        string cacheKey = $"page:{request.LanguageCode.ToUpperInvariant()}:{request.Slug.ToUpperInvariant()}";
        PublicPageResponse? cached = await cache.GetAsync<PublicPageResponse>(cacheKey, cancellationToken);
        if (cached is not null)
            return Result.Success(cached);

        // PageInfo.ComputeFullSlug (CMS side) omits the language prefix for the
        // default language and includes it for every other language — reconstruct
        // the same key here so the two sides agree regardless of which language
        // is currently marked default. For a non-default homepage request (empty
        // slug) that key is just the language code on its own — never a trailing
        // "/" with nothing after it.
        string defaultCode = await db.Languages
            .Where(l => l.IsDefault && l.IsActive)
            .Select(l => l.TwoLetterCode)
            .FirstOrDefaultAsync(cancellationToken) ?? "tr";

        bool isDefaultLanguage = string.Equals(request.LanguageCode, defaultCode, StringComparison.OrdinalIgnoreCase);

        string prefixed = request.Slug.Length == 0 ? request.LanguageCode : $"{request.LanguageCode}/{request.Slug}";
        string fullSlug = isDefaultLanguage ? request.Slug : prefixed;

        // Look the page up WITHOUT the published filter first, so a failed request
        // can report exactly why ("no such page" vs "exists but Draft") — the visitor
        // still just gets a 404 either way, but logs/devs see the real cause.
        PublicPage? page = await db.PageInfos
            .Include(p => p.Content)
            .FirstOrDefaultAsync(p => p.FullSlug == fullSlug, cancellationToken);

        if (page is null)
            return Result.Failure<PublicPageResponse>(PublicPageErrors.NotFound(fullSlug, request.LanguageCode, defaultCode));

        if (page.PageStatus != PublicPageStatus.Published)
            return Result.Failure<PublicPageResponse>(PublicPageErrors.NotPublished(fullSlug, page.PageStatus));

        if (!page.IsActive)
            return Result.Failure<PublicPageResponse>(PublicPageErrors.Inactive(fullSlug));

        // FullSlug alone does not say which language was asked for. With English
        // unpublished, "/en/about" no longer matches the language route, falls through
        // to the default-language fallback as the slug "en/about" — and that is exactly
        // the English page's FullSlug, so every page of an unpublished language kept
        // being served (hreflang and all). The page must belong to the language the
        // request resolved to, and that language must be one visitors may see.
        string? pageLanguageCode = await db.Languages
            .Where(PublicLanguage.PubliclyVisible)
            .Where(l => l.Id == page.LanguageId)
            .Select(l => l.TwoLetterCode)
            .FirstOrDefaultAsync(cancellationToken);
        if (!string.Equals(pageLanguageCode, request.LanguageCode, StringComparison.OrdinalIgnoreCase))
            return Result.Failure<PublicPageResponse>(PublicPageErrors.LanguageNotServed(fullSlug, request.LanguageCode));

        List<PageAlternateResponse> alternates = [];
        if (page.PageGroupId is not null)
        {
            alternates = await (
                from p in db.PageInfos
                join l in db.Languages on p.LanguageId equals l.Id
                where p.PageGroupId == page.PageGroupId
                   && p.Id != page.Id
                   && p.PageStatus == PublicPageStatus.Published
                   && p.IsActive
                   // An hreflang pointing at a language visitors cannot reach is a
                   // link to a 404 that search engines are told to treat as this page.
                   && l.IsActive && l.IsPublished
                select new PageAlternateResponse(l.TwoLetterCode, p.FullSlug, l.IsDefault))
                .ToListAsync(cancellationToken);
        }

        var response = new PublicPageResponse(
            page.Id,
            page.FullSlug,
            request.LanguageCode,
            page.SeoTitle,
            page.SeoMetaDescription,
            page.SeoMetaAuthor,
            page.SeoIsCanonical,
            page.SeoCanonicalUrl,
            page.SeoNoIndex,
            page.SeoNoFollow,
            page.SeoStructuredData,
            page.OgTitle,
            page.OgDescription,
            page.OgType,
            page.OgImage,
            page.OgUrl,
            page.TwitterCard,
            page.TwitterSite,
            page.Content?.GjsHtml,
            page.Content?.GjsCss,
            isDefaultLanguage,
            alternates,
            SocialMeta.FromJson(page.SeoSocialJson));

        await cache.SetAsync(cacheKey, response, CacheTtl, cancellationToken);
        return Result.Success(response);
    }
}
