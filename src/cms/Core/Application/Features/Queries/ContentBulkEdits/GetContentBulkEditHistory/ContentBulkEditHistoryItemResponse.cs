using Domain.Entities.ContentBulkEdits;

namespace Application.Features.Queries.ContentBulkEdits.GetContentBulkEditHistory;

public sealed record ContentBulkEditHistoryItemResponse(
    int Id,
    ContentBulkEditKind Kind,
    string? SearchText,
    string? ReplaceText,
    int AffectedPageCount,
    bool IsReverted,
    DateTime CreateDate,
    DateTime? RevertedDate,
    List<ContentBulkEditAffectedPageResponse> AffectedPages);
