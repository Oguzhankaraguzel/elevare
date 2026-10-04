using System.Data.Common;
using System.Globalization;
using System.Net;
using AngleSharp;
using AngleSharp.Dom;
using Application.Abstraction.Data;
using Application.Abstraction.Services;
using Domain.Entities.PublicPages;
using Domain.Entities.PublicTags;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Concrete;
using SharedKernel.Content;

namespace Application.Services;

/// <summary>
/// Resolves the "Sayfa Listesi" (Page Listing) GrapeJS block — a "repeater":
/// the block's canvas markup contains real, user-styled template elements (a
/// search form, one tag chip, one item card, pagination links) marked with
/// <c>data-elevare-*-template</c> attributes. At request time this service
/// clones each template once per real result (patching only href/text/src),
/// inserts the clones next to the original, then removes the original template
/// node — the user's own styling/layout survives untouched.
/// <para>
/// Reads its state (page/tag/search) from the listing parameters of the query
/// string only — see <see cref="ListingQuery"/> for their names, and why nothing
/// else in the URL is carried anywhere.
/// </para>
/// </summary>
public sealed class PageListingResolutionService(IPublicReadDbContext db)
{
    private const string MarkerClass = "elevare-page-listing";
    private const int MaxItemsPerPage = 100;
    private const int SummaryLength = 200;
    private const string DefaultShareImageKey = "Seo.DefaultOgImageUrl";

    // The reserved slugs of the built-in pages (see SystemPageProvider). They are
    // not content anyone browses to from a list.
    private static readonly string[] SystemSlugs = ["home", "404", "500", "maintenance"];

    /// <summary>
    /// Fills in every page-listing block. A null value means the page had no HTML —
    /// passed through unchanged, not an error; failure means the listing query
    /// itself broke.
    /// </summary>
    public async Task<Result<PageListingResolution>> ResolveAsync(
        string? html, ListingRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            return await ResolveCoreAsync(html, request, cancellationToken);
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException or TimeoutException)
        {
            return Result.Failure<PageListingResolution>(RenderErrors.ResolutionFailed("a page listing block", ex.Message));
        }
    }

    private async Task<Result<PageListingResolution>> ResolveCoreAsync(
        string? html, ListingRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(html))
            return Result.Success(new PageListingResolution(html, null));

        IBrowsingContext context = BrowsingContext.New(Configuration.Default);
        using IDocument document = await context.OpenAsync(req => req.Content(html), cancellationToken);

        List<IElement> markers = [.. document.QuerySelectorAll('.' + MarkerClass)];
        if (markers.Count == 0)
            return Result.Success(new PageListingResolution(document.Body?.InnerHtml ?? html, null));

        var state = new ListingLinks(request.BasePath, ListingQuery.Filter(request.Query));
        var texts = ListingTexts.For(request.LanguageCode);

        // <link rel=canonical/prev/next> in the page's <head> describes ONE url, so
        // a page carrying more than one listing block speaks for the first — the one
        // that also owns the plain page/tag/q parameters.
        ListingPaginationInfo? primary = null;
        for (int i = 0; i < markers.Count; i++)
        {
            string suffix = i == 0 ? "" : ListingSuffix(markers[i], i);
            ListingPaginationInfo info = await ResolveMarkerAsync(markers[i], suffix, request, state, texts, cancellationToken);
            primary ??= info;
        }

        return Result.Success(new PageListingResolution(document.Body?.InnerHtml ?? html, primary));
    }

    // The block's own id where it has a usable one — stable across edits, so a
    // bookmarked "?page-ilist=2" keeps working — else its position.
    private static string ListingSuffix(IElement marker, int index)
    {
        string? id = marker.Id;
        return !string.IsNullOrEmpty(id) && ListingQuery.IsListingKey(ListingQuery.Key(ListingQuery.PageKey, id))
            ? id
            : (index + 1).ToString(CultureInfo.InvariantCulture);
    }

    private async Task<ListingPaginationInfo> ResolveMarkerAsync(
        IElement marker, string suffix, ListingRequest request, ListingLinks links, ListingTexts texts,
        CancellationToken cancellationToken)
    {
        string source = marker.GetAttribute("data-elevare-source") ?? "self";
        string layout = marker.GetAttribute("data-elevare-layout") ?? "list";
        int itemsPerPage = Math.Clamp(ParseInt(marker.GetAttribute("data-elevare-items-per-page"), fallback: 10), 1, MaxItemsPerPage);
        string sort = marker.GetAttribute("data-elevare-sort") ?? "newest";
        bool showSearch = marker.GetAttribute("data-elevare-show-search") != "false";
        bool showTagFilter = marker.GetAttribute("data-elevare-show-tag-filter") != "false";
        bool showPagination = marker.GetAttribute("data-elevare-show-pagination") != "false";
        string paginationStyle = marker.GetAttribute("data-elevare-pagination-style") ?? "numeric-arrows";
        string? defaultTag = NullIfBlank(marker.GetAttribute("data-elevare-default-tag"));
        string? defaultImage = NullIfBlank(marker.GetAttribute("data-elevare-default-image"));
        int? sourcePageId = source == "specific"
            ? ParseIntOrNull(marker.GetAttribute("data-elevare-source-page-id"))
            : request.CurrentPageId;

        var keys = new ListingKeys(suffix);

        // Anything but a positive whole number is page 1 — and the canonical address
        // says so, rather than repeating "?page=abc" back.
        int page = int.TryParse(links.Get(keys.Page), NumberStyles.None, CultureInfo.InvariantCulture, out int requested) && requested >= 1
            ? requested
            : 1;

        // A tag in the URL wins over the block's default — including an empty one,
        // which is what "Tümü" sends: without that, a listing with a default tag had
        // no way to show everything.
        bool tagInUrl = links.Has(keys.Tag);
        string? tagSlug = tagInUrl ? NullIfBlank(links.Get(keys.Tag)) : defaultTag;

        string? q = NullIfBlank(links.Get(keys.Search));
        if (q is { Length: > ListingQuery.MaxSearchLength })
            q = q[..ListingQuery.MaxSearchLength];

        IQueryable<PublicPage> baseQuery = await BuildBaseQueryAsync(source, sourcePageId, request.CurrentPageId, cancellationToken);

        IQueryable<PublicPage> filtered = baseQuery;

        // "Related": only pages sharing at least one tag with this one.
        if (marker.GetAttribute("data-elevare-related-tags") == "true")
        {
            List<int> ownTags = await db.PageInfoTags
                .Where(pt => pt.PageInfoId == request.CurrentPageId)
                .Select(pt => pt.TagId)
                .ToListAsync(cancellationToken);
            List<int> relatedIds = await db.PageInfoTags
                .Where(pt => ownTags.Contains(pt.TagId) && pt.PageInfoId != request.CurrentPageId)
                .Select(pt => pt.PageInfoId)
                .Distinct()
                .ToListAsync(cancellationToken);
            filtered = filtered.Where(p => relatedIds.Contains(p.Id));
        }

        if (tagSlug is not null)
        {
            List<int> taggedPageIds = await (
                from t in db.Tags
                where t.Slug == tagSlug
                join pt in db.PageInfoTags on t.Id equals pt.TagId
                select pt.PageInfoId
            ).ToListAsync(cancellationToken);
            filtered = filtered.Where(p => taggedPageIds.Contains(p.Id));
        }

        if (q is not null)
        {
            // Title and description, either case — "bit" finds "Bit Tabanlı". Plain
            // ToLower() on both sides is the one form EF translates (lower() in SQL);
            // see SqlSearchProvider for the same choice.
#pragma warning disable CA1304, CA1308, CA1311, CA1862
            string needle = SearchTerm.LowerLikeDatabase(q);
            filtered = filtered.Where(p => p.SeoTitle.ToLower().Contains(needle)
                || p.SeoMetaDescription.ToLower().Contains(needle));
#pragma warning restore CA1304, CA1308, CA1311, CA1862
        }

        // The id breaks ties: two pages published the same day would otherwise come
        // back in any order, and a page could show on both page 1 and page 2.
        filtered = sort switch
        {
            "oldest" => filtered.OrderBy(p => p.PublishedAt ?? p.CreateDate).ThenBy(p => p.Id),
            "title-asc" => filtered.OrderBy(p => p.SeoTitle).ThenBy(p => p.Id),
            "title-desc" => filtered.OrderByDescending(p => p.SeoTitle).ThenByDescending(p => p.Id),
            _ => filtered.OrderByDescending(p => p.PublishedAt ?? p.CreateDate).ThenByDescending(p => p.Id),
        };

        int totalCount = await filtered.CountAsync(cancellationToken);
        List<PublicPage> items = await filtered
            .Skip((page - 1) * itemsPerPage)
            .Take(itemsPerPage)
            .ToListAsync(cancellationToken);

        var result = PagedResult<PublicPage>.Create(items, totalCount, page, itemsPerPage);

        List<PublicTag> tagCloud = [];
        if (showTagFilter)
        {
            List<int> basePageIds = await baseQuery.Select(p => p.Id).ToListAsync(cancellationToken);
            tagCloud = await (
                from pt in db.PageInfoTags
                where basePageIds.Contains(pt.PageInfoId)
                join t in db.Tags on pt.TagId equals t.Id
                select t
            ).Distinct().OrderBy(t => t.Name).ToListAsync(cancellationToken);
        }

        // Tags per listed item are only needed when the card template actually
        // contains a tags field — skip the extra query otherwise.
        Dictionary<int, List<string>> itemTagNames = [];
        if (items.Count > 0 && marker.QuerySelector("[data-elevare-field=tags]") is not null)
        {
            List<int> itemIds = [.. items.Select(p => p.Id)];
            var tagPairs = await (
                from pt in db.PageInfoTags
                where itemIds.Contains(pt.PageInfoId)
                join t in db.Tags on pt.TagId equals t.Id
                select new { pt.PageInfoId, t.Name }
            ).ToListAsync(cancellationToken);
            itemTagNames = tagPairs
                .GroupBy(x => x.PageInfoId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Name).OrderBy(n => n, StringComparer.CurrentCulture).ToList());
        }

        Dictionary<int, CardFacts> facts = await ReadCardFactsAsync(items, cancellationToken);
        string? siteImage = defaultImage is null && facts.Values.Any(f => f.NeedsImage)
            ? await db.SiteSettings.Where(s => s.Key == DefaultShareImageKey).Select(s => s.Value).FirstOrDefaultAsync(cancellationToken)
            : null;

        ResolveSearch(marker, links, keys, texts, showSearch);
        ResolveTagCloud(marker, tagCloud, tagSlug, links, keys, showTagFilter);
        ResolveItems(marker, result.Items, new CardDefaults(layout, defaultImage, NullIfBlank(siteImage), request.LanguageCode), itemTagNames, facts);
        ResolvePagination(marker, result, links, keys, texts, paginationStyle, showPagination);

        return new ListingPaginationInfo(
            result.Page,
            result.TotalPages,
            Tag: tagInUrl ? tagSlug : null,
            IsSearch: q is not null,
            ItemPaths: [.. result.Items.Select(BuildPageUrl)],
            FirstPosition: (result.Page - 1) * itemsPerPage + 1);
    }

    private async Task<IQueryable<PublicPage>> BuildBaseQueryAsync(
        string source, int? sourcePageId, int currentPageId, CancellationToken cancellationToken)
    {
        IQueryable<PublicPage> query = db.PageInfos
            .AsNoTracking()
            .Where(p => p.PageStatus == PublicPageStatus.Published && p.IsActive);

        if (source is "self" or "specific")
            return query.Where(p => p.ParentPageId == sourcePageId);

        var current = await db.PageInfos
            .Where(p => p.Id == currentPageId)
            .Select(p => new { p.LanguageId, p.ParentPageId })
            .FirstOrDefaultAsync(cancellationToken);
        int languageId = current?.LanguageId ?? 0;

        // "The pages next to this one" — an article's fellow articles, for a
        // "more to read" list at its end. Never the page itself.
        if (source == "siblings")
        {
            int? parentId = current?.ParentPageId;
            return query.Where(p => p.ParentPageId == parentId
                && p.LanguageId == languageId
                && p.Id != currentPageId
                && !(p.ParentPageId == null && SystemSlugs.Contains(p.Slug)));
        }

        // "Every published page" means every page a reader of THIS page could want:
        // in its language (a Turkish list of English posts reads as broken), not the
        // home page, the error pages or the maintenance page, and not the listing
        // page itself.
        return query.Where(p => p.LanguageId == languageId
            && p.Id != currentPageId
            && !(p.ParentPageId == null && SystemSlugs.Contains(p.Slug)));
    }

    /// <summary>What a card falls back to when the page left its description or share image empty.</summary>
    private sealed record CardFacts(string? Image, string? Summary, bool NeedsImage);

    // Only for pages that need it — a page with its own description and share image
    // costs nothing here. The same reading the share tags use (PageContentFacts),
    // so a page's card and its share preview show the same picture.
    private async Task<Dictionary<int, CardFacts>> ReadCardFactsAsync(List<PublicPage> items, CancellationToken cancellationToken)
    {
        List<int> ids = [.. items
            .Where(p => string.IsNullOrWhiteSpace(p.OgImage) || string.IsNullOrWhiteSpace(p.SeoMetaDescription))
            .Select(p => p.Id)];
        if (ids.Count == 0)
            return [];

        var contents = await db.PageInfos
            .Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, Html = p.Content != null ? p.Content.GjsHtml : null })
            .ToListAsync(cancellationToken);

        Dictionary<int, CardFacts> facts = [];
        foreach (var content in contents)
        {
            using IDocument? document = await PageContentFacts.ParseAsync(content.Html, cancellationToken);
            string? image = PageContentFacts.FirstImage(document);
            PublicPage page = items.First(p => p.Id == content.Id);
            facts[content.Id] = new CardFacts(
                image,
                PageContentFacts.OpeningText(document, SummaryLength),
                NeedsImage: string.IsNullOrWhiteSpace(page.OgImage) && image is null);
        }
        return facts;
    }

    // Only patches the input's name and value + preserves the page's other listing
    // parameters via hidden inputs — never touches the form/input/button's authored
    // styling or text, so the user's own design survives every render untouched.
    private static void ResolveSearch(IElement marker, ListingLinks links, ListingKeys keys, ListingTexts texts, bool show)
    {
        IElement? form = marker.QuerySelector(".elevare-listing-search");
        if (form is null) return;
        if (!show) { form.Remove(); return; }

        List<IElement> inputs = [.. form.QuerySelectorAll("input")];
        IElement? input = inputs.FirstOrDefault(i => i.GetAttribute("name") == ListingQuery.SearchKey);
        if (input is not null)
        {
            input.SetAttribute("name", keys.Search);
            input.SetAttribute("value", links.Get(keys.Search) ?? "");
            // The block used to call this "search the site" — it only filters this list.
            string? label = input.GetAttribute("aria-label");
            if (string.IsNullOrWhiteSpace(label) || label is "Sitede ara" or "Search the site")
                input.SetAttribute("aria-label", texts.SearchList);
        }

        foreach ((string key, string? value) in links.Current)
        {
            // A new search starts on page 1 of its own results.
            if (key == keys.Search || key == keys.Page) continue;
            if (inputs.Any(i => i.GetAttribute("name") == key)) continue;

            IElement hidden = marker.Owner!.CreateElement("input");
            hidden.SetAttribute("type", "hidden");
            hidden.SetAttribute("name", key);
            hidden.SetAttribute("value", value ?? "");
            form.AppendChild(hidden);
        }
    }

    // The "all/clear filter" chip and the generic per-tag chip are two real,
    // independently-styled templates the user designed. GrapesJS's exported HTML
    // carries almost no literal `style="..."` attributes — authored styles are
    // compiled into id-keyed CSS rules in the page's <style> block instead (which
    // this service never sees; only the html body is resolved here) — so an
    // active/inactive "look" cannot be swapped by copying a style attribute.
    // Instead, whichever role an element should visually match is achieved by
    // giving it that role's own id: browsers apply an id-selector CSS rule to
    // every element carrying that id, not just the first, so reassigning ids is
    // a safe way to "borrow" a look the user already designed for the other role.
    private static void ResolveTagCloud(
        IElement marker, List<PublicTag> tags, string? activeSlug, ListingLinks links, ListingKeys keys, bool show)
    {
        IElement? container = marker.QuerySelector(".elevare-listing-tagcloud");
        if (container is null) return;
        if (!show || tags.Count == 0) { container.Remove(); return; }

        IElement? allChip = container.QuerySelector("[data-elevare-tag-all]");
        IElement? chipTemplate = container.QuerySelector("[data-elevare-tag-chip-template]");
        if (chipTemplate is null) return;

        string? activeLookId = allChip?.Id;
        string? inactiveLookId = chipTemplate.Id;
        bool noFilterActive = activeSlug is null;

        if (allChip is not null)
        {
            // An explicit empty tag, so a block's default tag does not come back.
            allChip.SetAttribute("href", links.Href((keys.Tag, ""), (keys.Page, null)));
            if (noFilterActive) allChip.SetAttribute("aria-current", "true");
            else if (inactiveLookId is not null) allChip.Id = inactiveLookId;
        }

        foreach (PublicTag tag in tags)
        {
            var clone = (IElement)chipTemplate.Clone(true);
            clone.SetAttribute("href", links.Href((keys.Tag, tag.Slug), (keys.Page, null)));
            if (tag.Slug == activeSlug)
            {
                clone.SetAttribute("aria-current", "true");
                if (activeLookId is not null) clone.Id = activeLookId;
            }
            clone.TextContent = tag.Name;
            chipTemplate.Before(clone);
        }
        chipTemplate.Remove();
    }

    private sealed record CardDefaults(string Layout, string? BlockImage, string? SiteImage, string LanguageCode);

    // Clones the user's single item-card template once per page, patching the
    // href and every descendant marked data-elevare-field="title|summary|image|
    // date|tags". Falls back to a plain string-built card (never visibly empty)
    // if the user deleted the template entirely. A node marked
    // data-elevare-empty-template is the user's own "no results" message — kept
    // only when the result set is empty, removed otherwise.
    //
    // Nothing a page has no value for is left showing the template's sample text
    // or its placeholder picture: the field is removed instead.
    private static void ResolveItems(
        IElement marker, IReadOnlyList<PublicPage> items, CardDefaults defaults,
        Dictionary<int, List<string>> itemTagNames, Dictionary<int, CardFacts> facts)
    {
        IElement? container = marker.QuerySelector(".elevare-listing-items");
        if (container is null) return;

        // Canvas-only preview clones ("ghosts") the editor shows to visualize the
        // per-page item count — never meant for visitors.
        foreach (IElement ghost in container.QuerySelectorAll("[data-elevare-ghost]").ToList())
            ghost.Remove();

        IElement? emptyTemplate = container.QuerySelector("[data-elevare-empty-template]");
        if (emptyTemplate is not null && items.Count > 0)
            emptyTemplate.Remove();

        IElement? template = container.QuerySelector("[data-elevare-item-template]");
        if (template is null)
        {
            if (items.Count > 0)
                container.InnerHtml = string.Concat(items.Select(p => RenderItemFallback(p, Card(p, defaults, facts, itemTagNames), defaults.Layout)));
            return;
        }

        foreach (PublicPage p in items)
        {
            CardValues card = Card(p, defaults, facts, itemTagNames);
            var clone = (IElement)template.Clone(true);
            clone.RemoveAttribute("data-elevare-item-template");
            clone.SetAttribute("href", BuildPageUrl(p));

            foreach (IElement field in clone.QuerySelectorAll("[data-elevare-field]").ToList())
            {
                switch (field.GetAttribute("data-elevare-field"))
                {
                    case "title":
                        field.TextContent = card.Title;
                        break;
                    case "summary":
                        if (card.Summary is null) field.Remove();
                        else field.TextContent = card.Summary;
                        break;
                    case "image":
                        if (card.Image is not null)
                            field.SetAttribute("src", card.Image);
                        else if (PageContentFacts.IsPlaceholder(field.GetAttribute("src")))
                        {
                            // The editor's "Görsel" placeholder is not a picture of anything.
                            field.Remove();
                            break;
                        }
                        field.SetAttribute("alt", card.Title);
                        break;
                    case "date":
                        field.TextContent = card.DateText;
                        if (field.LocalName == "time")
                            field.SetAttribute("datetime", card.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                        break;
                    case "tags":
                        if (card.Tags is null) field.Remove();
                        else field.TextContent = card.Tags;
                        break;
                }
            }

            template.Before(clone);
        }
        template.Remove();
    }

    private sealed record CardValues(string Title, string? Summary, string? Image, DateTime Date, string DateText, string? Tags);

    private static CardValues Card(
        PublicPage p, CardDefaults defaults, Dictionary<int, CardFacts> facts, Dictionary<int, List<string>> itemTagNames)
    {
        facts.TryGetValue(p.Id, out CardFacts? fromContent);
        DateTime date = p.PublishedAt ?? p.CreateDate;
        return new CardValues(
            Title: !string.IsNullOrWhiteSpace(p.SeoTitle) ? p.SeoTitle : p.Slug,
            Summary: NullIfBlank(p.SeoMetaDescription) ?? fromContent?.Summary,
            Image: NullIfBlank(p.OgImage) ?? fromContent?.Image ?? defaults.BlockImage ?? defaults.SiteImage,
            Date: date,
            DateText: ListingTexts.FormatDate(date, defaults.LanguageCode),
            Tags: itemTagNames.TryGetValue(p.Id, out List<string>? names) && names.Count > 0 ? string.Join(", ", names) : null);
    }

    // Every published page's actual public URL is "/" + FullSlug — the CMS embeds
    // the language-code prefix into FullSlug itself for non-default languages (see
    // PageInfo.ComputeFullSlug). The one known exception is the reserved "home"
    // slug (served at "/" instead), which listings are not expected to include.
#pragma warning disable S1075
    private static string BuildPageUrl(PublicPage p) => "/" + p.FullSlug;
#pragma warning restore S1075

    // Only used when the user deleted the item template entirely — a minimal
    // safety net so the block never renders visibly empty.
    private static string RenderItemFallback(PublicPage p, CardValues card, string layout)
    {
        string title = WebUtility.HtmlEncode(card.Title);
        string url = BuildPageUrl(p);
        string summary = WebUtility.HtmlEncode(card.Summary ?? "");

        string imageHtml = card.Image is not null
            ? $"<img src=\"{WebUtility.HtmlEncode(card.Image)}\" alt=\"{title}\" loading=\"lazy\" style=\"width:100%;height:160px;object-fit:cover;border-radius:8px 8px 0 0;\">"
            : "";

        const string cardStyle = "display:block;break-inside:avoid;margin-bottom:20px;border:1px solid #e5e7eb;border-radius:8px;text-decoration:none;color:#1f2937;overflow:hidden;background:#fff;";
        const string listStyle = "display:block;padding:16px;border:1px solid #e5e7eb;border-radius:8px;text-decoration:none;color:#1f2937;background:#fff;";
        const string titleStyle = "margin:0 0 6px;font-size:1.05rem;font-weight:600;";
        const string summaryStyle = "margin:0;font-size:0.9rem;color:#6b7280;";

        return layout is "grid" or "masonry"
            ? $"<a href=\"{url}\" style=\"{cardStyle}\">{imageHtml}<div style=\"padding:16px;\"><h3 style=\"{titleStyle}\">{title}</h3><p style=\"{summaryStyle}\">{summary}</p></div></a>"
            : $"<a href=\"{url}\" style=\"{listStyle}\"><h3 style=\"{titleStyle}\">{title}</h3><p style=\"{summaryStyle}\">{summary}</p></a>";
    }

    // Which page-link templates get used (prev/next only for the "arrows" styles)
    // is decided here; the VISUAL style of every clone always comes from the
    // user's own templates. The number template is designed as the CURRENT page
    // (the block ships it highlighted), so the other numbers borrow the arrow
    // link's look — the same id-borrowing as the tag chips above; without it every
    // number rendered highlighted.
    private static void ResolvePagination(
        IElement marker, PagedResult<PublicPage> result, ListingLinks links, ListingKeys keys, ListingTexts texts,
        string style, bool showPagination)
    {
        IElement? container = marker.QuerySelector(".elevare-listing-pagination");
        if (container is null) return;
        if (!showPagination || result.TotalPages <= 1) { container.Remove(); return; }

        IElement? prevTemplate = container.QuerySelector("[data-elevare-prev-template]");
        IElement? nextTemplate = container.QuerySelector("[data-elevare-next-template]");
        IElement? pageLinkTemplate = container.QuerySelector("[data-elevare-page-link-template]");
        bool showArrows = style is "numeric-arrows" or "truncated-arrows" or "prev-next";
        string? plainLookId = Anchor(prevTemplate ?? nextTemplate)?.Id;

        if (showArrows && prevTemplate is not null && result.HasPreviousPage)
        {
            var clone = (IElement)prevTemplate.Clone(true);
            clone.RemoveAttribute("data-elevare-prev-template");
            IElement anchor = PatchAnchor(clone, links.Href((keys.Page, PageValue(result.Page - 1))), text: null);
            anchor.SetAttribute("rel", "prev");
            anchor.SetAttribute("aria-label", texts.PreviousPage);
            prevTemplate.Before(clone);
        }

        if (pageLinkTemplate is not null)
        {
            List<(int? Page, bool IsEllipsis)> sequence = BuildPageNumberSequence(result.TotalPages, result.Page, style);
            foreach ((int? p, bool isEllipsis) in sequence)
            {
                if (isEllipsis)
                {
                    IElement ellipsis = marker.Owner!.CreateElement("li");
                    ellipsis.SetAttribute("aria-hidden", "true");
                    ellipsis.SetAttribute("style", "display:inline-block;padding:8px 4px;");
                    ellipsis.TextContent = "…";
                    pageLinkTemplate.Before(ellipsis);
                    continue;
                }

                var clone = (IElement)pageLinkTemplate.Clone(true);
                clone.RemoveAttribute("data-elevare-page-link-template");
                string pageText = p!.Value.ToString(CultureInfo.InvariantCulture);
                IElement anchor = PatchAnchor(clone, links.Href((keys.Page, PageValue(p.Value))), pageText);
                if (p.Value == result.Page)
                {
                    clone.ClassList.Add("elevare-page-link-active");
                    anchor.SetAttribute("aria-current", "page");
                }
                else if (plainLookId is not null && anchor.Id is not null)
                {
                    anchor.Id = plainLookId;
                }
                pageLinkTemplate.Before(clone);
            }
        }

        if (showArrows && nextTemplate is not null && result.HasNextPage)
        {
            var clone = (IElement)nextTemplate.Clone(true);
            clone.RemoveAttribute("data-elevare-next-template");
            IElement anchor = PatchAnchor(clone, links.Href((keys.Page, PageValue(result.Page + 1))), text: null);
            anchor.SetAttribute("rel", "next");
            anchor.SetAttribute("aria-label", texts.NextPage);
            nextTemplate.Before(clone);
        }

        prevTemplate?.Remove();
        pageLinkTemplate?.Remove();
        nextTemplate?.Remove();

        // A list of page links is navigation; announce it as such.
        if (container.ParentElement is { LocalName: not "nav" } parent)
        {
            IElement nav = marker.Owner!.CreateElement("nav");
            nav.SetAttribute("aria-label", texts.Pagination);
            parent.ReplaceChild(nav, container);
            nav.AppendChild(container);
        }
    }

    // Page 1 is the listing's own address, never "?page=1".
    private static string? PageValue(int page) =>
        page <= 1 ? null : page.ToString(CultureInfo.InvariantCulture);

    private static IElement? Anchor(IElement? root)
    {
        if (root is null) return null;
        return root.LocalName == "a" ? root : root.QuerySelector("a");
    }

    // Templates may mark the whole repeatable unit (a <li>) rather than the
    // anchor itself, so the href/text patch targets the first descendant anchor
    // (or the element itself if it already is one).
    private static IElement PatchAnchor(IElement templateRoot, string href, string? text)
    {
        IElement anchor = Anchor(templateRoot) ?? templateRoot;
        anchor.SetAttribute("href", href);
        if (text is not null) anchor.TextContent = text;
        return anchor;
    }

    /// <summary>
    /// Pure, testable: decides WHICH page numbers to render, independent of styling.
    /// "numeric"/"numeric-arrows" show every page; "truncated"/"truncated-arrows"
    /// show a windowed first/last/current±1 sequence with a literal "…" for
    /// skipped gaps; "prev-next" shows no numbers at all (arrows only).
    /// </summary>
    internal static List<(int? Page, bool IsEllipsis)> BuildPageNumberSequence(int totalPages, int currentPage, string style)
    {
        if (style is "numeric" or "numeric-arrows")
            return [.. Enumerable.Range(1, totalPages).Select(i => ((int?)i, false))];

        if (style is "prev-next")
            return [];

        SortedSet<int> pages = [1, totalPages, currentPage];
        if (currentPage > 1) pages.Add(currentPage - 1);
        if (currentPage < totalPages) pages.Add(currentPage + 1);

        List<int> ordered = [.. pages.Where(p => p >= 1 && p <= totalPages).OrderBy(p => p)];
        List<(int? Page, bool IsEllipsis)> result = [];
        for (int i = 0; i < ordered.Count; i++)
        {
            if (i > 0 && ordered[i] - ordered[i - 1] > 1)
                result.Add((null, true));
            result.Add((ordered[i], false));
        }
        return result;
    }

    private static string? NullIfBlank(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static int ParseInt(string? text, int fallback)
        => int.TryParse(text, out int value) && value > 0 ? value : fallback;

    private static int? ParseIntOrNull(string? text)
        => int.TryParse(text, out int value) ? value : null;

    /// <summary>The three parameter names one listing reads.</summary>
    private sealed record ListingKeys(string Suffix)
    {
        public string Page => ListingQuery.Key(ListingQuery.PageKey, Suffix);
        public string Tag => ListingQuery.Key(ListingQuery.TagKey, Suffix);
        public string Search => ListingQuery.Key(ListingQuery.SearchKey, Suffix);
    }

    /// <summary>
    /// Builds a listing's links from the page's own address and its current listing
    /// parameters — every other listing on the page keeps its state, nothing else
    /// in the URL comes along.
    /// </summary>
    private sealed class ListingLinks(string basePath, Dictionary<string, string?> current)
    {
        public IReadOnlyDictionary<string, string?> Current => current;

        public bool Has(string key) => current.ContainsKey(key);

        public string? Get(string key) => current.TryGetValue(key, out string? value) ? value : null;

        public string Href(params (string Key, string? Value)[] overrides)
        {
            Dictionary<string, string?> merged = new(current, StringComparer.Ordinal);
            foreach ((string key, string? value) in overrides)
            {
                if (value is null) merged.Remove(key);
                else merged[key] = value;
            }
            // Always the same order (tag, q, page), so a link and the canonical the
            // view writes for the same state are the same string.
            List<string> parts = [.. merged
                .OrderBy(kv => KeyRank(kv.Key)).ThenBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value ?? "")}")];
            string path = string.IsNullOrEmpty(basePath) ? "/" : basePath;
            return parts.Count == 0 ? path : path + "?" + string.Join("&", parts);
        }

        private static int KeyRank(string key)
        {
            if (key.StartsWith(ListingQuery.TagKey, StringComparison.Ordinal)) return 0;
            if (key.StartsWith(ListingQuery.SearchKey, StringComparison.Ordinal)) return 1;
            return 2;
        }
    }
}
