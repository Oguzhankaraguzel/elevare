using Application.Services;
using Shouldly;

namespace Application.UnitTests.Services;

/// <summary>Covers <see cref="SearchTerm"/>: a term lower-cased the way PostgreSQL lower-cases the column.</summary>
public sealed class SearchTermTests
{
    [Theory]
    [InlineData("İŞLEM", "işlem")]
    [InlineData("İstanbul", "istanbul")]
    [InlineData("XOR", "xor")]
    [InlineData("Bit Tabanlı", "bit tabanlı")]
    public void Terms_are_lowered_like_the_database_lowers_the_column(string term, string expected)
    {
        SearchTerm.LowerLikeDatabase(term).ShouldBe(expected);
    }
}
