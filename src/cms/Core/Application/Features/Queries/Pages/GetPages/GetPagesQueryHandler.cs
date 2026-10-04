using Application.Abstraction.Data;
using Domain.Entities.PageInfos;
using Domain.Entities.Workflows;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.Pages.GetPages;

internal sealed class GetPagesQueryHandler(ICmsApplicationDbContext db)
    : IQueryHandler<GetPagesQuery, List<PageListItemResponse>>
{
    public async Task<Result<List<PageListItemResponse>>> Handle(
        GetPagesQuery request,
        CancellationToken cancellationToken)
    {
        // Cheap column filters run in SQL; the derived signals cannot, because two of
        // them are about other rows entirely (how many languages exist, what the
        // approval queue holds). The list has never been paginated — it fetches every
        // page either way — so composing the signals after materialising costs nothing
        // extra and keeps the rules readable.
        IQueryable<PageInfo> query = db.PageInfos.AsNoTracking();

        if (request.LanguageId is int langId)
            query = query.Where(p => p.LanguageId == langId);

        if (request.Status is PageStatus status)
            query = query.Where(p => p.PageStatus == status);

        if (request.Kinds is { Count: > 0 } kinds)
            query = query.Where(p => kinds.Contains(p.Kind));

        var rows = await query
            .OrderByDescending(p => p.UpdateDate ?? p.CreateDate)
            .Select(p => new
            {
                p.Id,
                p.SeoMeta.Title,
                p.Slug,
                p.FullSlug,
                p.LanguageId,
                LanguageName = p.Language.NameInNative,
                p.PageStatus,
                p.PendingStatus,
                p.CreateDate,
                p.UpdateDate,
                p.PageGroupId,
                p.ParentPageId,
                p.SeoMeta.SeoScore,
                p.Kind,
                p.SeoMeta.MetaDescription,
                p.SeoMeta.StructuredData,
                p.SeoMeta.OgImage,
                p.SeoMeta.FocusKeyword,
                p.SeoMeta.IsCanonical,
                p.SeoMeta.NoIndex,
                HasStagedContent = p.Content != null && p.Content.PreviewGjsHtml != null,
                // Sibling count within the language group, so a page can tell whether
                // every active language is covered without a second round trip.
                GroupSize = p.PageGroupId == null
                    ? 1
                    : db.PageInfos.Count(s => s.PageGroupId == p.PageGroupId && !s.IsDeleted),
                // Full name when set, username otherwise — a Guid answers nobody's
                // question about who last touched a page.
                CreatedByFullName = p.CreateUser.FullName,
                CreatedByUserName = p.CreateUser.UserName,
                UpdatedByFullName = p.UpdateUser == null ? null : p.UpdateUser.FullName,
                UpdatedByUserName = p.UpdateUser == null ? null : p.UpdateUser.UserName,
            })
            .ToListAsync(cancellationToken);

        int activeLanguageCount = await db.Languages
            .CountAsync(l => !l.IsDeleted && l.IsActive && l.IsPublished, cancellationToken);

        HashSet<int> awaitingApproval = [.. await db.ApprovalRequests
            .Where(a => a.Status == ApprovalStatus.Pending && a.ContentType == WorkflowContentType.Page)
            .Select(a => a.ContentId)
            .ToListAsync(cancellationToken)];

        DateTime staleBefore = DateTime.UtcNow.AddDays(-PageSignalRules.StaleAfterDays);

        List<PageListItemResponse> items = [.. rows.Select(r =>
        {
            PageSignal signals = PageSignal.None;

            if (string.IsNullOrWhiteSpace(r.MetaDescription)) signals |= PageSignal.MissingMetaDescription;
            if (string.IsNullOrWhiteSpace(r.StructuredData)) signals |= PageSignal.MissingStructuredData;
            if (string.IsNullOrWhiteSpace(r.OgImage)) signals |= PageSignal.MissingOgImage;
            if (string.IsNullOrWhiteSpace(r.FocusKeyword)) signals |= PageSignal.MissingFocusKeyword;
            if (!r.IsCanonical) signals |= PageSignal.NotCanonical;
            if (r.NoIndex) signals |= PageSignal.NoIndex;

            // Only meaningful once there is more than one language to be missing from.
            if (activeLanguageCount > 1 && r.GroupSize < activeLanguageCount)
                signals |= PageSignal.MissingTranslation;

            // Never scored is not the same as scored badly, so null stays unflagged.
            if (r.SeoScore is int score && score < PageSignalRules.LowSeoScoreBelow)
                signals |= PageSignal.LowSeoScore;

            if ((r.UpdateDate ?? r.CreateDate) < staleBefore)
                signals |= PageSignal.Stale;

            if (awaitingApproval.Contains(r.Id))
                signals |= PageSignal.AwaitingApproval;

            // Published, but what visitors see is not the newest version — either the
            // content is staged or the status change itself is still held back.
            if (r.HasStagedContent || r.PendingStatus is not null)
                signals |= PageSignal.HasStagedUpdate;

            return new PageListItemResponse(
                r.Id, r.Title, r.Slug, r.FullSlug, r.LanguageId, r.LanguageName,
                r.PageStatus, r.PendingStatus, r.CreateDate, r.UpdateDate,
                r.PageGroupId, r.ParentPageId, r.SeoScore, r.Kind, signals,
                DisplayName(r.CreatedByFullName, r.CreatedByUserName),
                DisplayName(r.UpdatedByFullName, r.UpdatedByUserName));
        })];

        // AND, not OR: each ticked box is another condition the page has to satisfy.
        if (request.RequiredSignals != PageSignal.None)
            items = [.. items.Where(i => (i.Signals & request.RequiredSignals) == request.RequiredSignals)];

        return Result.Success(items);
    }

    private static string? DisplayName(string? fullName, string? userName) =>
        string.IsNullOrWhiteSpace(fullName) ? userName : fullName;
}
