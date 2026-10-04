using System.Globalization;
using System.Text.Json.Nodes;
using AngleSharp.Html.Parser;
using Application.Abstraction.Data;
using Domain.Entities.Languages;
using Domain.Entities.PageInfos;
using Domain.Entities.StructuredData;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Content;

namespace Application.Features.Commands.StructuredData;

/// <summary>
/// Builds the graph nodes the CMS can derive on its own, from site settings and a
/// page's own data.
/// <para>
/// The shape follows what large Turkish publishers actually ship rather than the
/// synthetic-fragment style (<c>…/#organization</c>, <c>…/#website</c>) some
/// generators use. Concretely: a node's <c>@id</c> is the real URL of the thing it
/// describes, the breadcrumb hangs off the page it belongs to instead of floating
/// as a sibling, and the publisher is written out in full where it is used. It is
/// more verbose than id-references, and that is the trade being made — a consumer
/// reading any single node gets everything it needs without resolving pointers.
/// </para>
/// <para>
/// Shared by the two commands that need these nodes for opposite reasons: the draft
/// builds a graph from nothing, and the refresh replaces them inside graphs an
/// editor has since worked on.
/// </para>
/// </summary>
internal static partial class SchemaNodeFactory
{
    public const string PublicSiteBaseUrlKey = "Advanced.PublicSiteBaseUrl";
    private const string SiteNameKey = "General.SiteName";
    private const string LogoKey = "Appearance.LogoUrl";
    private const string TaglineKey = "General.Tagline";
    private const string PhoneKey = "Contact.Phone";
    private const string EmailKey = "Contact.Email";
    private const string AddressKey = "Contact.Address";
    private const string LocalityKey = "Contact.AddressLocality";
    private const string RegionKey = "Contact.AddressRegion";
    private const string PostalCodeKey = "Contact.PostalCode";
    private const string CountryKey = "Contact.AddressCountry";

    /// <summary>Reserved slug of the site's homepage, matching the public site's routing.</summary>
    public const string HomeSlug = "home";

    /// <summary>Mirrors PageHierarchy's cap: bad data must not spin here forever.</summary>
    private const int PageHierarchyDepthGuard = 10;

    private static readonly string[] SocialKeys =
    [
        "Social.FacebookUrl", "Social.InstagramUrl", "Social.XUrl",
        "Social.LinkedInUrl", "Social.YoutubeUrl",
    ];

    public static string PageUrl(string baseUrl, PageInfo page) => $"{baseUrl}/{page.FullSlug}";

    /// <summary>True for the site's front page, which is described as the organisation itself.</summary>
    public static bool IsHomePage(PageInfo page) =>
        page.ParentPageId is null
        && string.Equals(page.Slug, HomeSlug, StringComparison.OrdinalIgnoreCase);

    // ── Organization ──────────────────────────────────────────────────────────

    /// <summary>
    /// Who runs the site. Emitted standalone on the homepage and embedded as the
    /// publisher elsewhere, so it carries no <c>@id</c> — the <c>url</c> already
    /// identifies it, and a fragment id would only be meaningful to a resolver
    /// that has the rest of the graph in hand.
    /// </summary>
    public static JsonObject BuildOrganization(
        Dictionary<string, string?> settings, string baseUrl, IReadOnlyList<Language> languages,
        IReadOnlyDictionary<string, MediaInfo>? media = null)
    {
        var node = new JsonObject
        {
            [SchemaGraph.TypeKey] = SchemaCatalog.Organization.Type,
            // The site's own address identifies the organisation. No invented
            // fragment: "…/#organization" points at nothing that exists.
            [SchemaGraph.IdKey] = baseUrl,
            ["name"] = Value(settings, SiteNameKey) ?? baseUrl,
            ["url"] = baseUrl,
        };

        SetIfPresent(node, "description", Value(settings, TaglineKey));

        // logo as an ImageObject rather than a bare string: search engines read
        // dimensions and captions off it when they are present later.
        string? logo = Value(settings, LogoKey);
        if (!string.IsNullOrWhiteSpace(logo))
            node["logo"] = ImageObject(Absolute(baseUrl, logo), media);

        JsonArray sameAs = [];
        foreach (string key in SocialKeys)
        {
            string? url = Value(settings, key);
            if (!string.IsNullOrWhiteSpace(url))
                sameAs.Add(url);
        }
        if (sameAs.Count > 0)
            node["sameAs"] = sameAs;

        JsonObject? contact = BuildContactPoint(settings, languages);
        if (contact is not null)
            node["contactPoint"] = new JsonArray(contact);

        JsonObject? address = BuildAddress(settings);
        if (address is not null)
            node["address"] = address;

        SetIfPresent(node, "telephone", Value(settings, PhoneKey));
        SetIfPresent(node, "email", Value(settings, EmailKey));
        SetIfPresent(node, "areaServed", Value(settings, CountryKey));

        return node;
    }

    private static JsonObject? BuildContactPoint(
        Dictionary<string, string?> settings, IReadOnlyList<Language> languages)
    {
        string? phone = Value(settings, PhoneKey);
        string? email = Value(settings, EmailKey);
        if (string.IsNullOrWhiteSpace(phone) && string.IsNullOrWhiteSpace(email))
            return null;

        var node = new JsonObject
        {
            [SchemaGraph.TypeKey] = SchemaCatalog.ContactPoint.Type,
            ["contactType"] = "customer service",
        };

        SetIfPresent(node, "telephone", phone);
        SetIfPresent(node, "email", email);
        SetIfPresent(node, "areaServed", Value(settings, CountryKey));

        // The languages the site is actually published in — the honest answer to
        // "can I be helped in my language", taken from the language list rather
        // than assumed.
        if (languages.Count > 0)
        {
            JsonArray available = [];
            foreach (Language language in languages)
                available.Add(language.NameInEnglish);
            node["availableLanguage"] = available;
        }

        return node;
    }

    private static JsonObject? BuildAddress(Dictionary<string, string?> settings)
    {
        string?[] parts =
        [
            Value(settings, AddressKey), Value(settings, LocalityKey),
            Value(settings, RegionKey), Value(settings, PostalCodeKey), Value(settings, CountryKey),
        ];

        if (parts.All(string.IsNullOrWhiteSpace))
            return null;

        var node = new JsonObject { [SchemaGraph.TypeKey] = SchemaCatalog.PostalAddress.Type };
        SetIfPresent(node, "streetAddress", parts[0]);
        SetIfPresent(node, "addressLocality", parts[1]);
        SetIfPresent(node, "addressRegion", parts[2]);
        SetIfPresent(node, "postalCode", parts[3]);
        SetIfPresent(node, "addressCountry", parts[4]);
        return node;
    }

    // ── WebSite ───────────────────────────────────────────────────────────────

    /// <summary>
    /// The site as a whole. Carries no <c>@id</c>: the site's URL already belongs to
    /// the Organization node, and minting "…/#website" would invent an address for
    /// something that has a real one.
    /// <para>
    /// One site, described the same on every page: its address is the root, its
    /// languages are all the site is published in, its search is the default
    /// language's. It used to take the page's own language — so an English page
    /// said the site at "/" (the Turkish homepage) was in English, with the English
    /// search. A page's own language is on the WebPage node, where it belongs, and
    /// a per-language WebSite at "/en" would not help: Google reads the site name
    /// from the domain's root only.
    /// </para>
    /// </summary>
    /// <param name="languages">The site's published languages, default included.</param>
    public static async Task<JsonObject> BuildWebSiteAsync(
        ICmsApplicationDbContext db,
        Dictionary<string, string?> settings,
        string baseUrl,
        IReadOnlyList<Language> languages,
        CancellationToken cancellationToken)
    {
        var node = new JsonObject
        {
            [SchemaGraph.TypeKey] = SchemaCatalog.WebSite.Type,
            ["url"] = baseUrl,
            // Points at the Organization by its real address rather than a fragment.
            ["publisher"] = SchemaGraph.Reference(baseUrl),
        };

        SetIfPresent(node, "name", Value(settings, SiteNameKey));

        // Default language first; a single value when there is only one.
        List<string> codes = [.. languages
            .OrderByDescending(l => l.IsDefault)
            .Select(l => l.TwoLetterCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)];
        if (codes.Count == 1)
            node["inLanguage"] = codes[0];
        else if (codes.Count > 1)
            node["inLanguage"] = new JsonArray([.. codes.Select(c => (JsonNode?)c)]);

        int? defaultLanguageId = languages.FirstOrDefault(l => l.IsDefault)?.Id;
        JsonObject? search = defaultLanguageId is int languageId
            ? await BuildSearchActionAsync(db, languageId, baseUrl, cancellationToken)
            : null;
        if (search is not null)
            node["potentialAction"] = search;

        return node;
    }

    // ── WebPage ───────────────────────────────────────────────────────────────

    /// <param name="page">The page, with its content and language loaded.</param>
    /// <param name="baseUrl">The public site's address.</param>
    /// <param name="context">When given, the image falls back the way og:image does and
    /// carries its size, and a page with an Author Bio but no Article names its author.</param>
    public static JsonObject BuildWebPage(PageInfo page, string baseUrl, ArticleContext? context = null)
    {
        string pageUrl = PageUrl(baseUrl, page);

        var node = new JsonObject
        {
            // A category page is a collection of other pages — the list on it is
            // added as an ItemList by the public site, where it is always current.
            [SchemaGraph.TypeKey] = page.Kind == PageKind.Category ? SchemaCatalog.CollectionPage.Type : SchemaCatalog.WebPage.Type,
            // The page's own address, not "…#webpage" — this is the one thing in
            // the graph that genuinely has a URL of its own.
            [SchemaGraph.IdKey] = pageUrl,
            ["url"] = pageUrl,
            ["isPartOf"] = SchemaGraph.Reference(baseUrl),
        };

        SetIfPresent(node, "name", page.SeoMeta.Title);
        SetIfPresent(node, "description", page.SeoMeta.MetaDescription);
        SetIfPresent(node, "inLanguage", page.Language?.TwoLetterCode);

        string? image = context?.ShareImage ?? page.SeoMeta.OgImage;
        if (!string.IsNullOrWhiteSpace(image))
            node["image"] = ImageObject(Absolute(baseUrl, image), context?.Media);

        node["datePublished"] = ToIso(context?.DatePublished ?? page.PublishedAt ?? page.CreateDate);
        if ((context?.DateModified ?? page.UpdateDate) is { } updated)
            node["dateModified"] = ToIso(updated);

        // An Article node carries the author itself; without one, the page does.
        string? html = page.Content?.GjsHtml;
        if (context is not null && html?.Contains("data-elevare-article", StringComparison.Ordinal) != true
            && BuildAuthorFromBio(html, context) is { } author)
        {
            node["author"] = author;
        }

        return node;
    }

    /// <summary>
    /// A site-search action, but only when a search page actually exists — pointing
    /// a crawler at a URL that 404s is worse than saying nothing.
    /// </summary>
    private static async Task<JsonObject?> BuildSearchActionAsync(
        ICmsApplicationDbContext db, int languageId, string baseUrl, CancellationToken cancellationToken)
    {
        string[] searchSlugs = ["arama", "search"];

        string? slug = await db.PageInfos
            .Where(p => p.LanguageId == languageId
                && p.ParentPageId == null
                && searchSlugs.Contains(p.Slug)
                && p.PageStatus == PageStatus.Published
                && p.IsActive)
            .Select(p => p.FullSlug)
            .FirstOrDefaultAsync(cancellationToken);

        if (slug is null)
            return null;

        return new JsonObject
        {
            [SchemaGraph.TypeKey] = "SearchAction",
            ["target"] = $"{baseUrl}/{slug}?q={{search_term}}",
            ["query"] = "required",
        };
    }

    // ── BreadcrumbList ────────────────────────────────────────────────────────

    /// <summary>
    /// The page's trail, starting at the homepage. Each step's <c>item</c> is an
    /// object carrying the URL and the name together, which is what lets a consumer
    /// render the crumb without a second lookup.
    /// <para>
    /// Returns null when the trail would be the homepage alone — a one-item
    /// breadcrumb describes no hierarchy.
    /// </para>
    /// </summary>
    public static async Task<JsonObject?> BuildBreadcrumbAsync(
        ICmsApplicationDbContext db,
        PageInfo page,
        string baseUrl,
        Dictionary<string, string?> settings,
        CancellationToken cancellationToken)
    {
        if (IsHomePage(page))
            return null;

        List<(string Name, string Url)> trail = [];

        int? parentId = page.ParentPageId;
        int guard = 0;
        while (parentId is not null && guard++ <= PageHierarchyDepthGuard)
        {
            var parent = await db.PageInfos
                .Where(p => p.Id == parentId)
                .Select(p => new { p.SeoMeta.Title, p.Slug, p.FullSlug, p.ParentPageId })
                .FirstOrDefaultAsync(cancellationToken);

            if (parent is null) break;

            // The homepage is the trail's first step, added below — once. Walking
            // into it here as well gave every page under it two "home" crumbs.
            if (parent.ParentPageId is null && string.Equals(parent.Slug, HomeSlug, StringComparison.OrdinalIgnoreCase))
                break;

            string name = string.IsNullOrWhiteSpace(parent.Title) ? parent.Slug : parent.Title;
            trail.Insert(0, (name, $"{baseUrl}/{parent.FullSlug}"));
            parentId = parent.ParentPageId;
        }

        // The homepage of this page's language — its own title (not the brand
        // name: a crumb reading "Elevare" where the page says "Ana Sayfa" looks
        // machine-written) and its own address: "/" for the default language,
        // "/en" for English. It used to be "/" whatever the language, so an English
        // trail began at the Turkish homepage.
        var home = await db.PageInfos
            .Where(p => p.LanguageId == page.LanguageId
                && p.ParentPageId == null
                && p.Slug == HomeSlug)
            .Select(p => new { p.SeoMeta.Title, p.FullSlug })
            .FirstOrDefaultAsync(cancellationToken);

        string homeName = !string.IsNullOrWhiteSpace(home?.Title)
            ? home.Title
            : Value(settings, SiteNameKey) ?? baseUrl;
        string homeUrl = $"{baseUrl}/{home?.FullSlug}";

        trail.Insert(0, (homeName, homeUrl));

        string currentName = string.IsNullOrWhiteSpace(page.SeoMeta.Title) ? page.Slug : page.SeoMeta.Title;
        trail.Add((currentName, PageUrl(baseUrl, page)));

        if (trail.Count < 2)
            return null;

        JsonArray items = [];
        for (int i = 0; i < trail.Count; i++)
        {
            items.Add(new JsonObject
            {
                [SchemaGraph.TypeKey] = SchemaCatalog.ListItem.Type,
                ["position"] = i + 1,
                ["item"] = new JsonObject
                {
                    [SchemaGraph.IdKey] = trail[i].Url,
                    ["name"] = trail[i].Name,
                },
            });
        }

        return new JsonObject
        {
            [SchemaGraph.TypeKey] = SchemaCatalog.BreadcrumbList.Type,
            // No id: a trail is not a resource with an address of its own, and
            // "…#breadcrumb" would claim otherwise.
            ["itemListElement"] = items,
        };
    }

    // ── FAQPage ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Lifts questions and answers straight out of the page's FAQ block.
    /// <para>
    /// This is the one place the draft reads page CONTENT rather than metadata,
    /// and it is worth it: an editor who has already written six questions should
    /// not have to retype them into a form. Reads the <c>data-elevare-faq-*</c>
    /// hints the FAQ block emits — never schema.org microdata, which the blocks no
    /// longer carry precisely so the two can never disagree.
    /// </para>
    /// <para>
    /// Returns null when the page has no FAQ block, or has one with nothing filled
    /// in: an empty FAQPage node claims the page answers questions it does not.
    /// </para>
    /// </summary>
    public static JsonObject? BuildFaq(string? html, string pageUrl)
    {
        if (string.IsNullOrWhiteSpace(html) || !html.Contains("data-elevare-faq", StringComparison.Ordinal))
            return null;

        JsonArray questions = [];
        try
        {
            var parser = new HtmlParser();
            using AngleSharp.Html.Dom.IHtmlDocument document = parser.ParseDocument(html);

            foreach (AngleSharp.Dom.IElement item in document.QuerySelectorAll("[data-elevare-faq-item]"))
            {
                string? question = item.QuerySelector("[data-elevare-faq-question]")?.TextContent?.Trim();
                string? answer = item.QuerySelector("[data-elevare-faq-answer]")?.TextContent?.Trim();

                if (string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(answer))
                    continue;

                questions.Add(new JsonObject
                {
                    [SchemaGraph.TypeKey] = SchemaCatalog.Question.Type,
                    ["name"] = question,
                    ["acceptedAnswer"] = new JsonObject
                    {
                        [SchemaGraph.TypeKey] = SchemaCatalog.Answer.Type,
                        ["text"] = answer,
                    },
                });
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Unparseable content must not sink the whole draft — every other node
            // is still correct and useful without the FAQ.
            return null;
        }

        if (questions.Count == 0)
            return null;

        return new JsonObject
        {
            [SchemaGraph.TypeKey] = SchemaCatalog.FaqPage.Type,
            // The only fragment id in the graph, and it earns it: the FAQ is a
            // distinct thing living on the page, not the page itself.
            [SchemaGraph.IdKey] = $"{pageUrl}#faq",
            ["mainEntity"] = questions,
        };
    }

    // ── Article ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Lifts headline, author and publish date straight out of the page's Article
    /// block — same reasoning and the same <c>data-elevare-article-*</c> hint
    /// pattern as <see cref="BuildFaq"/>, and for the same reason: an editor who
    /// already wrote a headline and byline should not have to retype them into a
    /// second form, and the hints (never schema.org microdata) are the only
    /// source, so the block and the graph can never disagree.
    /// <para>
    /// Returns null when the page has no Article block, or its headline was never
    /// filled in — an Article node with no headline claims nothing useful.
    /// </para>
    /// </summary>
    public static JsonObject? BuildArticle(string? html, string pageUrl, ArticleContext? context = null)
    {
        if (string.IsNullOrWhiteSpace(html) || !html.Contains("data-elevare-article", StringComparison.Ordinal))
            return null;

        try
        {
            var parser = new HtmlParser();
            using AngleSharp.Html.Dom.IHtmlDocument document = parser.ParseDocument(html);

            AngleSharp.Dom.IElement? root = document.QuerySelector("[data-elevare-article]");
            if (root is null)
                return null;

            string? headline = Clean(root.QuerySelector("[data-elevare-article-headline]")?.TextContent);
            if (string.IsNullOrWhiteSpace(headline))
                return null;

            var node = new JsonObject
            {
                [SchemaGraph.TypeKey] = SchemaCatalog.Article.Type,
                // Same reasoning as the FAQ node's fragment: a distinct thing
                // living on the page, not the page itself.
                [SchemaGraph.IdKey] = $"{pageUrl}#article",
                // Google cuts an Article headline at 110 characters.
                ["headline"] = headline.Length <= 110 ? headline : headline[..110].TrimEnd(),
            };

            JsonObject? author = BuildAuthor(document, BylineName(root), context);
            if (author is not null)
                node["author"] = author;

            // The date the byline SHOWS wins over the <time datetime> behind it —
            // editing the text on the canvas never updated the attribute, so the two
            // drifted (a page read "13 Ağustos 2023" and said 2026-01-01). See
            // HumanDateParser.ResolveArticleDate.
            AngleSharp.Dom.IElement? time = root.QuerySelector("[data-elevare-article-date]");
            if (time is not null
                && HumanDateParser.ResolveArticleDate(time.GetAttribute("datetime"), time.ParentElement?.TextContent ?? time.TextContent) is { } published)
            {
                node["datePublished"] = ToIso(published);
            }
            else if (context?.DatePublished is { } pagePublished)
            {
                node["datePublished"] = ToIso(pagePublished);
            }
            if (context?.DateModified is { } modified)
                node["dateModified"] = ToIso(modified);

            // The page's own description when it has one — written as a summary,
            // which the opening paragraph only approximates.
            string? opening = Clean(root.QuerySelector("[data-elevare-article-body] p")?.TextContent);
            SetIfPresent(node, "description", FirstFilled(context?.MetaDescription, Truncate(opening, 300)));

            if (context is not null)
            {
                string? image = FirstFilled(FirstContentImage(root), context.ShareImage);
                if (image is not null)
                    node["image"] = ImageObject(Absolute(context.BaseUrl, image), context.Media);

                node["publisher"] = SchemaGraph.Reference(context.BaseUrl);
                node["mainEntityOfPage"] = SchemaGraph.Reference(pageUrl);
                SetIfPresent(node, "inLanguage", context.Language);
                if (context.Keywords.Count > 0)
                    node["keywords"] = string.Join(", ", context.Keywords);
            }

            return node;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Unparseable content must not sink the whole draft — every other
            // node is still correct and useful without the Article.
            return null;
        }
    }

    // ── Person (author) ───────────────────────────────────────────────────────

    /// <summary>
    /// The page's author, from its Author Bio block — name, title, bio, portrait and
    /// profile links, all already written there — or just the byline's name when the
    /// page has no bio for that person.
    /// </summary>
    /// <remarks>
    /// Newer bio blocks mark each part (<c>data-elevare-author-*</c>); older ones are
    /// read by their shape, which the block has always had: the name is the heading,
    /// the title the short line under it, the bio the paragraph after that. A byline
    /// naming someone else than the bio keeps just its own name: a bio further down
    /// the page is not necessarily about this article's author.
    /// </remarks>
    public static JsonObject? BuildAuthor(AngleSharp.Html.Dom.IHtmlDocument document, string? bylineName, ArticleContext? context)
    {
        ArgumentNullException.ThrowIfNull(document);
        JsonObject? bio = ReadAuthorBio(document, context);
        string? bioName = bio?["name"]?.GetValue<string>();

        if (bio is not null && (bylineName is null || SameName(bylineName, bioName)))
            return bio;
        if (string.IsNullOrWhiteSpace(bylineName))
            return null;

        return new JsonObject
        {
            [SchemaGraph.TypeKey] = SchemaCatalog.Person.Type,
            ["name"] = bylineName,
        };
    }

    /// <summary>The Author Bio block's person, or null when the page has none.</summary>
    public static JsonObject? BuildAuthorFromBio(string? html, ArticleContext? context)
    {
        if (string.IsNullOrWhiteSpace(html) || !html.Contains("elevare-author", StringComparison.Ordinal))
            return null;
        try
        {
            using AngleSharp.Html.Dom.IHtmlDocument document = new HtmlParser().ParseDocument(html);
            return ReadAuthorBio(document, context);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    private static JsonObject? ReadAuthorBio(AngleSharp.Html.Dom.IHtmlDocument document, ArticleContext? context)
    {
        AngleSharp.Dom.IElement? box = document.QuerySelector("[data-elevare-author]")
            ?? document.QuerySelector("[data-elevare-block='elevare-authorbio']");
        if (box is null)
            return null;

        AngleSharp.Dom.IElement? heading = box.QuerySelector("[data-elevare-author-name]")
            ?? box.QuerySelector("h1, h2, h3, h4, h5, h6");
        string? name = Clean(heading?.TextContent);
        if (string.IsNullOrWhiteSpace(name) || heading is null)
            return null;

        // The paragraphs after the name, in order: a short title line, then the bio.
        List<string> after = [];
        for (AngleSharp.Dom.IElement? e = heading.NextElementSibling; e is not null; e = e.NextElementSibling)
        {
            if (e.LocalName == "p" && Clean(e.TextContent) is { Length: > 0 } text)
                after.Add(text);
        }
        string? role = Clean(box.QuerySelector("[data-elevare-author-role]")?.TextContent)
            ?? after.FirstOrDefault(t => t.Length <= 120);
        string? bioText = Clean(box.QuerySelector("[data-elevare-author-bio]")?.TextContent)
            ?? after.FirstOrDefault(t => t != role && t.Length >= 40);

        var person = new JsonObject
        {
            [SchemaGraph.TypeKey] = SchemaCatalog.Person.Type,
            ["name"] = name,
        };
        SetIfPresent(person, "jobTitle", role);
        SetIfPresent(person, "description", bioText);

        string? portrait = UsableImage(box.QuerySelector("[data-elevare-author-image]") ?? box.QuerySelector("img"));
        if (portrait is not null && context is not null)
            person["image"] = ImageObject(Absolute(context.BaseUrl, portrait), context.Media);

        JsonArray sameAs = [];
        foreach (AngleSharp.Dom.IElement link in box.QuerySelectorAll("a[href]"))
        {
            string href = link.GetAttribute("href")!.Trim();
            if (href.StartsWith("http", StringComparison.OrdinalIgnoreCase) && !sameAs.Any(s => s!.GetValue<string>() == href))
                sameAs.Add(href);
        }
        if (sameAs.Count > 0)
            person["sameAs"] = sameAs;

        return person;
    }

    /// <summary>
    /// The byline's name: the marked element when the block still has it, otherwise
    /// read off the line itself — "Yazar: Ada Lovelace · 5 Mart 2026" — since editing
    /// the name on the canvas can wrap it in formatting and lose the marker.
    /// </summary>
    private static string? BylineName(AngleSharp.Dom.IElement article)
    {
        string? marked = Clean(article.QuerySelector("[data-elevare-article-author]")?.TextContent);
        if (!string.IsNullOrWhiteSpace(marked))
            return marked;

        string? line = Clean(article.QuerySelector("[data-elevare-article-date]")?.ParentElement?.TextContent);
        System.Text.RegularExpressions.Match m = line is null
            ? System.Text.RegularExpressions.Match.Empty
            : System.Text.RegularExpressions.Regex.Match(line, @"^(?:Yazar|By|Author)\s*:?\s*(?<name>[^·|•]+?)\s*(?:[·|•]|$)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
        return m.Success ? Clean(m.Groups["name"].Value) : null;
    }

    private static bool SameName(string a, string? b) =>
        b is not null && CultureInfo.InvariantCulture.CompareInfo.Compare(
            a, b, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) == 0;

    // ── Images ────────────────────────────────────────────────────────────────

    /// <summary>
    /// The image a page is shared with — the same chain the public site uses for
    /// og:image (SocialDefaultsResolutionService there): the page's own share image,
    /// else the first image in its content, else the site's default share image.
    /// </summary>
    public static string? ShareImage(PageInfo page, Dictionary<string, string?> settings)
    {
        ArgumentNullException.ThrowIfNull(page);
        string? firstInContent = null;
        if (!string.IsNullOrWhiteSpace(page.Content?.GjsHtml))
        {
            try
            {
                using AngleSharp.Html.Dom.IHtmlDocument document = new HtmlParser().ParseDocument(page.Content.GjsHtml);
                foreach (AngleSharp.Dom.IElement notContent in document.QuerySelectorAll(NotContentSelector).ToList())
                    notContent.Remove();
                firstInContent = document.Body is null ? null : FirstContentImage(document.Body);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                firstInContent = null;
            }
        }
        return FirstFilled(page.SeoMeta.OgImage, firstInContent, Value(settings, DefaultShareImageKey));
    }

    /// <summary>Every image address a draft may describe, for one media-library lookup.</summary>
    public static IReadOnlyList<string> ImageCandidates(PageInfo page, Dictionary<string, string?> settings)
    {
        ArgumentNullException.ThrowIfNull(page);
        List<string> found = [];
        void Add(string? src) { if (!string.IsNullOrWhiteSpace(src)) found.Add(src.Trim()); }

        Add(page.SeoMeta.OgImage);
        Add(Value(settings, DefaultShareImageKey));
        Add(Value(settings, LogoKey));
        if (!string.IsNullOrWhiteSpace(page.Content?.GjsHtml))
        {
            try
            {
                using AngleSharp.Html.Dom.IHtmlDocument document = new HtmlParser().ParseDocument(page.Content.GjsHtml);
                foreach (AngleSharp.Dom.IElement img in document.QuerySelectorAll("img[src]"))
                    Add(img.GetAttribute("src"));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Content that does not parse just contributes no candidates.
            }
        }
        return [.. found.Distinct(StringComparer.Ordinal)];
    }

    /// <summary>The key a media-library entry is matched by: its site-relative path.</summary>
    public static string MediaKey(string src) =>
        Uri.TryCreate(src, UriKind.Absolute, out Uri? uri) ? uri.AbsolutePath : src;

    private const string DefaultShareImageKey = "Seo.DefaultOgImageUrl";

    // Not the page's own content: linked header/footer templates (a logo is not what
    // the page is about), a page-level menu, and what is hidden or decorative.
    private const string NotContentSelector =
        ".elevare-tpl-ref, body > header, body > footer, body > nav, script, noscript, template, [hidden], [aria-hidden='true']";

    private static string? FirstContentImage(AngleSharp.Dom.IElement scope)
    {
        foreach (AngleSharp.Dom.IElement img in scope.QuerySelectorAll("img[src]"))
        {
            if (int.TryParse(img.GetAttribute("width"), out int width) && width < 200)
                continue;
            if (UsableImage(img) is { } src)
                return src;
        }
        return null;
    }

    // A real file: not an inline data: placeholder (what an untouched block ships
    // with) and not an SVG, which no share card or rich result displays.
    private static string? UsableImage(AngleSharp.Dom.IElement? img)
    {
        string? src = img?.GetAttribute("src")?.Trim();
        if (string.IsNullOrEmpty(src)
            || src.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
            || src.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            return null;
        return src;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    public static string? Value(Dictionary<string, string?> settings, string key) =>
        settings.TryGetValue(key, out string? v) && !string.IsNullOrWhiteSpace(v) ? v : null;

    /// <summary>
    /// An image with whatever the media library knows about it — width and height
    /// (Google wants them for Article images and logos), type and caption — so the
    /// editor never has to look them up and type them in.
    /// </summary>
    private static JsonObject ImageObject(string url, IReadOnlyDictionary<string, MediaInfo>? media = null)
    {
        var node = new JsonObject
        {
            [SchemaGraph.TypeKey] = "ImageObject",
            ["url"] = url,
        };
        if (media is null
            || !(media.TryGetValue(url, out MediaInfo? info) || media.TryGetValue(MediaKey(url), out info)))
            return node;

        if (info.Width is > 0 && info.Height is > 0)
        {
            node["width"] = info.Width.Value;
            node["height"] = info.Height.Value;
        }
        SetIfPresent(node, "encodingFormat", info.MimeType);
        SetIfPresent(node, "caption", info.AltText);
        return node;
    }

    private static string? FirstFilled(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    /// <summary>Text as a reader sees it: runs of whitespace (line breaks in the markup) are one space.</summary>
    private static string? Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        return Whitespace().Replace(text, " ").Trim();
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"\s+")]
    private static partial System.Text.RegularExpressions.Regex Whitespace();

    /// <summary>
    /// Site-relative media paths become absolute. A crawler resolves the JSON
    /// without a base href, so "/img/logo.png" would resolve against schema.org.
    /// </summary>
    private static string Absolute(string baseUrl, string url) =>
        url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? url
            : $"{baseUrl}/{url.TrimStart('/')}";

    private static void SetIfPresent(JsonObject node, string property, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            node[property] = value;
    }

    /// <summary>
    /// Cuts on a character boundary rather than a word one — good enough for a
    /// fallback description nobody reads character-for-character, and simpler
    /// than hunting for the nearest preceding space in a string that might not
    /// have one nearby (a URL, a long compound word).
    /// </summary>
    private static string? Truncate(string? text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        return text.Length <= maxLength ? text : text[..maxLength].TrimEnd() + "…";
    }

    /// <summary>
    /// Dates are stored as UTC, and schema.org wants an offset — without one a
    /// consumer has to guess the timezone, which shifts publication dates by hours.
    /// <para>
    /// Formatted against the invariant culture on purpose: under a Turkish locale
    /// the same pattern would otherwise emit Hijri-era digits and non-Gregorian
    /// numerals, producing a date string no crawler can read.
    /// </para>
    /// </summary>
    private static string ToIso(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc)
            .ToString("yyyy-MM-dd'T'HH:mm:ssK", CultureInfo.InvariantCulture);
}
