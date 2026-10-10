namespace Application.Features.Queries.Redirects.GetRedirectTarget;

/// <summary>Where a redirect rule sends the visitor, and whether to say so with 301 or 302.</summary>
/// <param name="Url">The final target, after the whole chain has been followed.</param>
/// <param name="IsPermanent">
/// False when any rule along the chain is temporary: one 302 hop means the move is
/// not settled, and a 301 would tell search engines to forget the original address.
/// </param>
#pragma warning disable CA1054 // A relative path or an absolute URL, passed straight to Redirect().
public sealed record RedirectTarget(string Url, bool IsPermanent);
#pragma warning restore CA1054
