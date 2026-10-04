using System.Globalization;
using System.Xml.Linq;
using AngleSharp.Dom;
using Application.Abstraction.Data;
using Domain.Entities.PublicPages;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;
using SharedKernel.Content;

namespace Application.Features.Queries.Feeds.GetArticleFeed;

internal sealed class GetArticleFeedQueryHandler(IPublicReadDbContext db)
    : IQueryHandler<GetArticleFeedQuery, string>
{
    private const int MaxItems = 30;
    private const int SummaryLength = 300;
    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";

    public async Task<Result<string>> Handle(GetArticleFeedQuery request, CancellationToken cancellationToken)
    {
        var language = await db.Languages
            .Where(l => l.TwoLetterCode == request.LanguageCode && l.IsActive && l.IsPublished)
            .Select(l => new { l.Id, l.TwoLetterCode })
            .FirstOrDefaultAsync(cancellationToken);
        if (language is null)
            return Result.Failure<string>(PublicFeedErrors.LanguageNotFound(request.LanguageCode));

        List<PublicPage> pages = await db.PageInfos
            .AsNoTracking()
            .Include(p => p.Content)
            .Where(p => p.LanguageId == language.Id && p.Kind == PublicPageKind.Article
                && p.PageStatus == PublicPageStatus.Published && p.IsActive)
            .OrderByDescending(p => p.PublishedAt ?? p.CreateDate).ThenByDescending(p => p.Id)
            .Take(MaxItems)
            .ToListAsync(cancellationToken);

        List<int> ids = [.. pages.Select(p => p.Id)];
        ILookup<int, string> tags = (await (
                from pt in db.PageInfoTags
                where ids.Contains(pt.PageInfoId)
                join t in db.Tags on pt.TagId equals t.Id
                select new { pt.PageInfoId, t.Name })
            .ToListAsync(cancellationToken))
            .ToLookup(x => x.PageInfoId, x => x.Name);

        Dictionary<string, string?> settings = await db.SiteSettings
            .Where(s => s.Key == "General.SiteName" || s.Key == "General.Tagline")
            .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);
        string siteName = Setting(settings, "General.SiteName") ?? request.SiteAddress;

        string site = request.SiteAddress.TrimEnd('/');
        string home = site + request.HomePath;
        var channel = new XElement("channel",
            new XElement("title", siteName),
            new XElement("link", home),
            new XElement("description", Setting(settings, "General.Tagline") ?? siteName),
            new XElement("language", language.TwoLetterCode),
            new XElement(Atom + "link",
                new XAttribute("href", site + request.FeedPath),
                new XAttribute("rel", "self"),
                new XAttribute("type", "application/rss+xml")));
        if (pages.Count > 0)
            channel.Add(new XElement("lastBuildDate", Rfc822(pages.Max(p => p.UpdateDate ?? p.PublishedAt ?? p.CreateDate))));

        foreach (PublicPage page in pages)
        {
            string link = site + "/" + page.FullSlug;
            var item = new XElement("item",
                new XElement("title", string.IsNullOrWhiteSpace(page.SeoTitle) ? page.Slug : page.SeoTitle),
                new XElement("link", link),
                new XElement("guid", new XAttribute("isPermaLink", "true"), link),
                new XElement("pubDate", Rfc822(page.PublishedAt ?? page.CreateDate)));
            string? summary = await SummaryAsync(page, cancellationToken);
            if (summary is not null)
                item.Add(new XElement("description", summary));
            foreach (string tag in tags[page.Id].Order(StringComparer.CurrentCulture))
                item.Add(new XElement("category", tag));
            channel.Add(item);
        }

        var feed = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("rss",
                new XAttribute("version", "2.0"),
                new XAttribute(XNamespace.Xmlns + "atom", Atom.NamespaceName),
                channel));
        return Result.Success(feed.Declaration + "\n" + feed.Root);
    }

    // The page's own description; else its opening paragraph — what a reader sees
    // under the title in their feed reader.
    private static async Task<string?> SummaryAsync(PublicPage page, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(page.SeoMetaDescription))
            return page.SeoMetaDescription.Trim();
        using IDocument? content = await PageContentFacts.ParseAsync(page.Content?.GjsHtml, cancellationToken);
        return PageContentFacts.OpeningText(content, SummaryLength);
    }

    private static string? Setting(Dictionary<string, string?> settings, string key) =>
        settings.TryGetValue(key, out string? v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;

    private static string Rfc822(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("R", CultureInfo.InvariantCulture);
}
