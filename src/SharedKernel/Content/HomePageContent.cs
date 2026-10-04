namespace SharedKernel.Content;

/// <summary>
/// The starter homepage a fresh install ships with — what a new site owner sees at
/// "/" before they have built anything of their own. Seeded as an ordinary,
/// editable builder page (see <c>DatabaseSeeder</c>), one per active language, so it
/// can simply be replaced with real content once the site has some.
/// <para>
/// Unlike <see cref="MaintenancePageContent"/> this has no compiled-in Web-side
/// fallback: the homepage is just the page whose slug is empty, and if none exists
/// the visitor gets an ordinary 404 — there is nothing here that needs to survive a
/// database outage.
/// </para>
/// </summary>
public static class HomePageContent
{
    /// <summary>
    /// The photo does not exist until the seeder has uploaded it, and the upload
    /// itself can fail (see <c>DatabaseSeeder.SeedTranslatedSystemPageAsync</c>) —
    /// so the URL is a parameter, not a compile-time constant. Null omits the
    /// <c>&lt;img&gt;</c> entirely rather than pointing it at nothing:
    /// <see cref="Css"/>'s own <c>background-color</c> is the fallback, the same
    /// way it always was before this had a real image element.
    /// </summary>
    // CA1054 asks for a Uri-typed parameter, but this is an <img src> value — it can
    // be a bare relative path, which System.Uri mishandles without a base.
#pragma warning disable CA1054
    public static string Html(string languageCode, string? imageUrl) =>
        languageCode == "en" ? BuildHtml(EyebrowEn, HeadingEn, BodyEn, imageUrl) : BuildHtml(EyebrowTr, HeadingTr, BodyTr, imageUrl);
#pragma warning restore CA1054

    private const string EyebrowTr = "Elevare ile hazırlandı";
    private const string HeadingTr = "Siteniz hazır.";
    private const string BodyTr = "Bu, düzenleyebileceğiniz bir taslak ana sayfa. İçeriğinizi eklemek için Sayfalar'dan bu sayfayı açın.";

    private const string EyebrowEn = "Built with Elevare";
    private const string HeadingEn = "Your site is ready.";
    private const string BodyEn = "This is a starter homepage you can edit. Open it from Pages to add your own content.";

    private static string BuildHtml(string eyebrow, string heading, string body, string? imageUrl)
    {
        // Eager: this is the single largest image on the homepage, sitting behind
        // everything else at first paint — and an eager image is fetched at high
        // priority (ResponsiveImageResolutionService).
        string img = imageUrl is null
            ? string.Empty
            : $"""<img class="elv-home-bg" src="{imageUrl}" alt="" loading="eager" />""";

        return $"""
            <section class="elv-home">
              {img}
              <div class="elv-home-scrim"></div>
              <div class="elv-home-panel">
                <span class="elv-home-eyebrow">{eyebrow}</span>
                <h1>{heading}</h1>
                <p>{body}</p>
              </div>
            </section>
            """;
    }

    /// <summary>
    /// Scoped under <c>.elv-home</c> so it cannot collide with the rest of the site's
    /// CSS once the operator starts adding their own blocks around it.
    /// </summary>
    public const string Css =
        """
        .elv-home{position:relative;width:100%;min-height:72vh;display:flex;align-items:center;
          /* Shown instantly, before the <img> below has a chance to load or if
             imageUrl was null (see BuildHtml) — never a bare white flash. */
          background-color:#0b111d;overflow:hidden;
          font-family:system-ui,-apple-system,'Segoe UI',Roboto,'Helvetica Neue',Arial,sans-serif}
        .elv-home-bg{position:absolute;inset:0;width:100%;height:100%;object-fit:cover}
        .elv-home-scrim{position:absolute;inset:0;background:linear-gradient(180deg,rgba(8,15,30,.25) 0%,rgba(8,15,30,.62) 100%)}
        .elv-home-panel{position:relative;max-width:640px;margin:0 auto;padding:48px 24px;text-align:center;color:#fff}
        .elv-home-eyebrow{display:inline-block;padding:5px 14px;border:1px solid rgba(255,255,255,.35);border-radius:999px;
          font-size:12px;font-weight:600;letter-spacing:.06em;text-transform:uppercase;margin-bottom:18px}
        .elv-home-panel h1{margin:0 0 12px;font-size:clamp(2rem,5vw,3rem);font-weight:700;text-shadow:0 2px 16px rgba(0,0,0,.4)}
        .elv-home-panel p{margin:0;font-size:16px;color:rgba(255,255,255,.88);line-height:1.6}
        @media (max-width:520px){.elv-home{min-height:56vh}.elv-home-panel{padding:32px 20px}}
        """;
}
