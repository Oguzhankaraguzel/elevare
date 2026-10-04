using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.Pages.GetPageDirectory;

/// <summary>
/// Every page, for the page builder's page pickers ("Site sayfası", a listing's
/// source page): all of them at every depth, unlike the parent picker's candidates,
/// which stop above the deepest level because those pages cannot take children.
/// <see cref="LanguageId"/> narrows it to one language; null — a template shared by
/// every language — lists them all (each path already carries its language prefix).
/// </summary>
public sealed record GetPageDirectoryQuery(int? LanguageId) : IQuery<List<PageDirectoryEntryResponse>>;
