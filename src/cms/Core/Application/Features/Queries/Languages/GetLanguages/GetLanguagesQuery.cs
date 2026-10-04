using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.Languages.GetLanguages;

public sealed record GetLanguagesQuery(bool? IsActive = null)
    : IQuery<PagedResult<LanguageResponse>>;
