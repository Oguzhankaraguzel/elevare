using SharedKernel.Media;
using Shouldly;

namespace Cms.Tests.Media;

/// <summary>
/// The JSON both applications read and write for an image's renditions. The CMS
/// writes it once at upload; the public site parses it on every uncached render,
/// so "never throws" matters as much as the round trip.
/// </summary>
public sealed class ImageRenditionsTests
{
    [Fact]
    public void Round_trips_and_orders_by_width()
    {
        string? json = ImageRenditions.ToJson(
        [
            new ImageRendition(960, 540, "/uploads/images/a-960w.webp", "/uploads/images/a-960w.jpg"),
            new ImageRendition(480, 270, "/uploads/images/a-480w.webp", "/uploads/images/a-480w.jpg"),
            new ImageRendition(1920, 1080, "/uploads/images/a-1920w.webp", null),
        ]);

        json.ShouldNotBeNull();
        // Short keys: this lands on every image row, and nothing ever reads it by name in SQL.
        json.ShouldContain("\"w\":960");
        json.ShouldNotContain("\"fb\":null");

        IReadOnlyList<ImageRendition> back = ImageRenditions.Parse(json);
        back.Select(r => r.Width).ShouldBe([480, 960, 1920]);
        back[2].FallbackPath.ShouldBeNull();
        back[0].WebpPath.ShouldBe("/uploads/images/a-480w.webp");
    }

    [Fact]
    public void Nothing_to_store_is_null_not_an_empty_array()
    {
        ImageRenditions.ToJson([]).ShouldBeNull();
    }

    [Fact]
    public void Parse_is_lenient_with_null_garbage_and_nonsense_sizes()
    {
        ImageRenditions.Parse(null).ShouldBeEmpty();
        ImageRenditions.Parse("").ShouldBeEmpty();
        ImageRenditions.Parse("not json").ShouldBeEmpty();
        ImageRenditions.Parse("{\"w\":1}").ShouldBeEmpty();
        ImageRenditions.Parse("[{\"w\":0,\"h\":10,\"webp\":\"/x.webp\"}]").ShouldBeEmpty();
    }
}
