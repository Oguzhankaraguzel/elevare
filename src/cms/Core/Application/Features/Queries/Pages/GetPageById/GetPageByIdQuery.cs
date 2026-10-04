using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.Pages.GetPageById;

public sealed record GetPageByIdQuery(int Id) : IQuery<PageEditorResponse>;
