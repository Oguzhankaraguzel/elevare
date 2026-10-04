using Application.Features.Queries.Search.SearchPublicContent;
using SharedKernel.Concrete;

namespace Application.Abstraction.Services;

/// <summary>
/// Strategy behind the public site's search. Only a SQL implementation exists
/// today (<c>SqlSearchProvider</c>); the seam is here so a real search engine can
/// be substituted without touching the query handler or the endpoint.
/// <para>
/// An empty list means "searched, found nothing"; a failure result means the
/// search never ran. Keeping those apart matters — presenting a backend outage as
/// "no results found" tells the visitor something untrue about the site.
/// </para>
/// </summary>
public interface ISearchProvider
{
    Task<Result<List<SearchResultItemResponse>>> SearchAsync(
        string term, string languageCode, int maxResults, CancellationToken cancellationToken = default);
}
