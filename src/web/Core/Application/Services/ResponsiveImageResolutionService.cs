using System.Data.Common;
using System.Globalization;
using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Dom;
using Application.Abstraction.Data;
using Application.Abstraction.Services;
using Domain.Entities.PublicMedia;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Concrete;
using SharedKernel.Media;

namespace Application.Services;

/// <summary>
/// Upgrades every media-library image embedded in a page to a responsive one at
/// render time: fills in <c>srcset</c>/<c>sizes</c> from the image's other size
/// variants, and <c>alt</c>/<c>width</c>/<c>height</c> from what the media library
/// already knows about it.
///
/// <para>
/// <b>Why this runs on the server instead of in the page builder.</b> The builder
/// can only help with images it inserted itself. An author who copies a URL out of
/// the media library and pastes it into a Custom Code block, or hand-writes an
/// <c>&lt;img&gt;</c>, gets none of it — and that is a normal thing to do, so a
/// builder-only solution would quietly produce two classes of image on the same
/// site. Resolving here means every image gets the same treatment no matter how it
/// arrived, including images embedded in pages long before variants existed: the
/// moment a second size is uploaded into the group, every page already using that
/// image starts serving a srcset, with nothing re-saved.
/// </para>
///
/// <para>
/// <b>What it will not do.</b> Author intent wins over inference, always. An
/// attribute that is already present is never rewritten — an explicit
/// <c>alt=""</c> means "decorative, skip me" and is left alone, a hand-written
/// <c>srcset</c> is left alone, and an image that is not from this site's media
/// library is not touched at all.
/// </para>
/// </summary>
public sealed class ResponsiveImageResolutionService(IPublicReadDbContext db)
{
    /// <summary>
    /// Only paths under the upload root are considered ours. Anything else in a
    /// page — an icon from a CDN, a hotlinked photograph — is left exactly as the
    /// author wrote it.
    /// </summary>
    private const string UploadPathMarker = "/uploads/";

    /// <summary>
    /// Below this, a second variant buys nothing: the browser would download a file
    /// barely smaller than the one it already has. Keeps a 1600px/1500px pair from
    /// producing a srcset that only adds bytes to the HTML.
    /// </summary>
    public Task<Result<string?>> ResolveAsync(string? html, CancellationToken cancellationToken) =>
        ResolveAsync(html, pageCss: null, cancellationToken);

    /// <param name="pageCss">The page's stylesheet, when the caller has it: a rule
    /// that gives an image a pixel width (<c>#id{width:200px}</c>) becomes that
    /// image's <c>sizes</c>, so a 200-pixel logo no longer downloads the 2000-pixel
    /// candidate that "100vw" would ask for.</param>
    public async Task<Result<string?>> ResolveAsync(string? html, string? pageCss, CancellationToken cancellationToken)
    {
        try
        {
            return await ResolveCoreAsync(html, pageCss, cancellationToken);
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException or TimeoutException)
        {
            return Result.Failure<string?>(RenderErrors.ResolutionFailed("responsive images", ex.Message));
        }
    }

    private async Task<Result<string?>> ResolveCoreAsync(string? html, string? pageCss, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(html) || !html.Contains(UploadPathMarker, StringComparison.OrdinalIgnoreCase))
            return Result.Success(html);

        IBrowsingContext context = BrowsingContext.New(Configuration.Default);
        using IDocument document = await context.OpenAsync(req => req.Content(html), cancellationToken);

        List<IElement> images = [.. document.QuerySelectorAll("img")];
        if (images.Count == 0)
            return Result.Success<string?>(document.Body?.InnerHtml ?? html);
        MarkEagerImages(images);

        // Match on path, not on the whole URL. The same file legitimately appears as
        // "/uploads/…" (what the CMS writes today), as an absolute URL on the public
        // host, and — in pages authored before media moved to relative URLs — as an
        // absolute URL on the CMS host. All three are the same image.
        Dictionary<IElement, string> pathByImage = [];
        foreach (IElement image in images)
        {
            string? path = ToUploadPath(image.GetAttribute("src"));
            if (path is not null)
                pathByImage[image] = path;
        }

        if (pathByImage.Count == 0)
            return Result.Success<string?>(document.Body?.InnerHtml ?? html);

        string[] paths = [.. pathByImage.Values.Distinct()];

        // One query: each embedded file carries its own renditions on its row.
        List<PublicMediaFile> embedded = await db.MediaFiles
            .Where(m => paths.Contains(m.FilePath))
            .ToListAsync(cancellationToken);

        if (embedded.Count == 0)
            return Result.Success<string?>(document.Body?.InnerHtml ?? html);

        var byPath = embedded
            .GroupBy(m => m.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        Dictionary<string, int> cssWidthById = CssPixelWidths(pageCss);

        foreach ((IElement image, string path) in pathByImage)
        {
            if (!byPath.TryGetValue(path, out PublicMediaFile? file))
                continue;

            IReadOnlyList<ImageRendition> renditions = ImageRenditions.Parse(file.Renditions);
            ApplyAlt(image, file);
            ApplyIntrinsicSize(image, file);
            ApplySrcset(image, file, renditions, cssWidthById);
            ApplyWebpSource(document, image, renditions);
        }

        return Result.Success<string?>(document.Body?.InnerHtml ?? html);
    }

    /// <summary>
    /// <c>fetchpriority="high"</c> on every image marked eager — the author's own
    /// "this is at the top of the page". Guessing which one of them the browser
    /// will call the Largest Contentful Paint went wrong both ways on the live
    /// site: "the first" picked the header's 66-pixel logo, "the largest" the
    /// photo in a narrow column over the full-width hero, because the server
    /// cannot see the layout. The eager images of a page are few — a logo and an
    /// opening image — so raising all of them is within what the guidance allows
    /// (the above-the-fold images, when which one is the LCP depends on the layout).
    /// An explicit value (low, auto, high) is the author's and is left alone.
    /// </summary>
    private static void MarkEagerImages(IEnumerable<IElement> images)
    {
        foreach (IElement image in images)
        {
            if (image.GetAttribute("loading") == "eager" && !image.HasAttribute("fetchpriority"))
                image.SetAttribute("fetchpriority", "high");
        }
    }

    // #id { … width: 200px … } — only an id rule with a pixel width, which is what
    // the builder writes for a sized image; percentages and everything else keep
    // the viewport-based fallback. The regex is deliberately narrow.
    private static readonly Regex CssIdWidthPattern = new(
        @"#(?<id>[A-Za-z][\w-]*)\s*\{(?<body>[^}]*)\}", RegexOptions.Compiled);
    private static readonly Regex CssWidthPattern = new(
        @"(?<![\w-])(?:max-)?width\s*:\s*(?<px>\d{2,4})px", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Mirrors the page listing block's mobile rule in the CMS (elevare-blocks.js):
    // ".elevare-page-listing .elevare-listing-items img { width: 100% }" below 767px.
    private const string ListingItemSelector = ".elevare-page-listing .elevare-listing-items";
    private const int ListingFullWidthBelowPx = 767;

    private static Dictionary<string, int> CssPixelWidths(string? css)
    {
        Dictionary<string, int> widths = [];
        if (string.IsNullOrWhiteSpace(css)) return widths;
        foreach (Match rule in CssIdWidthPattern.Matches(css))
        {
            Match width = CssWidthPattern.Match(rule.Groups["body"].Value);
            if (width.Success && int.TryParse(width.Groups["px"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int px) && px > 0)
                widths[rule.Groups["id"].Value] = px;
        }
        return widths;
    }

    /// <summary>
    /// Wraps the image in <c>&lt;picture&gt;</c> with the WebP renditions as a typed
    /// source, so a browser that reads WebP (all current ones) takes the file that
    /// is a quarter the size and the <c>&lt;img&gt;</c> below stays the JPEG/PNG
    /// fallback for the rest. Only when there are WebP copies — an image the CMS has
    /// not derived from yet is served as before.
    /// </summary>
    private static void ApplyWebpSource(IDocument document, IElement image, IReadOnlyList<ImageRendition> renditions)
    {
        if (image.ParentElement?.TagName.Equals("PICTURE", StringComparison.OrdinalIgnoreCase) == true) return;

        List<ImageRendition> webp = [.. renditions.Where(r => r.WebpPath is not null)];
        if (webp.Count == 0) return;

        IElement picture = document.CreateElement("picture");
        IElement source = document.CreateElement("source");
        source.SetAttribute("type", "image/webp");
        source.SetAttribute("srcset", string.Join(", ", webp.Select(r =>
            $"{r.WebpPath} {r.Width.ToString(CultureInfo.InvariantCulture)}w")));
        string? sizes = image.GetAttribute("sizes");
        if (!string.IsNullOrEmpty(sizes)) source.SetAttribute("sizes", sizes);

        image.Parent?.ReplaceChild(picture, image);
        picture.AppendChild(source);
        picture.AppendChild(image);
    }

    /// <summary>
    /// Reduces whatever the author wrote in <c>src</c> to the site-relative path the
    /// media library stores, or null when it is not one of our uploads.
    /// </summary>
    private static string? ToUploadPath(string? src)
    {
        if (string.IsNullOrWhiteSpace(src)) return null;

        string path = src;

        // Absolute URL (either host) → keep just the path. Checked by prefix, not by
        // asking Uri.TryCreate(..., UriKind.Absolute, ...) whether the string parses:
        // on Linux that call happily accepts a bare filesystem path like
        // "/uploads/images/…" too, handing back a "file" URI for it — which then
        // fails the scheme check below and rejects every relative src the CMS
        // actually writes. A prefix check has no such surprise.
        bool isHttpUrl = src.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || src.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

        if (isHttpUrl)
        {
            // data:/blob: URLs a builder can produce are neither this nor the
            // relative branch below, and correctly find no "/uploads/" marker in
            // them regardless — they just take the slower parse for no benefit, so
            // they are excluded up front instead.
            if (!Uri.TryCreate(src, UriKind.Absolute, out Uri? absolute))
                return null;
            path = absolute.AbsolutePath;
        }
        else
        {
            // Strip a query string or fragment from a relative URL; the stored
            // FilePath never has one.
            int cut = path.IndexOfAny(['?', '#']);
            if (cut >= 0) path = path[..cut];
        }

        // Cutting at the marker yields exactly the shape the media library stores
        // ("/uploads/images/…"), whichever host or prefix the author's URL carried.
        int markerIndex = path.IndexOf(UploadPathMarker, StringComparison.OrdinalIgnoreCase);
        return markerIndex >= 0 ? path[markerIndex..] : null;
    }

    /// <summary>
    /// Fills in alt text the media library already holds. A missing attribute means
    /// nobody has decided yet, so the library's answer is used; a present one —
    /// including an empty one, which marks a decorative image — is the author's
    /// decision and is left untouched.
    /// </summary>
    private static void ApplyAlt(IElement image, PublicMediaFile file)
    {
        if (image.HasAttribute("alt")) return;
        if (string.IsNullOrWhiteSpace(file.AltText)) return;

        image.SetAttribute("alt", file.AltText);
    }

    /// <summary>
    /// Emits the image's real pixel size so the browser can reserve the right space
    /// before the bytes arrive — this is what keeps a loading image from pushing the
    /// text below it down the page (Cumulative Layout Shift). Only set when neither
    /// dimension is present, so a half-specified pair is never completed into a
    /// wrong aspect ratio.
    /// </summary>
    private static void ApplyIntrinsicSize(IElement image, PublicMediaFile file)
    {
        if (file.Width is not > 0 || file.Height is not > 0) return;
        if (image.HasAttribute("width") || image.HasAttribute("height")) return;

        image.SetAttribute("width", file.Width.Value.ToString(CultureInfo.InvariantCulture));
        image.SetAttribute("height", file.Height.Value.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// The <c>&lt;img&gt;</c>'s own <c>srcset</c>: the fallback-format renditions
    /// (JPEG for a JPEG, PNG for a PNG) plus the stored image itself as the largest
    /// candidate. WebP never lands here — a browser that cannot read it would pick
    /// it; the WebP copies go into the <c>&lt;picture&gt;</c> source instead.
    /// </summary>
    private static void ApplySrcset(
        IElement image,
        PublicMediaFile file,
        IReadOnlyList<ImageRendition> renditions,
        Dictionary<string, int> cssWidthById)
    {
        if (image.HasAttribute("srcset") || renditions.Count == 0) return;

        List<(string Path, int Width)> candidates = [.. renditions
            .Where(r => r.FallbackPath is not null)
            .Select(r => (r.FallbackPath!, r.Width))];
        if (file.Width is > 0 && candidates.All(c => c.Width < file.Width.Value))
            candidates.Add((file.FilePath, file.Width.Value));

        // A WebP master has no fallback copies (they would be WebP too), so its
        // <img> keeps a single src; the <picture> source still gets the sizes below.
        if (candidates.Count >= 2)
        {
            image.SetAttribute("srcset", string.Join(", ", candidates.Select(c =>
                $"{c.Path} {c.Width.ToString(CultureInfo.InvariantCulture)}w")));
        }

        // Without `sizes`, the browser assumes the image spans the viewport and
        // always picks the largest file — which would make the srcset actively
        // harmful on a phone. When the page's own CSS gives this image a pixel
        // width, that is the answer; otherwise the honest upper bound: never wider
        // than the largest variant, and full-width below that.
        if (!image.HasAttribute("sizes"))
        {
            string? id = image.GetAttribute("id");
            if (id is not null && cssWidthById.TryGetValue(id, out int cssWidth))
            {
                string width = $"{cssWidth.ToString(CultureInfo.InvariantCulture)}px";
                // A page listing's card image is that width only on wider screens:
                // the block's own stylesheet stretches it to the full card below
                // 767px. Left at the pixel width, a phone picked a file half the
                // size it needed and showed the card image blurred.
                image.SetAttribute("sizes", image.Closest(ListingItemSelector) is not null
                    ? $"(max-width: {ListingFullWidthBelowPx}px) 100vw, {width}"
                    : width);
            }
            else
            {
                int largest = Math.Max(file.Width ?? 0, renditions[^1].Width);
                string largestText = largest.ToString(CultureInfo.InvariantCulture);
                image.SetAttribute("sizes", $"(max-width: {largestText}px) 100vw, {largestText}px");
            }
        }
    }
}
