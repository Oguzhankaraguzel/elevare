using System.Text.RegularExpressions;
using Domain.Entities.SiteCodeSnippets;
using SharedKernel.Concrete;

namespace Application.Security;

/// <summary>
/// Turns whatever a vendor's setup page handed the author into correct markup.
/// <para>
/// Google shows a measurement id in one place and a copy-paste block in another;
/// people paste whichever they found. Asking for exactly one of the two guarantees
/// a support ticket, so each preset accepts both: it pulls the id out of a pasted
/// block, or takes a bare id, and always emits the same known-good snippet. The
/// snippet is generated rather than stored verbatim so a vendor changing their
/// boilerplate does not leave every existing site on the old version.
/// </para>
/// </summary>
public static partial class SiteCodePresetFactory
{
    /// <summary>
    /// Builds the snippet(s) for <paramref name="preset"/> from raw author input.
    /// <para>
    /// Returns more than one part when the vendor's tag genuinely has more than one
    /// half — Google Tag Manager needs a head script <em>and</em> a body noscript,
    /// and the noscript is the half people forget, which silently breaks measurement
    /// for visitors without JavaScript.
    /// </para>
    /// </summary>
    public static Result<IReadOnlyList<SiteCodePart>> Build(SiteCodePreset preset, string? rawInput)
    {
        string input = (rawInput ?? string.Empty).Trim();

        if (input.Length == 0)
            return Result.Failure<IReadOnlyList<SiteCodePart>>(SiteCodeSnippetErrors.ContentRequired);

        return preset switch
        {
            SiteCodePreset.GoogleAnalytics4 => BuildGa4(input),
            SiteCodePreset.GoogleTagManager => BuildGtm(input),
            SiteCodePreset.MetaPixel => BuildMetaPixel(input),
            SiteCodePreset.SearchConsoleVerification => BuildVerification(input),

            // Consent platforms have no common shape — every vendor ships something
            // different — so the script is kept as pasted. What the preset
            // contributes is the placement (pinned to the top of <head>, ahead of
            // every tag it is supposed to be able to block) and, see BuildConsent,
            // a stylesheet that no longer blocks first paint.
            SiteCodePreset.CookieConsent => BuildConsent(input),

            _ => Result.Failure<IReadOnlyList<SiteCodePart>>(SiteCodeSnippetErrors.PresetValueUnrecognised),
        };
    }

    private static Result<IReadOnlyList<SiteCodePart>> BuildGa4(string input)
    {
        Match match = Ga4IdRegex().Match(input);
        if (!match.Success)
            return Result.Failure<IReadOnlyList<SiteCodePart>>(SiteCodeSnippetErrors.PresetValueUnrecognised);

        string id = match.Value.ToUpperInvariant();

        // $$ so a single brace is literal JavaScript and {{id}} is the substitution —
        // raw string literals do not accept the {{ escape that ordinary ones do.
        string content =
            $$"""
            <script async src="https://www.googletagmanager.com/gtag/js?id={{id}}"></script>
            <script>
              window.dataLayer = window.dataLayer || [];
              function gtag(){dataLayer.push(arguments);}
              gtag('js', new Date());
              gtag('config', '{{id}}');
            </script>
            """;

        return Result.Success<IReadOnlyList<SiteCodePart>>(
            [new SiteCodePart(SiteCodeKind.Script, SiteCodePlacement.HeadEnd, content, Suffix: null)]);
    }

    private static Result<IReadOnlyList<SiteCodePart>> BuildGtm(string input)
    {
        Match match = GtmIdRegex().Match(input);
        if (!match.Success)
            return Result.Failure<IReadOnlyList<SiteCodePart>>(SiteCodeSnippetErrors.PresetValueUnrecognised);

        string id = match.Value.ToUpperInvariant();

        string head =
            $$"""
            <script>
            (function(w,d,s,l,i){w[l]=w[l]||[];w[l].push({'gtm.start':new Date().getTime(),event:'gtm.js'});
            var f=d.getElementsByTagName(s)[0],j=d.createElement(s),dl=l!='dataLayer'?'&l='+l:'';
            j.async=true;j.src='https://www.googletagmanager.com/gtm.js?id='+i+dl;f.parentNode.insertBefore(j,f);
            })(window,document,'script','dataLayer','{{id}}');
            </script>
            """;

        string body =
            $"""
            <noscript><iframe src="https://www.googletagmanager.com/ns.html?id={id}"
            height="0" width="0" style="display:none;visibility:hidden"></iframe></noscript>
            """;

        return Result.Success<IReadOnlyList<SiteCodePart>>(
        [
            new SiteCodePart(SiteCodeKind.Script, SiteCodePlacement.HeadStart, head, Suffix: null),
            new SiteCodePart(SiteCodeKind.RawHtml, SiteCodePlacement.BodyStart, body, Suffix: "noscript"),
        ]);
    }

    private static Result<IReadOnlyList<SiteCodePart>> BuildMetaPixel(string input)
    {
        Match match = PixelIdRegex().Match(input);
        if (!match.Success)
            return Result.Failure<IReadOnlyList<SiteCodePart>>(SiteCodeSnippetErrors.PresetValueUnrecognised);

        string id = match.Value;
        string content =
            $$"""
            <script>
            !function(f,b,e,v,n,t,s){if(f.fbq)return;n=f.fbq=function(){n.callMethod?
            n.callMethod.apply(n,arguments):n.queue.push(arguments)};if(!f._fbq)f._fbq=n;
            n.push=n;n.loaded=!0;n.version='2.0';n.queue=[];t=b.createElement(e);t.async=!0;
            t.src=v;s=b.getElementsByTagName(e)[0];s.parentNode.insertBefore(t,s)}
            (window,document,'script','https://connect.facebook.net/en_US/fbevents.js');
            fbq('init', '{{id}}');
            fbq('track', 'PageView');
            </script>
            """;

        return Result.Success<IReadOnlyList<SiteCodePart>>(
            [new SiteCodePart(SiteCodeKind.Script, SiteCodePlacement.HeadEnd, content, Suffix: null)]);
    }

    private static Result<IReadOnlyList<SiteCodePart>> BuildVerification(string input)
    {
        // Either the whole <meta ... content="TOKEN"> or just the token.
        Match fromTag = VerificationContentRegex().Match(input);
        string token = fromTag.Success ? fromTag.Groups["token"].Value : input;

        // Tokens are opaque, but they are always a single word — anything with
        // whitespace or angle brackets is markup we failed to read, not a token.
        if (token.Length == 0 || token.Any(char.IsWhiteSpace) || token.Contains('<', StringComparison.Ordinal))
            return Result.Failure<IReadOnlyList<SiteCodePart>>(SiteCodeSnippetErrors.PresetValueUnrecognised);

        string content = $"""<meta name="google-site-verification" content="{token}" />""";

        return Result.Success<IReadOnlyList<SiteCodePart>>(
            [new SiteCodePart(SiteCodeKind.MetaTag, SiteCodePlacement.HeadStart, content, Suffix: null)]);
    }

    /// <summary>
    /// A consent vendor's paste is usually two things: the script, and a stylesheet
    /// for the banner. They become two rows, like Google Tag Manager's two halves.
    /// <para>
    /// The stylesheet is rewritten to load without blocking — nothing on the page
    /// waits for the banner's styling, yet as a plain link at the top of the head it
    /// held first paint until a third-party CDN answered (Lighthouse measured 240 ms
    /// of it on the live homepage): print media until it has arrived, then applied,
    /// with a &lt;noscript&gt; copy for browsers without JavaScript. Every external
    /// origin in the paste gets a preconnect ahead of the script, so the connection
    /// is open by the time the script tag is reached.
    /// </para>
    /// <para>
    /// The SCRIPT itself is left exactly as pasted. A consent tool that blocks
    /// trackers until the visitor agrees does so by running before them,
    /// synchronously; a defer here would silently turn it into a banner that blocks
    /// nothing.
    /// </para>
    /// </summary>
    private static Result<IReadOnlyList<SiteCodePart>> BuildConsent(string input)
    {
        List<string> stylesheets = [.. StylesheetLinkRegex().Matches(input).Select(m => m.Groups["href"].Value)];
        string script = StylesheetLinkRegex().Replace(input, "").Trim();

        HashSet<string> origins = new(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in ExternalUrlRegex().Matches(input))
        {
            if (Uri.TryCreate(m.Groups["url"].Value, UriKind.Absolute, out Uri? uri))
                origins.Add(uri.GetLeftPart(UriPartial.Authority));
        }
        string preconnects = string.Concat(origins.Select(o => "<link rel=\"preconnect\" href=\"" + o + "\" crossorigin>" + Environment.NewLine));

        List<SiteCodePart> parts = [];
        if (script.Length > 0)
            parts.Add(new SiteCodePart(SiteCodeKind.Script, SiteCodePlacement.HeadStart, preconnects + script, Suffix: null));
        if (stylesheets.Count > 0)
        {
            string css = string.Join(Environment.NewLine, stylesheets.Select(href =>
                "<link rel=\"stylesheet\" href=\"" + href + "\" media=\"print\" onload=\"this.media='all'\">"
                + "<noscript><link rel=\"stylesheet\" href=\"" + href + "\"></noscript>"));
            parts.Add(new SiteCodePart(SiteCodeKind.Style, SiteCodePlacement.HeadStart, css, Suffix: "css"));
        }

        return parts.Count == 0
            ? Result.Failure<IReadOnlyList<SiteCodePart>>(SiteCodeSnippetErrors.PresetValueUnrecognised)
            : Result.Success<IReadOnlyList<SiteCodePart>>(parts);
    }

    [GeneratedRegex("""<link\b(?=[^>]*\brel\s*=\s*["']stylesheet["'])[^>]*\bhref\s*=\s*["'](?<href>https?://[^"']+)["'][^>]*/?>""", RegexOptions.IgnoreCase)]
    private static partial Regex StylesheetLinkRegex();

    [GeneratedRegex("""(?:src|href)\s*=\s*["'](?<url>https?://[^"']+)["']""", RegexOptions.IgnoreCase)]
    private static partial Regex ExternalUrlRegex();

    [GeneratedRegex(@"G-[A-Z0-9]{4,}", RegexOptions.IgnoreCase)]
    private static partial Regex Ga4IdRegex();

    [GeneratedRegex(@"GTM-[A-Z0-9]{4,}", RegexOptions.IgnoreCase)]
    private static partial Regex GtmIdRegex();

    /// <summary>Pixel ids are long digit strings; <c>fbq('init', '...')</c> is where they appear in a pasted block.</summary>
    [GeneratedRegex(@"\d{10,20}")]
    private static partial Regex PixelIdRegex();

    [GeneratedRegex("""content\s*=\s*["'](?<token>[^"']+)["']""", RegexOptions.IgnoreCase)]
    private static partial Regex VerificationContentRegex();
}
