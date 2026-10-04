using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.PageTemplates.GetPageTemplateById;

public sealed record GetPageTemplateByIdQuery(int Id) : IQuery<PageTemplateEditorResponse>;
