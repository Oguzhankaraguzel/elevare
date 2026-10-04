using Application.Abstraction.Data;
using Application.Features.Commands.Pages;
using Domain.Entities.Languages;
using Domain.Entities.PageContents;
using Domain.Entities.PageInfos;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;
using SharedKernel.Extensions.Strings;

namespace Application.Features.Commands.Pages.CreatePage;

internal sealed class CreatePageCommandHandler(ICmsApplicationDbContext db)
    : ICommandHandler<CreatePageCommand, int>
{
    public async Task<Result<int>> Handle(CreatePageCommand request, CancellationToken cancellationToken)
    {
        string slug = (request.Slug.HasValue() ? request.Slug! : request.Title).ToSlug();
        if (slug.IsNullOrWhiteSpace())
            return Result.Failure<int>(PageInfoErrors.InvalidSlug);

        Language? language = await db.Languages
            .FirstOrDefaultAsync(l => l.Id == request.LanguageId, cancellationToken);
        if (language is null)
            return Result.Failure<int>(LanguageErrors.NotFound);

        bool slugExists = await db.PageInfos
            .AnyAsync(p => p.Slug == slug && p.LanguageId == request.LanguageId, cancellationToken);
        if (slugExists)
            return Result.Failure<int>(PageInfoErrors.SlugAlreadyExists);

        PageInfo? parent = null;
        if (request.ParentPageId is int parentId)
        {
            // ComputeFullSlug() below walks ParentPage.ParentPage.… to the root, so
            // the parent's own ancestor must be loaded too, or a page created two
            // levels deep gets a silently truncated slug.
            parent = await db.PageInfos
                .Include(p => p.ParentPage)
                .FirstOrDefaultAsync(p => p.Id == parentId, cancellationToken);
            if (parent is null)
                return Result.Failure<int>(PageInfoErrors.ParentNotFound);
            if (parent.LanguageId != request.LanguageId)
                return Result.Failure<int>(PageInfoErrors.ParentLanguageMismatch);

            int parentLevel = await PageHierarchy.GetLevelAsync(db, parent.Id, cancellationToken);
            if (parentLevel + 1 > PageHierarchy.MaxDepth)
                return Result.Failure<int>(PageInfoErrors.MaxDepthExceeded);
        }

        string defaultCode = await db.Languages
            .Where(l => l.IsDefault)
            .Select(l => l.TwoLetterCode)
            .FirstOrDefaultAsync(cancellationToken) ?? "tr";

        var page = new PageInfo
        {
            Slug = slug,
            PageStatus = PageStatus.Draft,
            Kind = request.Kind,
            LanguageId = request.LanguageId,
            Language = language,
            ParentPage = parent,
            ParentPageId = parent?.Id,
            SeoMeta = new SeoMeta { Title = request.Title },
            Content = new PageContent()
        };

        page.ComputeFullSlug(defaultCode);

        db.PageInfos.Add(page);

        // Save explicitly so the generated identity is available for the response;
        // the SaveChanges pipeline behavior then runs as a harmless no-op.
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(page.Id);
    }
}
