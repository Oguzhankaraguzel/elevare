using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.PageTemplates.GetPageTemplateVersions;

/// <summary>
/// Every template's content version (<c>ContentChangedAt ?? UpdateDate ?? CreateDate</c>), keyed by id.
/// <para>
/// A page that inserted a template while it was <em>not</em> <c>IsLinked</c> keeps
/// no live connection to it — that's the whole point of the unlinked/linked split
/// (see <c>PageTemplate.IsLinked</c>). But an editor who did that insert still
/// deserves to know their page's copy is now out of date with the source; the page
/// editor stamps the version at insert time onto the block and compares it against
/// this map on load to decide whether to say so.
/// </para>
/// </summary>
public sealed record GetPageTemplateVersionsQuery : IQuery<Dictionary<int, PageTemplateVersionInfo>>;
