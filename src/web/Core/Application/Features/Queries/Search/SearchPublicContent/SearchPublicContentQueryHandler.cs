using Application.Abstraction.Services;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.Search.SearchPublicContent;

internal sealed class SearchPublicContentQueryHandler(ISearchProvider searchProvider)
    : IQueryHandler<SearchPublicContentQuery, List<SearchResultItemResponse>>
{
    public async Task<Result<List<SearchResultItemResponse>>> Handle(
        SearchPublicContentQuery request, CancellationToken cancellationToken)
    {
        string term = request.Term.Trim();
        if (term.Length < 2)
            return Result.Success(new List<SearchResultItemResponse>());

        // Passed through as-is: a search backend failure is not the same answer as
        // "no matches", and flattening it here would hide that from the endpoint.
        return await searchProvider.SearchAsync(
            term, request.LanguageCode, request.MaxResults, cancellationToken);
    }
}
