using Application.Abstraction.Data;
using Application.Features.Commands.Pages;
using Domain.Entities.PageInfos;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;
using SharedKernel.Social;

namespace Application.Features.Queries.Pages.GetPageById;

internal sealed class GetPageByIdQueryHandler(ICmsApplicationDbContext db)
    : IQueryHandler<GetPageByIdQuery, PageEditorResponse>
{
    public async Task<Result<PageEditorResponse>> Handle(
        GetPageByIdQuery request,
        CancellationToken cancellationToken)
    {
        PageInfo? page = await db.PageInfos
            .AsNoTracking()
            .Include(p => p.Content)
            .Include(p => p.Tags)
            .Include(p => p.ExcludedSiteCodeSnippets)
            .Include(p => p.CreateUser)
            .Include(p => p.UpdateUser)
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (page is null)
            return Result.Failure<PageEditorResponse>(PageInfoErrors.NotFound);

        var response = new PageEditorResponse(
            page.Id,
            page.SeoMeta.Title,
            page.Slug,
            page.FullSlug,
            page.LanguageId,
            page.PageStatus,
            page.PendingStatus,
            page.Content?.GjsHtml,
            page.Content?.GjsCss,
            page.Content?.GjsData,
            new SeoMetaResponse(
                page.SeoMeta.IsCanonical,
                page.SeoMeta.CanonicalUrl,
                page.SeoMeta.MetaDescription,
                page.SeoMeta.MetaAuthor,
                page.SeoMeta.NoIndex,
                page.SeoMeta.NoFollow,
                page.SeoMeta.FocusKeyword,
                page.SeoMeta.StructuredData,
                page.SeoMeta.OgTitle,
                page.SeoMeta.OgDescription,
                page.SeoMeta.OgType,
                page.SeoMeta.OgImage,
                page.SeoMeta.OgUrl,
                page.SeoMeta.TwitterCard,
                page.SeoMeta.TwitterSite,
                SocialMeta.FromJson(page.SeoMeta.SocialJson)),
            page.ParentPageId,
            page.SeoMeta.SeoScore,
            page.Tags?.Select(t => t.Id).ToList() ?? [],
            page.Kind,
            new AuditInfoResponse(
                DisplayName(page.CreateUser),
                page.CreateDate,
                DisplayName(page.UpdateUser),
                page.UpdateDate,
                page.PublishedAt),
            page.ExcludedSiteCodeSnippets?.Select(s => s.Id).ToList() ?? [],
            PageFingerprint.Of(page));

        return Result.Success(response);
    }

    /// <summary>Full name when there is one, otherwise the username — never a raw id.</summary>
    private static string? DisplayName(Domain.Entities.Users.AppUser? user)
    {
        if (user is null) return null;
        return string.IsNullOrWhiteSpace(user.FullName) ? user.UserName : user.FullName;
    }
}
