using Domain.Entities.PageTemplates;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.PageTemplates.GetPageTemplates;

/// <summary>Lists reusable templates, optionally filtered by type and/or language.</summary>
public sealed record GetPageTemplatesQuery(PageTemplateType? Type = null, int? LanguageId = null) : IQuery<List<PageTemplateListItemResponse>>;
