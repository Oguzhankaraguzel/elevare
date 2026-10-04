using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.PageTemplates.GetLinkedPageTemplatesContent;

public sealed record LinkedPageTemplateContent(string? GjsHtml, string? GjsCss);
