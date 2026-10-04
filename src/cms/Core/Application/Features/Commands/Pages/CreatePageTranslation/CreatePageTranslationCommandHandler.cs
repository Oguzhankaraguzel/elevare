using Application.Abstraction.Data;
using Application.Features.Commands.StructuredData;
using Application.Features.Commands.Trash;
using Domain.Entities.Languages;
using Domain.Entities.PageContents;
using Domain.Entities.PageInfos;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;
using SharedKernel.Extensions.Strings;

namespace Application.Features.Commands.Pages.CreatePageTranslation;

internal sealed class CreatePageTranslationCommandHandler(ICmsApplicationDbContext db)
    : ICommandHandler<CreatePageTranslationCommand, int>
{
    public async Task<Result<int>> Handle(CreatePageTranslationCommand request, CancellationToken cancellationToken)
    {
        PageInfo? source = await db.PageInfos
            .Include(p => p.Content)
            .Include(p => p.PageGroup!).ThenInclude(g => g.Pages!)
            .FirstOrDefaultAsync(p => p.Id == request.SourcePageId, cancellationToken);

        if (source is null)
            return Result.Failure<int>(PageInfoErrors.NotFound);

        if (source.LanguageId == request.TargetLanguageId)
            return Result.Failure<int>(PageInfoErrors.SameLanguage);

        Language? targetLanguage = await db.Languages
            .FirstOrDefaultAsync(l => l.Id == request.TargetLanguageId, cancellationToken);
        if (targetLanguage is null)
            return Result.Failure<int>(LanguageErrors.NotFound);

        PageGroup group;
        if (source.PageGroup is not null)
        {
            group = source.PageGroup;
            if (group.Pages!.Any(p => p.LanguageId == request.TargetLanguageId))
                return Result.Failure<int>(PageInfoErrors.TranslationAlreadyExists);
        }
        else
        {
            group = new PageGroup { Name = source.SeoMeta.Title.HasValue() ? source.SeoMeta.Title : source.Slug };
            db.PageGroups.Add(group);
            source.PageGroup = group;
        }

        // Clear the tombstone before deciding the slug: a purged row must not make
        // the collision check below invent a "-en" suffix for an address that is
        // about to be free.
        if (request.ReplaceDeleted)
        {
            List<int> tombstones = await db.PageInfos
                .IgnoreQueryFilters()
                .Where(p => p.IsDeleted
                            && p.PageGroupId == group.Id
                            && p.LanguageId == request.TargetLanguageId)
                .Select(p => p.Id)
                .ToListAsync(cancellationToken);

            foreach (int tombstoneId in tombstones)
                await TrashPurge.PurgePageAsync(db, tombstoneId, cancellationToken);
        }

        string defaultCode = await db.Languages
            .Where(l => l.IsDefault)
            .Select(l => l.TwoLetterCode)
            .FirstOrDefaultAsync(cancellationToken) ?? "tr";

        bool slugTaken = await db.PageInfos
            .AnyAsync(p => p.Slug == source.Slug && p.LanguageId == request.TargetLanguageId, cancellationToken);
        string targetSlug = slugTaken ? $"{source.Slug}-{targetLanguage.TwoLetterCode}" : source.Slug;

        // Only reuse the source's parent when that parent already has its own
        // translation in the target language — otherwise the new page starts
        // top-level rather than guessing at a cross-language parent. The actual
        // PARENT ENTITY (not just its id) is assigned to ParentPage below —
        // ComputeFullSlug() walks that navigation property, and a freshly
        // constructed PageInfo has no way to resolve it from ParentPageId alone
        // before SaveChanges runs.
        PageInfo? targetParent = null;
        if (source.ParentPageId is not null)
        {
            PageInfo? parent = await db.PageInfos
                .Include(p => p.PageGroup!).ThenInclude(g => g.Pages!)
                .FirstOrDefaultAsync(p => p.Id == source.ParentPageId, cancellationToken);

            targetParent = parent?.PageGroup?.Pages?
                .FirstOrDefault(p => p.LanguageId == request.TargetLanguageId);

            // ComputeFullSlug() below walks ParentPage.ParentPage.… to the root, and
            // a page plucked out of the PageGroup's Pages collection above never had
            // its own ParentPage loaded — load it explicitly, or a translation whose
            // parent is itself nested gets a silently truncated slug.
            if (targetParent?.ParentPageId is int targetParentParentId)
            {
                targetParent.ParentPage = await db.PageInfos
                    .FirstOrDefaultAsync(p => p.Id == targetParentParentId, cancellationToken);
            }
        }

        var translation = new PageInfo
        {
            Slug = targetSlug,
            PageStatus = PageStatus.Draft,
            LanguageId = request.TargetLanguageId,
            Language = targetLanguage,
            ParentPage = targetParent,
            ParentPageId = targetParent?.Id,
            PageGroup = group,
            Kind = source.Kind,
            // The texts carry over as a starting point the translator writes over.
            // What names the source page itself does not: its share address, its
            // canonical address and its structured data (address, language,
            // breadcrumb). Copied, the translation told crawlers and share cards it
            // was the other language's page until someone noticed.
            SeoMeta = new SeoMeta
            {
                Title = source.SeoMeta.Title,
                MetaDescription = source.SeoMeta.MetaDescription,
                MetaAuthor = source.SeoMeta.MetaAuthor,
                // Crawler directives carry over: a translation of a page kept out of
                // the index almost never wants to be the one copy that IS indexed.
                NoIndex = source.SeoMeta.NoIndex,
                NoFollow = source.SeoMeta.NoFollow,
                FocusKeyword = source.SeoMeta.FocusKeyword,
                OgTitle = source.SeoMeta.OgTitle,
                OgDescription = source.SeoMeta.OgDescription,
                OgType = source.SeoMeta.OgType,
                OgImage = source.SeoMeta.OgImage,
                TwitterCard = source.SeoMeta.TwitterCard,
                TwitterSite = source.SeoMeta.TwitterSite
            },
            Content = new PageContent
            {
                GjsHtml = source.Content?.GjsHtml,
                GjsCss = source.Content?.GjsCss,
                GjsData = source.Content?.GjsData
            }
        };

        translation.ComputeFullSlug(defaultCode);

        string? baseUrl = await db.SiteSettings
            .Where(s => s.Key == SchemaNodeFactory.PublicSiteBaseUrlKey)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(baseUrl))
            translation.SeoMeta.OgUrl = SchemaNodeFactory.PageUrl(baseUrl.Trim().TrimEnd('/'), translation);

        db.PageInfos.Add(translation);

        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(translation.Id);
    }
}
