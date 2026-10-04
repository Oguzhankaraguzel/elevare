using SharedKernel.Content;
using Shouldly;

namespace Application.UnitTests.Services;

/// <summary>Covers <see cref="HumanDateParser"/>: the date an Article block's byline states.</summary>
public sealed class HumanDateParserTests
{
    [Theory]
    [InlineData("Yazar: Ada · 13 Ağustos 2023", 2023, 8, 13)]
    [InlineData("By Ada · August 13, 2023", 2023, 8, 13)]
    [InlineData("13 August 2023", 2023, 8, 13)]
    [InlineData("13.08.2023", 2023, 8, 13)]
    [InlineData("2023-08-13", 2023, 8, 13)]
    [InlineData("1 şubat 2024", 2024, 2, 1)]
    public void Written_dates_are_read(string text, int year, int month, int day)
    {
        HumanDateParser.TryParse(text, out DateTime date).ShouldBeTrue();
        date.ShouldBe(new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc));
    }

    [Theory]
    [InlineData("Ağustos 2023")]
    [InlineData("31 Şubat 2024")]
    [InlineData("hiç tarih yok")]
    [InlineData("")]
    public void Partial_or_impossible_dates_are_not(string text)
    {
        HumanDateParser.TryParse(text, out _).ShouldBeFalse();
    }

    [Fact]
    public void An_article_date_with_nothing_written_falls_back_to_the_attribute()
    {
        HumanDateParser.ResolveArticleDate("2026-03-05", "Yazar: Ada").ShouldBe(new DateTime(2026, 3, 5, 0, 0, 0, DateTimeKind.Utc));
        HumanDateParser.ResolveArticleDate(null, null).ShouldBeNull();
    }
}
