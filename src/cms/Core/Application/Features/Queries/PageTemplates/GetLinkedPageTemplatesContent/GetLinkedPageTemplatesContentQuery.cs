using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.PageTemplates.GetLinkedPageTemplatesContent;

/// <summary>
/// Returns the current content of every <c>IsLinked</c> template, keyed by id, so the
/// page/template editor can refresh any inserted "linked" blocks to the latest version
/// as soon as the canvas is opened.
/// </summary>
public sealed record GetLinkedPageTemplatesContentQuery : IQuery<Dictionary<int, LinkedPageTemplateContent>>;
