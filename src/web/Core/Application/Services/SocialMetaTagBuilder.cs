using System.Globalization;
using SharedKernel.Social;

namespace Application.Services;
/// <summary>
/// Turns a page's social metadata into the exact list of Open Graph and X Card tags
/// the public page emits, in order. Pure and testable — the view only loops.
/// <para>
/// Two rules shape it. Type-specific properties are written only for the type the
/// page declares: <c>article:*</c> on an article, <c>profile:*</c> on a profile, and
/// so on, because a reader takes <c>og:type</c> as the schema and ignores — or
/// flags — properties that do not belong to it. And nothing is filled in: every tag
/// carries a field the page itself set, an empty field writes no tag (X reads the
/// og: tags when its own are absent). Suggestions are the CMS panel's job — its
/// "Otomatik doldur" — so the panel and the page never disagree.
/// </para>
/// </summary>
public static class SocialMetaTagBuilder
{
    private const int TwitterImageAltMaxLength = 420;

    public static IReadOnlyList<SocialMetaTag> Build(SocialTagInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        SocialMeta s = input.Social;
        List<SocialMetaTag> tags = [];
        void Og(string key, string? value) { if (!string.IsNullOrWhiteSpace(value)) tags.Add(new SocialMetaTag("property", key, value.Trim())); }
        void OgInt(string key, int? value) { if (value is > 0) tags.Add(new SocialMetaTag("property", key, value.Value.ToString(CultureInfo.InvariantCulture))); }
        void OgDate(string key, DateTime? value) { if (value.HasValue) tags.Add(new SocialMetaTag("property", key, Iso(value.Value))); }
        void OgList(string key, IEnumerable<string> values) { foreach (string v in values) Og(key, v); }
        void Tw(string key, string? value) { if (!string.IsNullOrWhiteSpace(value)) tags.Add(new SocialMetaTag("name", key, value.Trim())); }
        void TwInt(string key, int? value) { if (value is > 0) tags.Add(new SocialMetaTag("name", key, value.Value.ToString(CultureInfo.InvariantCulture))); }

        string type = string.IsNullOrWhiteSpace(input.Type) ? "website" : input.Type.Trim();

        // ── Open Graph core ──────────────────────────────────────────────────
        Og("og:locale", string.IsNullOrWhiteSpace(s.Locale) ? input.Locale : s.Locale);
        foreach (string alt in input.AlternateLocales) Og("og:locale:alternate", alt);
        Og("og:type", type);
        Og("og:title", input.Title);
        Og("og:description", input.Description);
        Og("og:determiner", s.Determiner);
        Og("og:url", input.Link);

        if (!string.IsNullOrWhiteSpace(input.Image))
        {
            Og("og:image", input.Image);
            if (input.Image.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) Og("og:image:secure_url", input.Image);
            Og("og:image:type", s.ImageType);
            OgInt("og:image:width", s.ImageWidth);
            OgInt("og:image:height", s.ImageHeight);
            Og("og:image:alt", s.ImageAlt);
        }
        if (!string.IsNullOrWhiteSpace(s.Video))
        {
            Og("og:video", s.Video);
            if (s.Video.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) Og("og:video:secure_url", s.Video);
            Og("og:video:type", s.VideoType);
            OgInt("og:video:width", s.VideoWidth);
            OgInt("og:video:height", s.VideoHeight);
        }
        if (!string.IsNullOrWhiteSpace(s.Audio))
        {
            Og("og:audio", s.Audio);
            if (s.Audio.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) Og("og:audio:secure_url", s.Audio);
            Og("og:audio:type", s.AudioType);
        }

        // ── Type-specific ────────────────────────────────────────────────────
        if (type == "article")
        {
            OgDate("article:published_time", s.ArticlePublishedTime);
            OgDate("article:modified_time", s.ArticleModifiedTime);
            OgDate("article:expiration_time", s.ArticleExpirationTime);
            OgList("article:author", s.ArticleAuthors);
            Og("article:section", s.ArticleSection);
            OgList("article:tag", s.ArticleTags);
            Og("article:publisher", s.ArticlePublisher);
        }
        else if (type == "profile")
        {
            Og("profile:first_name", s.ProfileFirstName);
            Og("profile:last_name", s.ProfileLastName);
            Og("profile:username", s.ProfileUsername);
            if (s.ProfileGender is "male" or "female") Og("profile:gender", s.ProfileGender);
        }
        else if (type == "book")
        {
            OgList("book:author", s.BookAuthors);
            Og("book:isbn", s.BookIsbn);
            OgDate("book:release_date", s.BookReleaseDate);
            OgList("book:tag", s.BookTags);
        }
        else if (type.StartsWith("video.", StringComparison.Ordinal))
        {
            foreach (SocialPerson actor in s.VideoActors)
            {
                if (string.IsNullOrWhiteSpace(actor.Url)) continue;
                Og("video:actor", actor.Url);
                // The role belongs to the actor written just before it — order matters.
                Og("video:actor:role", actor.Role);
            }
            OgList("video:director", s.VideoDirectors);
            OgList("video:writer", s.VideoWriters);
            OgInt("video:duration", s.VideoDuration);
            OgDate("video:release_date", s.VideoReleaseDate);
            OgList("video:tag", s.VideoTags);
            if (type == "video.episode") Og("video:series", s.VideoSeries);
        }
        else if (type == "music.song")
        {
            OgInt("music:duration", s.MusicDuration);
            Og("music:album", s.MusicAlbum);
            OgInt("music:album:disc", s.MusicAlbumDisc);
            OgInt("music:album:track", s.MusicAlbumTrack);
            OgList("music:musician", s.MusicMusicians);
        }
        else if (type == "music.album")
        {
            OgList("music:song", s.MusicSongs);
            OgInt("music:song:disc", s.MusicAlbumDisc);
            OgInt("music:song:track", s.MusicAlbumTrack);
            OgList("music:musician", s.MusicMusicians);
            OgDate("music:release_date", s.MusicReleaseDate);
        }
        else if (type == "music.playlist")
        {
            OgList("music:song", s.MusicSongs);
            Og("music:creator", s.MusicCreator);
        }
        else if (type == "music.radio_station")
        {
            Og("music:creator", s.MusicCreator);
        }

        // ── Facebook ─────────────────────────────────────────────────────────
        Og("fb:app_id", s.FacebookAppId);

        // ── X / Twitter ──────────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(input.TwitterCard))
        {
            string card = input.TwitterCard.Trim();
            Tw("twitter:card", card);
            Tw("twitter:title", s.TwitterTitle);
            Tw("twitter:description", s.TwitterDescription);
            string? image = string.IsNullOrWhiteSpace(s.TwitterImage) ? null : s.TwitterImage.Trim();
            Tw("twitter:image", image);
            if (image is not null)
            {
                string? alt = string.IsNullOrWhiteSpace(s.TwitterImageAlt) ? null : s.TwitterImageAlt.Trim();
                if (alt is not null && alt.Length > TwitterImageAltMaxLength) alt = alt[..TwitterImageAltMaxLength];
                Tw("twitter:image:alt", alt);
            }
            if (card == "player")
            {
                Tw("twitter:player", s.PlayerUrl);
                TwInt("twitter:player:width", s.PlayerWidth);
                TwInt("twitter:player:height", s.PlayerHeight);
                Tw("twitter:player:stream", s.PlayerStream);
            }
            else if (card == "app")
            {
                Tw("twitter:app:name:iphone", s.AppNameIphone);
                Tw("twitter:app:id:iphone", s.AppIdIphone);
                Tw("twitter:app:url:iphone", s.AppUrlIphone);
                Tw("twitter:app:name:ipad", s.AppNameIpad);
                Tw("twitter:app:id:ipad", s.AppIdIpad);
                Tw("twitter:app:url:ipad", s.AppUrlIpad);
                Tw("twitter:app:name:googleplay", s.AppNameGooglePlay);
                Tw("twitter:app:id:googleplay", s.AppIdGooglePlay);
                Tw("twitter:app:url:googleplay", s.AppUrlGooglePlay);
                Tw("twitter:app:country", s.AppCountry);
            }
        }
        // Attribution is worth having even without a card: readers that build a
        // card from Open Graph alone still credit the site and the author.
        Tw("twitter:site", input.TwitterSite);
        Tw("twitter:creator", s.TwitterCreator);

        return tags;
    }

    // ISO 8601 as the protocol asks; an offset only when the value carries one
    // (dates typed into the editor are local wall-clock times with no zone).
    private static string Iso(DateTime value) =>
        value.Kind == DateTimeKind.Unspecified
            ? value.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture)
            : value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
