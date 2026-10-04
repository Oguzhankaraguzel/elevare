using System.Text.Json.Nodes;
using Application.Abstraction.Data;
using Domain.Entities.Languages;
using Domain.Entities.PageInfos;
using Domain.Entities.StructuredData;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Commands.StructuredData.BuildDraft;

/// <summary>
/// Assembles the starting graph for a page: who runs the site, the site itself,
/// this page, where it sits in the tree, and any FAQ already written into it —
/// each as its own node, cross-referenced by <c>@id</c>.
/// <para>
/// Those ids are real addresses. The organisation is identified by the site's URL
/// and the page by its own, rather than by minted fragments like
/// <c>…/#organization</c> that resolve to nothing. Only the FAQ keeps a fragment,
/// because it genuinely is a distinct thing living on the page.
/// </para>
/// <para>
/// The draft is a snapshot, not a live binding — the organisation's details are
/// copied in, so changing them in Site Settings later does not reach pages already
/// built. That is the price of "the JSON you see is the JSON that ships"; the bulk
/// refresh exists to pay it.
/// </para>
/// </summary>
internal sealed class BuildStructuredDataDraftCommandHandler(ICmsApplicationDbContext db)
    : ICommandHandler<BuildStructuredDataDraftCommand, string>
{
    public async Task<Result<string>> Handle(
        BuildStructuredDataDraftCommand request,
        CancellationToken cancellationToken)
    {
        Dictionary<string, string?> settings = await db.SiteSettings
            .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);

        string? baseUrl = SchemaNodeFactory
            .Value(settings, SchemaNodeFactory.PublicSiteBaseUrlKey)?.TrimEnd('/');

        if (string.IsNullOrWhiteSpace(baseUrl))
            return Result.Failure<string>(StructuredDataErrors.PublicSiteBaseUrlMissing);

        PageInfo? page = await db.PageInfos
            .Include(p => p.Content)
            .Include(p => p.Language)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.PageId, cancellationToken);

        if (page is null)
            return Result.Failure<string>(StructuredDataErrors.PageNotFound(request.PageId));

        // The default language counts even when it is not flagged published: it is
        // the language the site is actually served in, and a contact point that
        // omits it would claim visitors cannot be helped in it.
        List<Language> languages = await db.Languages
            .Where(l => l.IsPublished || l.IsDefault)
            .OrderBy(l => l.DisplayOrder)
            .ToListAsync(cancellationToken);

        ArticleContext context = await BuildContextAsync(page, settings, baseUrl, cancellationToken);

        var graph = SchemaGraph.CreateEmpty();

        graph.Add(SchemaNodeFactory.BuildOrganization(settings, baseUrl, languages, context.Media));
        graph.Add(await SchemaNodeFactory.BuildWebSiteAsync(db, settings, baseUrl, languages, cancellationToken));
        graph.Add(SchemaNodeFactory.BuildWebPage(page, baseUrl, context));

        JsonObject? breadcrumb = await SchemaNodeFactory
            .BuildBreadcrumbAsync(db, page, baseUrl, settings, cancellationToken);
        if (breadcrumb is not null)
            graph.Add(breadcrumb);

        JsonObject? faq = SchemaNodeFactory
            .BuildFaq(page.Content?.GjsHtml, SchemaNodeFactory.PageUrl(baseUrl, page));
        if (faq is not null)
            graph.Add(faq);

        JsonObject? article = SchemaNodeFactory
            .BuildArticle(page.Content?.GjsHtml, SchemaNodeFactory.PageUrl(baseUrl, page), context);
        if (article is not null)
            graph.Add(article);

        return Result.Success(graph.Serialize());
    }

    /// <summary>
    /// Everything the page already says about itself outside its content: its
    /// description, share image, tags, language, last save — and, from the media
    /// library, the size of every image the graph may describe, in one query.
    /// </summary>
    private async Task<ArticleContext> BuildContextAsync(
        PageInfo page, Dictionary<string, string?> settings, string baseUrl, CancellationToken cancellationToken)
    {
        List<string> keys = [.. SchemaNodeFactory.ImageCandidates(page, settings)
            .SelectMany(src => new[] { src, SchemaNodeFactory.MediaKey(src) })
            .Distinct(StringComparer.Ordinal)];

        var media = (await db.MediaFiles
                .AsNoTracking()
                .Where(m => keys.Contains(m.FilePath))
                .Select(m => new { m.FilePath, m.Width, m.Height, m.MimeType, m.AltText })
                .ToListAsync(cancellationToken))
            .GroupBy(m => m.FilePath, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => new MediaInfo(g.First().Width, g.First().Height, g.First().MimeType, g.First().AltText),
                StringComparer.Ordinal);

        List<string> tags = await db.PageInfoTags
            .Where(pt => pt.PageInfoId == page.Id)
            .Join(db.Tags, pt => pt.TagId, t => t.Id, (pt, t) => t.Name)
            .OrderBy(n => n)
            .ToListAsync(cancellationToken);

        // Built while the page is being edited: modified now, and a page never
        // published yet is published now — not "when it was created".
        DateTime now = DateTime.UtcNow;
        return new ArticleContext(
            BaseUrl: baseUrl,
            MetaDescription: page.SeoMeta.MetaDescription,
            ShareImage: SchemaNodeFactory.ShareImage(page, settings),
            DateModified: now,
            Language: page.Language?.TwoLetterCode,
            Keywords: tags,
            Media: media,
            DatePublished: page.PublishedAt ?? now);
    }
}
