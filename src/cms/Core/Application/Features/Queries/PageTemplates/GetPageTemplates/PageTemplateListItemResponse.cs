using Domain.Entities.PageTemplates;

namespace Application.Features.Queries.PageTemplates.GetPageTemplates;

public sealed record PageTemplateListItemResponse(
    int Id,
    string Name,
    PageTemplateType Type,
    bool IsLinked,
    DateTime CreateDate,
    DateTime? UpdateDate,
    int? LanguageId,
    string? LanguageName);
