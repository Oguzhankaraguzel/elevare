using System.Text.Json;
using System.Text.Json.Serialization;

namespace SharedKernel.Social;

/// <summary>
/// Everything a page can say about itself to Facebook, LinkedIn, X and the other
/// link-preview readers beyond the five core Open Graph fields the page keeps in
/// its own columns (og:title/description/type/image/url, twitter:card/site).
/// <para>
/// Stored as one JSON column (<c>SeoSocialJson</c>) rather than fifty columns: the
/// set is wide, most of it is type-specific — an article's authors mean nothing on
/// a profile, a player card's stream nothing on a summary card — and nothing ever
/// queries it; the public site reads it back whole and writes tags. Shared between
/// both applications so the two never disagree about what a field is called.
/// </para>
/// <para>
/// Property names follow the Open Graph protocol (ogp.me) and X's card markup:
/// <c>og:image:alt</c> is <see cref="ImageAlt"/>, <c>article:published_time</c> is
/// <see cref="ArticlePublishedTime"/>, <c>twitter:app:id:iphone</c> is
/// <see cref="AppIdIphone"/>. Lists are the array-valued properties (several
/// <c>article:tag</c> tags, several <c>book:author</c> profiles).
/// </para>
/// </summary>
public sealed class SocialMeta
{
    // ── Open Graph, any type ─────────────────────────────────────────────────
    /// <summary><c>og:site_name</c> for this page only; the layout's site name is the default.</summary>
    public string? SiteName { get; set; }
    /// <summary><c>og:determiner</c>: "a", "an", "the", "" or "auto".</summary>
    public string? Determiner { get; set; }
    /// <summary><c>og:locale</c> override (xx_XX); derived from the page's language when empty.</summary>
    public string? Locale { get; set; }

    /// <summary><c>og:image:alt</c> — also used for <c>twitter:image:alt</c> unless that is set.</summary>
    public string? ImageAlt { get; set; }
    public int? ImageWidth { get; set; }
    public int? ImageHeight { get; set; }
    /// <summary><c>og:image:type</c>, a MIME type such as image/jpeg.</summary>
    public string? ImageType { get; set; }

    /// <summary><c>og:video</c> — a video file or player URL that goes with the page.</summary>
    public string? Video { get; set; }
    public string? VideoType { get; set; }
    public int? VideoWidth { get; set; }
    public int? VideoHeight { get; set; }
    /// <summary><c>og:audio</c>.</summary>
    public string? Audio { get; set; }
    public string? AudioType { get; set; }

    // ── og:type = article ────────────────────────────────────────────────────
    public DateTime? ArticlePublishedTime { get; set; }
    public DateTime? ArticleModifiedTime { get; set; }
    public DateTime? ArticleExpirationTime { get; set; }
    /// <summary><c>article:author</c> — profile URLs, one per entry.</summary>
    public List<string> ArticleAuthors { get; set; } = [];
    public string? ArticleSection { get; set; }
    public List<string> ArticleTags { get; set; } = [];
    /// <summary><c>article:publisher</c> — the publisher's Facebook Page URL (a Facebook extension, widely read).</summary>
    public string? ArticlePublisher { get; set; }

    // ── og:type = profile ────────────────────────────────────────────────────
    public string? ProfileFirstName { get; set; }
    public string? ProfileLastName { get; set; }
    public string? ProfileUsername { get; set; }
    /// <summary><c>profile:gender</c>: "male" or "female" — the protocol's own enum.</summary>
    public string? ProfileGender { get; set; }

    // ── og:type = book ───────────────────────────────────────────────────────
    public List<string> BookAuthors { get; set; } = [];
    public string? BookIsbn { get; set; }
    public DateTime? BookReleaseDate { get; set; }
    public List<string> BookTags { get; set; } = [];

    // ── og:type = video.movie / video.episode / video.tv_show / video.other ──
    public List<SocialPerson> VideoActors { get; set; } = [];
    public List<string> VideoDirectors { get; set; } = [];
    public List<string> VideoWriters { get; set; } = [];
    /// <summary><c>video:duration</c> in seconds.</summary>
    public int? VideoDuration { get; set; }
    public DateTime? VideoReleaseDate { get; set; }
    public List<string> VideoTags { get; set; } = [];
    /// <summary><c>video:series</c> — the tv_show URL an episode belongs to.</summary>
    public string? VideoSeries { get; set; }

    // ── og:type = music.song / music.album / music.playlist / music.radio_station
    /// <summary><c>music:duration</c> in seconds (song).</summary>
    public int? MusicDuration { get; set; }
    /// <summary><c>music:album</c> URL (song) — or, for an album, its <c>music:song</c> URLs live in <see cref="MusicSongs"/>.</summary>
    public string? MusicAlbum { get; set; }
    public int? MusicAlbumDisc { get; set; }
    public int? MusicAlbumTrack { get; set; }
    public List<string> MusicSongs { get; set; } = [];
    /// <summary><c>music:musician</c> profile URLs.</summary>
    public List<string> MusicMusicians { get; set; } = [];
    /// <summary><c>music:creator</c> profile URL (playlist, radio station).</summary>
    public string? MusicCreator { get; set; }
    public DateTime? MusicReleaseDate { get; set; }

    // ── Facebook ─────────────────────────────────────────────────────────────
    /// <summary><c>fb:app_id</c> — ties the page to a Facebook app for Insights.</summary>
    public string? FacebookAppId { get; set; }

    // ── X / Twitter ──────────────────────────────────────────────────────────
    /// <summary><c>twitter:creator</c> — the author's @handle.</summary>
    public string? TwitterCreator { get; set; }
    /// <summary><c>twitter:title</c> override; X falls back to og:title on its own, other readers do not.</summary>
    public string? TwitterTitle { get; set; }
    public string? TwitterDescription { get; set; }
    public string? TwitterImage { get; set; }
    /// <summary><c>twitter:image:alt</c>, up to 420 characters.</summary>
    public string? TwitterImageAlt { get; set; }

    /// <summary><c>twitter:player</c> — HTTPS iframe URL (card = player).</summary>
    public string? PlayerUrl { get; set; }
    public int? PlayerWidth { get; set; }
    public int? PlayerHeight { get; set; }
    /// <summary><c>twitter:player:stream</c> — a raw MP4 for clients without the iframe.</summary>
    public string? PlayerStream { get; set; }

    // card = app
    public string? AppNameIphone { get; set; }
    public string? AppIdIphone { get; set; }
    public string? AppUrlIphone { get; set; }
    public string? AppNameIpad { get; set; }
    public string? AppIdIpad { get; set; }
    public string? AppUrlIpad { get; set; }
    public string? AppNameGooglePlay { get; set; }
    public string? AppIdGooglePlay { get; set; }
    public string? AppUrlGooglePlay { get; set; }
    /// <summary><c>twitter:app:country</c> — App Store country when the app is not in the US store.</summary>
    public string? AppCountry { get; set; }

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    /// <summary>Null for an instance with nothing set, so a page that never used any of this stores nothing.</summary>
    public string? ToJson() => IsEmpty ? null : JsonSerializer.Serialize(this, Options);

    /// <summary>Never throws: a column that somehow holds something unreadable behaves as "nothing set".</summary>
    public static SocialMeta FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new SocialMeta();
        try
        {
            return JsonSerializer.Deserialize<SocialMeta>(json, Options) ?? new SocialMeta();
        }
        catch (JsonException)
        {
            return new SocialMeta();
        }
    }

    [JsonIgnore]
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(SiteName) && string.IsNullOrWhiteSpace(Determiner) && string.IsNullOrWhiteSpace(Locale)
        && string.IsNullOrWhiteSpace(ImageAlt) && ImageWidth is null && ImageHeight is null && string.IsNullOrWhiteSpace(ImageType)
        && string.IsNullOrWhiteSpace(Video) && string.IsNullOrWhiteSpace(VideoType) && VideoWidth is null && VideoHeight is null
        && string.IsNullOrWhiteSpace(Audio) && string.IsNullOrWhiteSpace(AudioType)
        && ArticlePublishedTime is null && ArticleModifiedTime is null && ArticleExpirationTime is null
        && ArticleAuthors.Count == 0 && string.IsNullOrWhiteSpace(ArticleSection) && ArticleTags.Count == 0 && string.IsNullOrWhiteSpace(ArticlePublisher)
        && string.IsNullOrWhiteSpace(ProfileFirstName) && string.IsNullOrWhiteSpace(ProfileLastName)
        && string.IsNullOrWhiteSpace(ProfileUsername) && string.IsNullOrWhiteSpace(ProfileGender)
        && BookAuthors.Count == 0 && string.IsNullOrWhiteSpace(BookIsbn) && BookReleaseDate is null && BookTags.Count == 0
        && VideoActors.Count == 0 && VideoDirectors.Count == 0 && VideoWriters.Count == 0 && VideoDuration is null
        && VideoReleaseDate is null && VideoTags.Count == 0 && string.IsNullOrWhiteSpace(VideoSeries)
        && MusicDuration is null && string.IsNullOrWhiteSpace(MusicAlbum) && MusicAlbumDisc is null && MusicAlbumTrack is null
        && MusicSongs.Count == 0 && MusicMusicians.Count == 0 && string.IsNullOrWhiteSpace(MusicCreator) && MusicReleaseDate is null
        && string.IsNullOrWhiteSpace(FacebookAppId)
        && string.IsNullOrWhiteSpace(TwitterCreator) && string.IsNullOrWhiteSpace(TwitterTitle) && string.IsNullOrWhiteSpace(TwitterDescription)
        && string.IsNullOrWhiteSpace(TwitterImage) && string.IsNullOrWhiteSpace(TwitterImageAlt)
        && string.IsNullOrWhiteSpace(PlayerUrl) && PlayerWidth is null && PlayerHeight is null && string.IsNullOrWhiteSpace(PlayerStream)
        && string.IsNullOrWhiteSpace(AppNameIphone) && string.IsNullOrWhiteSpace(AppIdIphone) && string.IsNullOrWhiteSpace(AppUrlIphone)
        && string.IsNullOrWhiteSpace(AppNameIpad) && string.IsNullOrWhiteSpace(AppIdIpad) && string.IsNullOrWhiteSpace(AppUrlIpad)
        && string.IsNullOrWhiteSpace(AppNameGooglePlay) && string.IsNullOrWhiteSpace(AppIdGooglePlay) && string.IsNullOrWhiteSpace(AppUrlGooglePlay)
        && string.IsNullOrWhiteSpace(AppCountry);
}
