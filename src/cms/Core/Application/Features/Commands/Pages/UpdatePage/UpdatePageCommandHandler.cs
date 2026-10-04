using Application.Features.Commands.Pages.Shared;
using Application.Abstraction.Data;
using Application.Abstraction.Services.Authentication;
using Application.Features.Commands.Pages;
using Application.Features.Workflows;
using Application.Security;
using Domain.Entities.PageContents;
using Domain.Entities.PageInfos;
using Domain.Entities.SiteCodeSnippets;
using Domain.Entities.Tags;
using Domain.Entities.Workflows;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;
using SharedKernel.Extensions.Strings;
using SharedKernel.Social;

namespace Application.Features.Commands.Pages.UpdatePage;

internal sealed class UpdatePageCommandHandler(ICmsApplicationDbContext db, IUserContext userContext)
    : ICommandHandler<UpdatePageCommand>
{
    public async Task<Result> Handle(UpdatePageCommand request, CancellationToken cancellationToken)
    {
        if (CustomCodeGuard.ContainsCustomCode(request.GjsHtml) && !userContext.CanAuthorCustomCode)
            return Result.Failure(PageInfoErrors.CustomCodeNotAllowed);

        // ComputeFullSlug() below walks ParentPage.ParentPage.… all the way to the
        // root, so a single-level Include leaves it silently truncating the slug of
        // any page nested two levels deep (a plain re-save, no parent change,
        // still calls ComputeFullSlug unconditionally). PageHierarchy.MaxDepth is 3,
        // so one ThenInclude reaches the root from any legal depth.
        PageInfo? page = await db.PageInfos
            .Include(p => p.Language)
            .Include(p => p.ParentPage).ThenInclude(p => p!.ParentPage)
            .Include(p => p.Content)
            .Include(p => p.Tags)
            .Include(p => p.ExcludedSiteCodeSnippets)
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (page is null)
            return Result.Failure(PageInfoErrors.NotFound);

        if (request.ExpectedFingerprint is not null && !request.Overwrite
            && request.ExpectedFingerprint != PageFingerprint.Of(page))
            return Result.Failure(PageInfoErrors.EditedElsewhere);

        string slug = request.Slug.ToSlug();
        if (slug.IsNullOrWhiteSpace())
            return Result.Failure(PageInfoErrors.InvalidSlug);

        bool slugOrParentChanged = slug != page.Slug || request.ParentPageId != page.ParentPageId;
        bool wasPublished = page.PageStatus == PageStatus.Published;
        string oldFullSlug = page.FullSlug;

        if (slug != page.Slug)
        {
            bool slugExists = await db.PageInfos
                .AnyAsync(p => p.Id != page.Id && p.Slug == slug && p.LanguageId == page.LanguageId, cancellationToken);
            if (slugExists)
                return Result.Failure(PageInfoErrors.SlugAlreadyExists);

            page.Slug = slug;
        }

        if (request.ParentPageId != page.ParentPageId)
        {
            if (request.ParentPageId is int newParentId)
            {
                if (await PageHierarchy.IsSelfOrDescendantAsync(db, page.Id, newParentId, cancellationToken))
                    return Result.Failure(PageInfoErrors.CircularParentReference);

                // Same reason as the Include above: ComputeFullSlug needs the new
                // parent's own ancestor chain, not just the parent itself.
                PageInfo? newParent = await db.PageInfos
                    .Include(p => p.ParentPage)
                    .FirstOrDefaultAsync(p => p.Id == newParentId, cancellationToken);
                if (newParent is null)
                    return Result.Failure(PageInfoErrors.ParentNotFound);
                if (newParent.LanguageId != page.LanguageId)
                    return Result.Failure(PageInfoErrors.ParentLanguageMismatch);

                int parentLevel = await PageHierarchy.GetLevelAsync(db, newParent.Id, cancellationToken);
                int subtreeExtraLevels = await PageHierarchy.GetSubtreeExtraLevelsAsync(db, page.Id, cancellationToken);
                if (parentLevel + 1 + subtreeExtraLevels > PageHierarchy.MaxDepth)
                    return Result.Failure(PageInfoErrors.MaxDepthExceeded);

                page.ParentPage = newParent;
                page.ParentPageId = newParent.Id;
            }
            else
            {
                page.ParentPage = null;
                page.ParentPageId = null;
            }
        }

        // Mutate the owned SeoMeta in place rather than replacing it, so EF only
        // updates the changed columns instead of cascade-deleting the owned entity.
        page.Kind = request.Kind;
        page.SeoMeta.Title = request.Title;
        page.SeoMeta.IsCanonical = request.Seo.IsCanonical;
        page.SeoMeta.CanonicalUrl = request.Seo.CanonicalUrl;
        page.SeoMeta.MetaDescription = request.Seo.MetaDescription;
        page.SeoMeta.MetaAuthor = request.Seo.MetaAuthor;
        page.SeoMeta.NoIndex = request.Seo.NoIndex;
        page.SeoMeta.NoFollow = request.Seo.NoFollow;
        page.SeoMeta.FocusKeyword = request.Seo.FocusKeyword;
        page.SeoMeta.StructuredData = request.Seo.StructuredData;
        page.SeoMeta.OgTitle = request.Seo.OgTitle;
        page.SeoMeta.OgDescription = request.Seo.OgDescription;
        page.SeoMeta.OgType = request.Seo.OgType;
        page.SeoMeta.OgImage = request.Seo.OgImage;
        page.SeoMeta.OgUrl = request.Seo.OgUrl;
        page.SeoMeta.TwitterCard = request.Seo.TwitterCard;
        page.SeoMeta.TwitterSite = request.Seo.TwitterSite;
        // Null means the caller did not send it (an older client); an instance with
        // nothing set stores null, so a page that never used any of this stays clean.
        if (request.Seo.Social is not null)
            page.SeoMeta.SocialJson = request.Seo.Social.ToJson();
        // Only overwrite when the client actually reported a fresh score (the
        // analyzer runs client-side and may not have finished before Save is
        // clicked) — never stomp a previously persisted score with null.
        if (request.Seo.SeoScore.HasValue)
        {
            page.SeoMeta.SeoScore = request.Seo.SeoScore;
            page.SeoMeta.SeoScoreUpdatedAt = DateTime.UtcNow;
        }

        string defaultCode = await db.Languages
            .Where(l => l.IsDefault)
            .Select(l => l.TwoLetterCode)
            .FirstOrDefaultAsync(cancellationToken) ?? "tr";
        page.ComputeFullSlug(defaultCode);

        // FullSlug is denormalized per page — if this page's own slug or parent
        // changed, every descendant's stored FullSlug is now stale and must be
        // recomputed too (e.g. renaming "hekimler" must also update
        // "hekimler/sami-sokucu").
        // Every page that moved, this one and those below it: links to them, here and
        // everywhere else, are pointed at the new addresses once the save is assembled.
        Dictionary<string, string> moves = new(StringComparer.Ordinal);
        if (page.FullSlug != oldFullSlug)
            moves[oldFullSlug] = page.FullSlug;
        if (slugOrParentChanged)
            await PageHierarchy.RecomputeDescendantSlugsAsync(db, page, defaultCode, cancellationToken, moves);

        // Best-effort: a redirect couldn't be created (e.g. a manual rule already
        // claims this path) shouldn't block the page rename itself from saving.
        if (wasPublished && page.FullSlug != oldFullSlug)
            await RedirectResolution.UpsertForSlugChangeAsync(db, page.Id, oldFullSlug, page.FullSlug, cancellationToken);

        if (request.TagIds is not null)
        {
            List<Tag> selectedTags = await db.Tags
                .Where(t => request.TagIds.Contains(t.Id))
                .ToListAsync(cancellationToken);
            page.Tags = selectedTags;
        }

        // Same shape as the tags above. Null means "not sent" (a caller that does
        // not know about exclusions leaves them alone); an empty list clears them.
        if (request.ExcludedSiteCodeSnippetIds is not null)
        {
            List<SiteCodeSnippet> excluded = await db.SiteCodeSnippets
                .Where(s => request.ExcludedSiteCodeSnippetIds.Contains(s.Id))
                .ToListAsync(cancellationToken);
            page.ExcludedSiteCodeSnippets = excluded;
        }

        page.Content ??= new PageContent { PageInfoId = page.Id };
        // The editor's round-trip project JSON always updates live regardless of any
        // active workflow — it's never served to the public site, only reloaded to
        // restore the canvas, so gating it would just hide the writer's own work
        // from themselves without protecting anything.
        page.Content.GjsData = request.GjsData;

        bool staged = await ApprovalWorkflowResolution.StageIfWorkflowActiveAsync(
            db, WorkflowContentType.Page, page.Id, cancellationToken);

        // Content isn't the only thing a pending review must gate — the status
        // badge next to it would otherwise say "Published" the instant this save
        // is submitted, even though the HTML actually being served hasn't changed
        // (or, for a brand-new page, doesn't exist yet). Hold the requested status
        // in PendingStatus alongside the staged HTML; DecideApprovalCommandHandler
        // applies both together once the chain clears.
        bool goingLiveUnreviewed = staged
            && request.Status == PageStatus.Published
            && page.PageStatus != PageStatus.Published;

        if (goingLiveUnreviewed)
        {
            page.PendingStatus = request.Status;
        }
        else
        {
            page.PageStatus = request.Status;
            page.PendingStatus = null;
        }

        if (staged)
        {
            // Rendered output is gated behind approval — stage it instead of
            // publishing, leaving the live GjsHtml/GjsCss untouched. The existing
            // signed-preview-link flow already reads these columns, so reviewers see
            // exactly what's pending with zero Web-side changes.
            page.Content.PreviewGjsHtml = request.GjsHtml;
            page.Content.PreviewGjsCss = request.GjsCss;
        }
        else
        {
            page.Content.GjsHtml = request.GjsHtml;
            page.Content.GjsCss = request.GjsCss;
            // A genuine publish supersedes any staged preview snapshot — clear it so
            // a stale unsaved-edit preview never lingers past a real publish.
            page.Content.PreviewGjsHtml = null;
            page.Content.PreviewGjsCss = null;
        }

        PagePublication.Stamp(page, DateTime.UtcNow);

        // After the content above is assigned, so this page's own og:url, structured
        // data and links are moved along with everyone else's.
        if (moves.Count > 0)
            await SiteAddressMigration.ApplyAsync(db, moves, cancellationToken);

        return Result.Success();
    }
}
