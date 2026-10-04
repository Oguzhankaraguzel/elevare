namespace SharedKernel.Content;

/// <summary>
/// The site's default 500 screen, seeded as an ordinary editable builder page with
/// the reserved slug <c>500</c> (see <c>DatabaseSeeder</c>). Same fallback story as
/// <see cref="NotFoundPageContent"/>: only rendered when a published page exists,
/// otherwise <c>GlobalExceptionHandlerMiddleware</c> shows the plain text in
/// <c>Views/Shared/Error.cshtml</c>, on purpose — that path exists for when the
/// database itself is down, which is exactly when this page cannot be fetched
/// anyway.
/// </summary>
public static class ServerErrorPageContent
{
    /// <summary>
    /// The URL is a parameter rather than a compile-time constant for the same
    /// reason as <see cref="HomePageContent.Html"/>: the photo does not exist until
    /// the seeder uploads it, and that upload can fail. Null omits the
    /// <c>&lt;img&gt;</c> entirely — <see cref="Css"/>'s <c>background-color</c>
    /// carries the screen either way.
    /// </summary>
    // CA1054 asks for a Uri-typed parameter, but this is an <img src> value — it can
    // be a bare relative path, which System.Uri mishandles without a base.
#pragma warning disable CA1054
    public static string Html(string languageCode, string? imageUrl) =>
        languageCode == "en" ? BuildHtml(BodyEn, LinkEn, imageUrl) : BuildHtml(BodyTr, LinkTr, imageUrl);
#pragma warning restore CA1054

    private const string BodyTr = "Beklenmeyen bir hata oluştu. Lütfen daha sonra tekrar deneyin.";
    private const string LinkTr = "Ana sayfaya dön";

    private const string BodyEn = "Something went wrong. Please try again later.";
    private const string LinkEn = "Back to homepage";

    private static string BuildHtml(string body, string link, string? imageUrl)
    {
        // Eager: the photo fills the viewport behind this content, so it is the
        // page's LCP candidate — see HomePageContent.BuildHtml for the same call.
        string img = imageUrl is null
            ? string.Empty
            : $"""<img class="elv-sys-bg" src="{imageUrl}" alt="" loading="eager" />""";

        return $"""
            <section class="elv-sys elv-sys-500">
              {img}
              <div class="elv-sys-scrim"></div>
              <div class="elv-sys-panel">
                <span class="elv-sys-code">500</span>
                <p>{body}</p>
                <a class="elv-sys-link" href="/">{link}</a>
              </div>
            </section>
            """;
    }

    /// <summary>
    /// Layout is identical to <see cref="NotFoundPageContent"/>'s <c>.elv-sys</c>
    /// base (both seeded independently, so each carries its own full copy rather
    /// than depending on the other's CSS being present) — only the background photo
    /// and copy differ.
    /// </summary>
    public const string Css =
        """
        .elv-sys{position:relative;width:100%;min-height:72vh;display:flex;align-items:center;justify-content:center;
          background-color:#0b111d;overflow:hidden;text-align:center;
          font-family:system-ui,-apple-system,'Segoe UI',Roboto,'Helvetica Neue',Arial,sans-serif}
        .elv-sys-bg{position:absolute;inset:0;width:100%;height:100%;object-fit:cover}
        .elv-sys-scrim{position:absolute;inset:0;background:radial-gradient(ellipse at 50% 40%,rgba(8,15,30,.35) 0%,rgba(8,15,30,.72) 100%)}
        .elv-sys-panel{position:relative;padding:48px 24px;color:#fff}
        .elv-sys-code{display:block;font-size:clamp(3.5rem,10vw,6rem);font-weight:700;line-height:1;
          letter-spacing:-.02em;text-shadow:0 3px 20px rgba(0,0,0,.45)}
        .elv-sys-panel p{margin:12px 0 22px;font-size:17px;color:rgba(255,255,255,.9)}
        .elv-sys-link{display:inline-block;padding:11px 26px;border:1px solid rgba(255,255,255,.55);border-radius:4px;
          color:#fff;font-weight:600;font-size:14px;text-decoration:none;transition:background .15s ease}
        .elv-sys-link:hover{background:rgba(255,255,255,.12)}
        @media (max-width:520px){.elv-sys{min-height:56vh}.elv-sys-panel{padding:32px 20px}}
        """;
}
