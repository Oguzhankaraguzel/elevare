using Domain.Entities.PageInfos;

namespace Application.Features.Queries.Pages.GetPages;

public sealed record PageListItemResponse(
    int Id,
    string Title,
    string Slug,
    string FullSlug,
    int LanguageId,
    string LanguageName,
    PageStatus Status,
    PageStatus? PendingStatus,
    DateTime CreateDate,
    DateTime? UpdateDate,
    int? PageGroupId,
    int? ParentPageId,
    int? SeoScore,
    PageKind Kind,
    PageSignal Signals,
    string? CreatedBy,
    string? UpdatedBy);
