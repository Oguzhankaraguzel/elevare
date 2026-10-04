using Application.Abstraction.Services;
using Application.Features.Queries.Search.SearchPublicContent;
using SharedKernel.Concrete;
using Shouldly;

namespace Application.UnitTests.Features;

/// <summary>
/// Covers the handler's own responsibility — the short-term guard and delegating
/// to <see cref="ISearchProvider"/> — independent from any specific provider's
/// matching logic (see <c>SqlSearchProviderTests</c> for that).
/// </summary>
public sealed class SearchPublicContentQueryHandlerTests
{
    [Fact]
    public async Task Term_shorter_than_two_characters_returns_empty_without_calling_the_provider()
    {
        StubSearchProvider provider = new();
        SearchPublicContentQueryHandler handler = new(provider);

        Result<List<SearchResultItemResponse>> result = await handler.Handle(
            new SearchPublicContentQuery("k", "tr"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeEmpty();
        provider.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task Delegates_a_valid_term_to_the_configured_provider()
    {
        StubSearchProvider provider = new();
        SearchPublicContentQueryHandler handler = new(provider);

        Result<List<SearchResultItemResponse>> result = await handler.Handle(
            new SearchPublicContentQuery("kalp", "tr", 5), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldHaveSingleItem();
        provider.CallCount.ShouldBe(1);
        provider.LastTerm.ShouldBe("kalp");
        provider.LastLanguageCode.ShouldBe("tr");
        provider.LastMaxResults.ShouldBe(5);
    }

    [Fact]
    public async Task Provider_failure_is_propagated_rather_than_reported_as_no_results()
    {
        // The point of ISearchProvider returning a Result: a backend that could not be
        // queried must not reach the visitor as a confident "nothing found".
        StubSearchProvider provider = new() { FailWith = SearchErrors.Failed("the index is offline") };
        SearchPublicContentQueryHandler handler = new(provider);

        Result<List<SearchResultItemResponse>> result = await handler.Handle(
            new SearchPublicContentQuery("kalp", "tr"), CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Search.Failed");
        provider.CallCount.ShouldBe(1);
    }

    private sealed class StubSearchProvider : ISearchProvider
    {
        public int CallCount { get; private set; }
        public string? LastTerm { get; private set; }
        public string? LastLanguageCode { get; private set; }
        public int LastMaxResults { get; private set; }

        /// <summary>When set, the provider reports this failure instead of returning hits.</summary>
        public Error? FailWith { get; init; }

        public Task<Result<List<SearchResultItemResponse>>> SearchAsync(
            string term, string languageCode, int maxResults, CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastTerm = term;
            LastLanguageCode = languageCode;
            LastMaxResults = maxResults;

            if (FailWith is not null)
                return Task.FromResult(Result.Failure<List<SearchResultItemResponse>>(FailWith));

            return Task.FromResult(Result.Success(new List<SearchResultItemResponse>
            {
                new("Örnek", "/ornek", "", null)
            }));
        }
    }
}
