using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.Redirects.GetRedirectTarget;

/// <summary>
/// Looks up whether a path that failed to resolve to a live page has a redirect
/// rule pointing somewhere else (old slug after a rename, or a deleted/archived
/// page's replacement URL). Returns the target path/URL to send a 301 to.
/// </summary>
public sealed record GetRedirectTargetQuery(string LanguageCode, string Slug) : IQuery<string>;
