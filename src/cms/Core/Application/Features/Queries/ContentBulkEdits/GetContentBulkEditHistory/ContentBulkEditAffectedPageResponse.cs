namespace Application.Features.Queries.ContentBulkEdits.GetContentBulkEditHistory;

public sealed record ContentBulkEditAffectedPageResponse(
    int PageInfoId,
    string PageTitleSnapshot,
    string PageFullSlugSnapshot,
    int MatchCount);
