using Application.Services;
using SharedKernel.Social;
using Shouldly;

namespace Application.UnitTests.Services;

/// <summary>
/// The tags a page emits for link previews. What matters is which tags appear for
/// which og:type, that the X tags are written out with their fallbacks, and that
/// nothing empty leaks — a <c>&lt;meta property="og:image:alt" content=""&gt;</c> is
/// a tag readers act on, not a no-op.
/// </summary>
public sealed class SocialMetaTagBuilderTests
{
    private static SocialTagInput Input(string type = "website", SocialMeta? social = null, string? twitterCard = null, string? image = "https://x.test/a.jpg") =>
        new("Başlık", "Açıklama", type, image, "https://x.test/p", twitterCard, "@site", "tr_TR", ["en_US"], social ?? new SocialMeta());

    private static Dictionary<string, List<string>> ByKey(IEnumerable<SocialMetaTag> tags) =>
        tags.GroupBy(t => t.Key).ToDictionary(g => g.Key, g => g.Select(t => t.Content).ToList());

    [Fact]
    public void Core_tags_come_out_in_order_with_locale_alternates()
    {
        IReadOnlyList<SocialMetaTag> tags = SocialMetaTagBuilder.Build(Input());

        tags.Select(t => t.Key).Take(6).ShouldBe(["og:locale", "og:locale:alternate", "og:type", "og:title", "og:description", "og:url"]);
        tags.First(t => t.Key == "og:locale").Content.ShouldBe("tr_TR");
        tags.ShouldContain(t => t.Key == "og:image:secure_url" && t.Content == "https://x.test/a.jpg");
        tags.ShouldContain(t => t.Key == "twitter:site" && t.Attribute == "name");
    }

    [Fact]
    public void Nothing_empty_is_emitted()
    {
        SocialMeta social = new() { ImageAlt = "  ", Determiner = "", ArticleTags = ["", "  "] };

        IReadOnlyList<SocialMetaTag> tags = SocialMetaTagBuilder.Build(Input("article", social, image: null));

        tags.Select(t => t.Key).ShouldNotContain("og:image");
        tags.Select(t => t.Key).ShouldNotContain("og:image:alt");
        tags.Select(t => t.Key).ShouldNotContain("og:determiner");
        tags.Select(t => t.Key).ShouldNotContain("article:tag");
        tags.ShouldAllBe(t => t.Content.Length > 0);
    }

    [Fact]
    public void Type_specific_properties_follow_og_type()
    {
        SocialMeta social = new()
        {
            ArticlePublishedTime = new DateTime(2026, 9, 14, 10, 30, 0, DateTimeKind.Unspecified),
            ArticleAuthors = ["https://x.test/ali"],
            ArticleTags = ["csharp", "bit"],
            ProfileFirstName = "Oğuzhan",
            BookIsbn = "978-3",
        };

        Dictionary<string, List<string>> article = ByKey(SocialMetaTagBuilder.Build(Input("article", social)));
        article["article:published_time"].ShouldBe(["2026-09-14T10:30:00"]);
        article["article:tag"].ShouldBe(["csharp", "bit"]);
        article.ShouldNotContainKey("profile:first_name");
        article.ShouldNotContainKey("book:isbn");

        Dictionary<string, List<string>> profile = ByKey(SocialMetaTagBuilder.Build(Input("profile", social)));
        profile["profile:first_name"].ShouldBe(["Oğuzhan"]);
        profile.ShouldNotContainKey("article:tag");
    }

    [Fact]
    public void Profile_gender_is_only_the_protocols_two_values()
    {
        SocialMeta social = new() { ProfileGender = "other" };

        ByKey(SocialMetaTagBuilder.Build(Input("profile", social))).ShouldNotContainKey("profile:gender");
    }

    [Fact]
    public void Video_actor_role_follows_its_actor()
    {
        SocialMeta social = new()
        {
            VideoActors = [new SocialPerson { Url = "https://x.test/a", Role = "Lead" }, new SocialPerson { Url = "https://x.test/b" }],
            VideoSeries = "https://x.test/show",
        };

        List<SocialMetaTag> movie = [.. SocialMetaTagBuilder.Build(Input("video.movie", social)).Where(t => t.Key.StartsWith("video:", StringComparison.Ordinal))];
        movie.Select(t => t.Key).ShouldBe(["video:actor", "video:actor:role", "video:actor"]);
        movie.ShouldNotContain(t => t.Key == "video:series");

        ByKey(SocialMetaTagBuilder.Build(Input("video.episode", social)))["video:series"].ShouldBe(["https://x.test/show"]);
    }

    [Fact]
    public void Music_types_get_their_own_shape()
    {
        SocialMeta social = new() { MusicDuration = 240, MusicSongs = ["https://x.test/s1"], MusicCreator = "https://x.test/dj", MusicAlbumTrack = 3 };

        Dictionary<string, List<string>> song = ByKey(SocialMetaTagBuilder.Build(Input("music.song", social)));
        song["music:duration"].ShouldBe(["240"]);
        song["music:album:track"].ShouldBe(["3"]);
        song.ShouldNotContainKey("music:song");

        Dictionary<string, List<string>> playlist = ByKey(SocialMetaTagBuilder.Build(Input("music.playlist", social)));
        playlist["music:song"].ShouldBe(["https://x.test/s1"]);
        playlist["music:creator"].ShouldBe(["https://x.test/dj"]);
        playlist.ShouldNotContainKey("music:duration");
    }

    [Fact]
    public void Empty_twitter_fields_write_no_tag_x_reads_open_graph_itself()
    {
        SocialMeta social = new() { ImageAlt = "Kapak", TwitterCreator = "@ali" };

        Dictionary<string, List<string>> tags = ByKey(SocialMetaTagBuilder.Build(Input(social: social, twitterCard: "summary_large_image")));

        tags["twitter:card"].ShouldBe(["summary_large_image"]);
        tags["twitter:creator"].ShouldBe(["@ali"]);
        tags.ShouldNotContainKey("twitter:title");
        tags.ShouldNotContainKey("twitter:image");
        tags.ShouldNotContainKey("twitter:image:alt");
        tags.ShouldNotContainKey("twitter:player");
    }

    [Fact]
    public void Twitter_fields_that_are_set_are_written_and_image_alt_is_capped_at_420()
    {
        SocialMeta social = new() { TwitterTitle = "X başlığı", TwitterImage = "https://x.test/x.jpg", TwitterImageAlt = new string('a', 500) };

        Dictionary<string, List<string>> tags = ByKey(SocialMetaTagBuilder.Build(Input(social: social, twitterCard: "summary")));

        tags["twitter:title"].ShouldBe(["X başlığı"]);
        tags["twitter:image"].ShouldBe(["https://x.test/x.jpg"]);
        tags["twitter:image:alt"][0].Length.ShouldBe(420);
    }

    [Fact]
    public void Player_and_app_cards_carry_their_own_tags_only()
    {
        SocialMeta social = new()
        {
            PlayerUrl = "https://x.test/player", PlayerWidth = 640, PlayerHeight = 360,
            AppIdIphone = "123", AppNameIphone = "App", AppCountry = "TR",
        };

        Dictionary<string, List<string>> player = ByKey(SocialMetaTagBuilder.Build(Input(social: social, twitterCard: "player")));
        player["twitter:player:width"].ShouldBe(["640"]);
        player.ShouldNotContainKey("twitter:app:id:iphone");

        Dictionary<string, List<string>> app = ByKey(SocialMetaTagBuilder.Build(Input(social: social, twitterCard: "app")));
        app["twitter:app:id:iphone"].ShouldBe(["123"]);
        app["twitter:app:country"].ShouldBe(["TR"]);
        app.ShouldNotContainKey("twitter:player");
    }

    [Fact]
    public void No_card_means_no_twitter_content_tags_but_attribution_stays()
    {
        Dictionary<string, List<string>> tags = ByKey(SocialMetaTagBuilder.Build(Input(social: new SocialMeta { TwitterCreator = "@ali" })));

        tags.ShouldNotContainKey("twitter:card");
        tags.ShouldNotContainKey("twitter:title");
        tags["twitter:site"].ShouldBe(["@site"]);
        tags["twitter:creator"].ShouldBe(["@ali"]);
    }

    [Fact]
    public void Json_round_trip_keeps_everything_and_empty_stores_nothing()
    {
        new SocialMeta().ToJson().ShouldBeNull();
        SocialMeta.FromJson("not json").IsEmpty.ShouldBeTrue();

        SocialMeta social = new() { ArticleTags = ["a"], VideoActors = [new SocialPerson { Url = "u", Role = "r" }], ImageWidth = 1200 };
        var back = SocialMeta.FromJson(social.ToJson());
        back.ArticleTags.ShouldBe(["a"]);
        back.VideoActors.Single().Role.ShouldBe("r");
        back.ImageWidth.ShouldBe(1200);
    }
}
