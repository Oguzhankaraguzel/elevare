using Application.Abstraction.Data;
using Domain.Entities.PublicPages;
using Domain.Entities.PublicRedirects;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.Redirects.GetRedirectTarget;

internal sealed class GetRedirectTargetQueryHandler(IPublicReadDbContext db)
    : IQueryHandler<GetRedirectTargetQuery, string>
{
    // Same bound as the CMS's redirect-health analysis (GetRedirectsQueryHandler) —
    // enough hops for any legitimate chain, and what turns a genuine cycle into a
    // clean "not found" instead of a live infinite 301 loop for visitors.
    private const int MaxChainWalk = 10;

    public async Task<Result<string>> Handle(GetRedirectTargetQuery request, CancellationToken cancellationToken)
    {
        // Mirrors GetPublicPageBySlugQueryHandler's FullSlug reconstruction so both
        // sides agree on the same key regardless of which language is default.
        string defaultCode = await db.Languages
            .Where(l => l.IsDefault && l.IsActive)
            .Select(l => l.TwoLetterCode)
            .FirstOrDefaultAsync(cancellationToken) ?? "tr";

        bool isDefaultLanguage = string.Equals(request.LanguageCode, defaultCode, StringComparison.OrdinalIgnoreCase);
        string prefixed = request.Slug.Length == 0 ? request.LanguageCode : $"{request.LanguageCode}/{request.Slug}";
        string fullSlug = isDefaultLanguage ? request.Slug : prefixed;

        PublicRedirect? redirect = await db.Redirects
            .FirstOrDefaultAsync(r => r.OldPath == fullSlug, cancellationToken);

        if (redirect is null)
            return Result.Failure<string>(PublicRedirectErrors.NotFound(fullSlug));

        // Two rules can point at each other (a genuine authoring mistake, but the CMS
        // lets you save it — the admin screen only warns). Resolving that here would
        // hand PageController a target that 301s a real visitor in circles forever,
        // so the walk below collapses any chain into its single final target and
        // refuses outright the moment a hop revisits a path already seen.
        HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase) { Normalize(fullSlug) };

        for (int hop = 0; hop < MaxChainWalk; hop++)
        {
            string? target = await ResolveHopTargetAsync(redirect, cancellationToken);

            // "" is a legitimate target — the homepage's own FullSlug, once "home" is
            // stripped from it — so only a genuinely absent target (null) counts as
            // unresolvable; IsNullOrWhiteSpace would wrongly reject the site root too.
            if (target is null)
                return Result.Failure<string>(PublicRedirectErrors.NoTarget(fullSlug));

            if (IsExternal(target))
                return Result.Success(target);

            string key = Normalize(target);
            if (!visited.Add(key))
                return Result.Failure<string>(PublicRedirectErrors.Loop(fullSlug));

            PublicRedirect? next = await db.Redirects.FirstOrDefaultAsync(r => r.OldPath == key, cancellationToken);
            if (next is null)
                return Result.Success(target);

            redirect = next;
        }

        // Ran out of hops without the chain closing on itself or landing anywhere —
        // no client follows this many redirects either, so it is a loop in practice.
        return Result.Failure<string>(PublicRedirectErrors.TooLong(fullSlug));
    }

    // A source page always wins over the frozen NewPath snapshot, so a page renamed
    // twice keeps resolving correctly — but only while that page is still live; if
    // it's since been unpublished/deleted, fall back to NewPath.
    private async Task<string?> ResolveHopTargetAsync(PublicRedirect redirect, CancellationToken cancellationToken)
    {
        if (redirect.SourcePageId is int pageId)
        {
            string? currentFullSlug = await db.PageInfos
                .Where(p => p.Id == pageId && p.PageStatus == PublicPageStatus.Published && p.IsActive)
                .Select(p => p.FullSlug)
                .FirstOrDefaultAsync(cancellationToken);

            if (currentFullSlug is not null)
                return $"/{currentFullSlug}";
        }

        return redirect.NewPath;
    }

    // OldPath is stored in FullSlug form with no leading slash, while NewPath and a
    // resolved page's "/" + FullSlug both carry one — comparing them raw would miss
    // every real match, so both sides of every hop go through this first.
    private static string Normalize(string? path) =>
        string.IsNullOrWhiteSpace(path) ? string.Empty : path.Trim().TrimStart('/').TrimEnd('/');

    private static bool IsExternal(string target) =>
        target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        target.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
        target.StartsWith("//", StringComparison.Ordinal);
}
