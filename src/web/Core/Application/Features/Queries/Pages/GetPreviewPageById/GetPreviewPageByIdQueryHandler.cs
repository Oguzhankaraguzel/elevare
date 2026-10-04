using Application.Abstraction.Data;
using Application.Features.Queries.Pages.GetPublicPageBySlug;
using Domain.Entities.PublicPages;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Social;
using SharedKernel.Concrete;

namespace Application.Features.Queries.Pages.GetPreviewPageById;

internal sealed class GetPreviewPageByIdQueryHandler(IPublicReadDbContext db)
    : IQueryHandler<GetPreviewPageByIdQuery, PublicPageResponse>
{
    public async Task<Result<PublicPageResponse>> Handle(
        GetPreviewPageByIdQuery request,
        CancellationToken cancellationToken)
    {
        // PublicPage's own query filter (!IsDeleted) already excludes soft-deleted
        // pages here — no extra filtering needed for that case.
        PublicPage? page = await db.PageInfos
            .Include(p => p.Content)
            .FirstOrDefaultAsync(p => p.Id == request.PageId, cancellationToken);

        if (page is null)
            return Result.Failure<PublicPageResponse>(PublicPageErrors.PreviewNotFound(request.PageId));

        string defaultCode = await db.Languages
            .Where(l => l.IsDefault && l.IsActive)
            .Select(l => l.TwoLetterCode)
            .FirstOrDefaultAsync(cancellationToken) ?? "tr";

        string languageCode = await db.Languages
            .Where(l => l.Id == page.LanguageId)
            .Select(l => l.TwoLetterCode)
            .FirstOrDefaultAsync(cancellationToken) ?? defaultCode;

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
                select new PageAlternateResponse(l.TwoLetterCode, p.FullSlug, l.IsDefault))
                .ToListAsync(cancellationToken);
        }

        bool isDefaultLanguage = string.Equals(languageCode, defaultCode, StringComparison.OrdinalIgnoreCase);

        var response = new PublicPageResponse(
            page.Id,
            page.FullSlug,
            languageCode,
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
            // Staged unsaved edits take priority over live content, so previewing an
            // already-Published page shows what the CMS user is currently working on
            // rather than what real visitors see. Falls back to live content when no
            // preview snapshot has been staged yet (e.g. a brand-new Draft page).
            page.Content?.PreviewGjsHtml ?? page.Content?.GjsHtml,
            page.Content?.PreviewGjsCss ?? page.Content?.GjsCss,
            isDefaultLanguage,
            alternates,
            SocialMeta.FromJson(page.SeoSocialJson));

        return Result.Success(response);
    }
}
