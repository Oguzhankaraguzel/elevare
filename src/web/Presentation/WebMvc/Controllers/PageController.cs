using Application.Abstraction.Services;
using Application.Features.Queries.Pages.GetPreviewPageById;
using Application.Features.Queries.Pages.GetPublicPageBySlug;
using Application.Features.Queries.Redirects.GetRedirectTarget;
using Application.Features.Queries.SiteCodeSnippets.GetPageSiteCodeExclusions;
using Application.Services;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Net.Http.Headers;
using SharedKernel.Concrete;

namespace WebMvc.Controllers;

/// <summary>
/// Generic renderer for every builder page — INCLUDING the homepage. This is now
/// the site's only content controller. Reached two ways:
/// <list type="bullet">
///   <item>Via <see cref="Routing.SlugRouteValueTransformer"/> for language-prefixed
///   URLs (<c>/{languageCode}/{slug}</c>) — <paramref name="languageCode"/>/<paramref name="slug"/>
///   arrive pre-populated as route values.</item>
///   <item>Via <c>MapFallbackToController</c> for everything else that didn't match
///   any other endpoint — a true last resort, meaning "no language prefix" i.e. the
///   CMS's current default language.</item>
/// </list>
/// </summary>
[Route("")]
public sealed class PageController(
    ISender sender,
    TemplateResolutionService templateResolver,
    LanguageSwitcherResolutionService languageSwitcherResolver,
    PageListingResolutionService pageListingResolver,
    AdjacentPageResolutionService adjacentPageResolver,
    BreadcrumbResolutionService breadcrumbResolver,
    ResponsiveImageResolutionService responsiveImageResolver,
    IPreviewTokenValidator previewTokenService,
    ILanguageDirectory languageDirectory) : BaseController
{
    /// <summary>Evicted by <c>/api/cache/clear</c>, which the CMS calls after every successful change.</summary>
    public const string OutputCacheTag = "pages";

    /// <summary>The output-cache policy for rendered pages — see Program.cs and <see cref="ListingQuery"/>.</summary>
    public const string OutputCachePolicy = "Pages";

    // A rendered page is kept for an hour. That is only safe because it does not
    // have to expire on its own: every successful CMS command asks the site to drop
    // this tag (PublicSiteCacheInvalidationPipelineBehavior in the CMS), so an edit
    // is live on the next request. The hour is the ceiling for the case where that
    // call cannot get through. Of the query string, only the listing parameters are
    // part of the key: they are the only part a page reacts to.
    [OutputCache(PolicyName = OutputCachePolicy)]
    public async Task<IActionResult> Index(string? languageCode, string? slug, CancellationToken cancellationToken)
    {
        // The default language lives at the root, so its prefixed address is a second
        // address for the same page: with English default, /en/about is /about. A 301
        // rather than serving it — it is where every English URL lived before English
        // became the default, and those links should carry over, not compete.
        if (languageCode is not null
            && string.Equals(languageCode, languageDirectory.DefaultLanguageCode, StringComparison.OrdinalIgnoreCase))
            return RedirectPermanent($"{Request.PathBase}/{(slug ?? string.Empty).Trim('/')}{Request.QueryString}");

        string resolvedLanguageCode = languageCode ?? languageDirectory.DefaultLanguageCode;

        // languageCode is only non-null when reached via the language-prefixed route
        // (SlugRouteValueTransformer) — there, a missing slug always means "this
        // language's homepage", regardless of the raw request path. ASP.NET Core's
        // model binder converts an empty catch-all route value to null for a
        // string? parameter, so a bare "/en" arrives with slug == null here too —
        // falling back to Request.Path.Value in that branch would wrongly resolve
        // to the language code itself ("en") instead of the homepage. Only the true
        // fallback route (no language prefix at all, so no separate slug route
        // value exists) needs the raw path to recover the requested slug.
        //
        // An empty resolvedSlug IS the homepage request — it is never rewritten to
        // "home" here. PageInfo.ComputeFullSlug never stores "home" as a real segment
        // either (the homepage's own FullSlug is "" or just the language code), so
        // the two sides already agree without a translation step in between.
        string resolvedSlug = languageCode is not null
            ? slug ?? string.Empty
            : slug ?? Request.Path.Value?.Trim('/') ?? string.Empty;

        Result<PublicPageResponse> result = await sender.Send(
            new GetPublicPageBySlugQuery(resolvedLanguageCode, resolvedSlug), cancellationToken);

        if (result.IsFailure)
        {
            // Before giving up, check whether this path has a redirect rule (old slug
            // after a rename, or a deleted/archived page's replacement URL) — a 301
            // here is what keeps bookmarked/indexed links from dead-ending on a 404.
            Result<string> redirect = await sender.Send(
                new GetRedirectTargetQuery(resolvedLanguageCode, resolvedSlug), cancellationToken);
            if (redirect.IsSuccess)
                return RedirectPermanent(redirect.Value);

            // The query's error explains the real cause (no such page / exists but
            // Draft / inactive) — surface it so logs and the debugger tell the truth.
            // The visitor still just sees the 404 page.
            throw NotFoundResource(result.Error.Description);
        }

        ViewData["Lang"] = resolvedLanguageCode;

        // Resolve any "linked" template blocks embedded in the page content to their
        // current version — same-request, no caching, so edits propagate immediately.
        await ResolveAndStoreHtmlAsync(result.Value, cancellationToken);

        return View(result.Value);
    }


    /// <summary>
    /// Runs the three request-time HTML resolvers in order and puts the outcome into
    /// ViewData. Extracted so <see cref="Index"/> and <see cref="Preview"/> cannot
    /// drift apart — a preview that resolved differently from the live render would
    /// defeat the point of previewing.
    /// <para>
    /// A resolver failure throws rather than rendering half-resolved markup: an
    /// unresolved page would leak <c>elevare-tpl-ref</c> placeholders and empty
    /// listings to the visitor while looking deliberate, and would keep the outage
    /// invisible. The error page is the honest answer, and the middleware logs the
    /// reason (which the Result carries) to AppLogs on the way out.
    /// </para>
    /// </summary>
    private async Task ResolveAndStoreHtmlAsync(PublicPageResponse page, CancellationToken cancellationToken)
    {
        Result<ResolvedTemplateContent> templates = await templateResolver.ResolveAsync(page.GjsHtml, cancellationToken);
        if (templates.IsFailure)
            throw new InvalidOperationException(templates.Error.Description);

        Result<string?> withSwitcher = await languageSwitcherResolver.ResolveAsync(
            templates.Value.Html, page.Id, cancellationToken);
        if (withSwitcher.IsFailure)
            throw new InvalidOperationException(withSwitcher.Error.Description);

        // A page's public address is "/" + FullSlug, language prefix included — see
        // PageListingResolutionService.BuildPageUrl.
#pragma warning disable S1075
        string pagePath = "/" + page.FullSlug;
#pragma warning restore S1075
        Result<PageListingResolution> withListings = await pageListingResolver.ResolveAsync(
            withSwitcher.Value,
            new ListingRequest(page.Id, pagePath, page.LanguageCode, ToQueryDictionary(Request.Query)),
            cancellationToken);
        if (withListings.IsFailure)
            throw new InvalidOperationException(withListings.Error.Description);
        // "?page=99" of a three-page list is an address with nothing at it. A 404 says
        // so; an empty 200 is a soft 404 that search engines index as a thin page.
        if (withListings.Value.Pagination is { IsOutOfRange: true })
            throw NotFoundResource($"Listing page {withListings.Value.Pagination.Page} is past the last page ({withListings.Value.Pagination.TotalPages}).");

        Result<string?> withAdjacent = await adjacentPageResolver.ResolveAsync(
            withListings.Value.Html, page.Id, cancellationToken);
        if (withAdjacent.IsFailure)
            throw new InvalidOperationException(withAdjacent.Error.Description);

        Result<string?> withBreadcrumbs = await breadcrumbResolver.ResolveAsync(
            withAdjacent.Value, page.Id, cancellationToken);
        if (withBreadcrumbs.IsFailure)
            throw new InvalidOperationException(withBreadcrumbs.Error.Description);

        // Last on purpose: by now the HTML also contains whatever the linked
        // templates, page listings and breadcrumbs brought in, so images from those
        // get the same srcset/alt treatment as the page's own. The CSS handed over
        // is what the page will actually ship (page + templates, template last —
        // the same merge the layout renders), so the `sizes` hint reads the rule
        // that wins: a template's own width for its logo, not a stale copy of it
        // the page's CSS may still carry from before the template was linked.
        string shippedCss = CssRuleDeduplicator.Merge(page.GjsCss, templates.Value.Css);
        Result<string?> withResponsiveImages = await responsiveImageResolver.ResolveAsync(
            withBreadcrumbs.Value, shippedCss, cancellationToken);
        if (withResponsiveImages.IsFailure)
            throw new InvalidOperationException(withResponsiveImages.Error.Description);

        // Split LAST, once every resolver has had its turn: the menu and footer can
        // themselves be linked templates, carry language switchers or breadcrumbs, and
        // hold images that need srcset — all of which only exist by this point.
        // Splitting earlier would mean running the whole chain three times.
        // Code colouring and reading times: from the final markup, so a code block or
        // an article inside a linked template gets them too.
        string? enhanced = await ContentEnhancer.EnhanceAsync(withResponsiveImages.Value, page.LanguageCode, cancellationToken);

        PageRegions regions = await PageRegionSplitter.SplitAsync(enhanced, cancellationToken);

        ViewData["ResolvedHtml"] = regions.Main;
        ViewData["PageHeaderHtml"] = regions.Header;
        ViewData["PageFooterHtml"] = regions.Footer;
        // One stylesheet, not two concatenated: the page's own CSS already carries a
        // copy of every linked template's rules (the editor saved them there), and
        // the resolver hands us the template's copy as well. See CssRuleDeduplicator.
        ViewData["PageCss"] = shippedCss;
        // Feeds the page-number-aware <link rel=canonical/prev/next> in Index.cshtml —
        // null when this page has no "Sayfa Listesi" block at all.
        ViewData["ListingPagination"] = withListings.Value.Pagination;
        // The site codes this page's author switched off; the layout skips them.
        // Failure here is not a render failure — the page is still whole without
        // the exclusions, it just carries every site code, which is the default.
        Result<HashSet<int>> excluded = await sender.Send(new GetPageSiteCodeExclusionsQuery(page.Id), cancellationToken);
        ViewData[SiteCodeExclusions.ViewDataKey] = excluded.IsSuccess ? excluded.Value : [];
    }

    private static Dictionary<string, string?> ToQueryDictionary(IQueryCollection query)
        => ListingQuery.Filter(query.Select(kv => new KeyValuePair<string, string?>(kv.Key, kv.Value.ToString())));

    /// <summary>
    /// Renders ANY page — regardless of PageStatus/IsActive — through the exact
    /// same view/template/language-switcher pipeline as <see cref="Index"/>, gated
    /// entirely by a signed, time-limited token (see <see cref="IPreviewTokenValidator"/>)
    /// generated by the CMS's "Önizle" button. Deliberately not output-cached and
    /// marked no-store so no intermediate cache ever serves a stale or
    /// token-specific response.
    /// </summary>
    [HttpGet("elevare-preview")]
    public async Task<IActionResult> Preview(string? token, CancellationToken cancellationToken)
    {
        if (!previewTokenService.TryValidate(token, out int pageId))
            throw NotFoundResource("Preview token is missing, malformed, expired, or has an invalid signature.");

        Result<PublicPageResponse> result = await sender.Send(new GetPreviewPageByIdQuery(pageId), cancellationToken);

        if (result.IsFailure)
            throw NotFoundResource(result.Error.Description);

        ViewData["Lang"] = result.Value.LanguageCode;
        ViewData["IsPreview"] = true;

        await ResolveAndStoreHtmlAsync(result.Value, cancellationToken);

        Response.Headers[HeaderNames.CacheControl] = "no-store";

        return View("Index", result.Value);
    }
}
