namespace SharedKernel.Content;

/// <summary>
/// The site's default maintenance screen, as markup.
/// <para>
/// It lives here — rather than next to either consumer — because BOTH sides need
/// the identical screen and they cannot reference each other: the CMS seeder
/// writes it into the database as a normal, editable builder page, while the Web
/// app keeps it compiled in as the fallback shown when that page is unavailable
/// (unpublished, or the database itself is down — the very situation maintenance
/// mode usually accompanies). Two copies would silently drift, so visitors would
/// meet a different screen depending on which path served it.
/// </para>
/// <para>
/// The screen is deliberately WORDLESS apart from the brand: the mark's own gentle
/// breathing motion and a pulsing progress row carry the "we are working on it"
/// message, so it reads the same to a visitor who cannot read the site's language —
/// or cannot read at all. The only text is "Elevare".
/// </para>
/// </summary>
public static class MaintenancePageContent
{
    /// <summary>
    /// Site-relative path of the background photo, served from the Web app's
    /// <c>wwwroot</c>. Static files are served BEFORE the maintenance gate, so this
    /// still loads while the site is closed. If the file is absent the CSS falls
    /// back to a gradient in the same palette, so the screen never looks broken.
    /// </summary>
    public const string BackgroundImagePath = "/img/maintenance-bg.jpg";

    /// <summary>
    /// Site-relative path of the brand mark, white-on-transparent — the navy/green
    /// original is illegible against a dark photo, so a reversed variant lives here
    /// specifically for this screen (see also cms wwwroot's full-color one, used
    /// against light CMS chrome). Same resilience reasoning as the background photo:
    /// a plain file under wwwroot, not a Media Library upload, so it still loads
    /// with the database down.
    /// </summary>
    public const string MarkImagePath = "/img/elevare-mark-white.png";

    /// <summary>Brand wordmark — the single piece of visible text on the screen.</summary>
    public const string BrandName = "Elevare";

    /// <summary>
    /// Body markup with the compiled-in images — the Web app's fallback, which has to
    /// work with the database down, so it takes the photos from <c>wwwroot</c>.
    /// </summary>
    public static readonly string Html = HtmlWith(BackgroundImagePath, MarkImagePath);

    /// <summary>
    /// Body markup with the given images. The seeded builder page passes Media
    /// Library addresses: <c>/img/…</c> exists only on the public site, so in the
    /// CMS editor the photo removed itself and the mark showed a broken image,
    /// while an upload is served by both apps. A null background is left out and
    /// the gradient behind it shows instead.
    /// </summary>
    // CA1054: same as NotFoundPageContent.Html — an <img src> can be a bare
    // relative path, which System.Uri mishandles without a base.
#pragma warning disable CA1054
    public static string HtmlWith(string? backgroundUrl, string markUrl)
#pragma warning restore CA1054
    {
        string background = backgroundUrl is null
            ? string.Empty
            : $"""<img class="elv-mt-bg" src="{backgroundUrl}" alt="" loading="eager" onerror="this.remove()" />""";
        return $"""
            <div class="elv-mt">
              {background}
              <div class="elv-mt-scrim"></div>
              <main class="elv-mt-panel">
                <img class="elv-mt-mark" src="{markUrl}" alt="" />
                <h1 class="elv-mt-brand">{BrandName}</h1>
                <div class="elv-mt-dots" aria-hidden="true"><span></span><span></span><span></span></div>
              </main>
            </div>
            """;
    }

    /// <summary>
    /// Styles for <see cref="Html"/>. Everything is scoped under <c>.elv-mt</c> so
    /// dropping this into a builder page cannot collide with the page's own rules.
    /// </summary>
    public const string Css =
        """
        .elv-mt{position:fixed;inset:0;display:flex;align-items:center;justify-content:center;
          /* Gradient is the same dark navy the rest of the brand uses (see app.css's
             --cms-bg/--cms-sidebar), so a missing photo still looks like Elevare and
             not like a random stock-photo placeholder — the photo is a real <img>
             now (see Html), and its onerror handler removes itself on a 404,
             uncovering this gradient rather than leaving a broken-image icon. */
          background:linear-gradient(180deg,#1a2c4d 0%,#101c33 55%,#0b111d 100%);
          overflow:hidden;font-family:system-ui,-apple-system,'Segoe UI','Helvetica Neue',Arial,sans-serif}
        .elv-mt-bg{position:absolute;inset:0;width:100%;height:100%;object-fit:cover}
        .elv-mt-scrim{position:absolute;inset:0;background:radial-gradient(ellipse at 50% 45%,rgba(8,20,45,.34) 0%,rgba(8,20,45,.52) 55%,rgba(6,15,35,.68) 100%)}
        .elv-mt-panel{position:relative;display:flex;flex-direction:column;align-items:center;gap:22px;padding:44px 56px;text-align:center}
        .elv-mt-mark{width:104px;height:auto;filter:drop-shadow(0 4px 14px rgba(0,0,0,.42));animation:elv-mt-breathe 2.6s ease-in-out infinite}
        @keyframes elv-mt-breathe{0%,100%{opacity:.92;transform:scale(1)}50%{opacity:1;transform:scale(1.04)}}
        .elv-mt-brand{margin:0;color:#fff;font-size:clamp(2.4rem,7vw,4.2rem);font-weight:300;letter-spacing:.22em;
          text-indent:.22em;line-height:1;text-shadow:0 3px 22px rgba(0,0,0,.5)}
        .elv-mt-dots{display:flex;gap:11px}
        .elv-mt-dots span{width:11px;height:11px;border-radius:50%;background:#fff;opacity:.45;
          box-shadow:0 2px 8px rgba(0,0,0,.35);animation:elv-mt-pulse 1.4s ease-in-out infinite}
        .elv-mt-dots span:nth-child(2){animation-delay:.2s}
        .elv-mt-dots span:nth-child(3){animation-delay:.4s}
        @keyframes elv-mt-pulse{0%,80%,100%{opacity:.35;transform:scale(.8)}40%{opacity:1;transform:scale(1.15)}}
        @media (max-width:520px){.elv-mt-panel{padding:32px 24px;gap:18px}.elv-mt-mark{width:80px}}
        /* Motion can trigger nausea/migraine for some visitors; the screen still reads
           without it, so honour the OS-level preference and hold everything still. */
        @media (prefers-reduced-motion:reduce){
          .elv-mt-mark,.elv-mt-dots span{animation:none}
          .elv-mt-dots span{opacity:.75}}
        """;

    /// <summary>
    /// Wraps <see cref="Html"/> and <see cref="Css"/> into a complete, standalone
    /// document — used for the compiled-in fallback, which has no layout to sit in.
    /// </summary>
    /// <param name="languageCode">Value for the document's <c>lang</c> attribute.</param>
    // Doubled '$' so the CSS below can use bare braces: in a raw string literal the
    // usual "{{" escape does not apply, the number of '$' decides how many braces
    // open an interpolation instead.
    public static string BuildDocument(string languageCode = "tr") =>
        $$"""
        <!DOCTYPE html>
        <html lang="{{languageCode}}">
        <head>
            <meta charset="utf-8" />
            <meta name="viewport" content="width=device-width, initial-scale=1.0" />
            <meta name="robots" content="noindex" />
            <title>{{BrandName}}</title>
            <style>*,*::before,*::after{box-sizing:border-box}html,body{margin:0;padding:0;height:100%}
        {{Css}}</style>
        </head>
        <body>
        {{Html}}
        </body>
        </html>
        """;
}
