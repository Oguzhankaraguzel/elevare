namespace Application.Features.Queries.ContentBulkEdits.SearchPagesByContent;

public sealed record PageContentMatchResponse(
    int PageInfoId,
    string Title,
    string FullSlug,
    string LanguageName,
    int MatchCount);
