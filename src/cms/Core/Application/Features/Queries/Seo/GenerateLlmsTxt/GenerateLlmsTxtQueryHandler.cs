using System.Text;
using Application.Abstraction.Data;
using Domain.Entities.PageInfos;
using Domain.Entities.SiteSettings;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.Seo.GenerateLlmsTxt;

internal sealed class GenerateLlmsTxtQueryHandler(ICmsApplicationDbContext db)
    : IQueryHandler<GenerateLlmsTxtQuery, string>
{
    /// <summary>
    /// Enough to describe a site, few enough that a human will actually read the draft
    /// and prune it. A site with hundreds of pages does not need all of them here.
    /// </summary>
    private const int MaxPages = 50;

    public async Task<Result<string>> Handle(GenerateLlmsTxtQuery request, CancellationToken cancellationToken)
    {
        Dictionary<string, string?> settings = await db.SiteSettings
            .Where(s => !s.IsDeleted)
            .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);

        string siteName = Fallback(settings.GetValueOrDefault("General.SiteName"), "Site");
        string? tagline = settings.GetValueOrDefault("General.Tagline");
        string? description = settings.GetValueOrDefault(SeoSettingKeys.MetaDescription);
        string baseUrl = Fallback(settings.GetValueOrDefault("Advanced.PublicSiteBaseUrl"), "").TrimEnd('/');

        // Only the default language: llms.txt has no hreflang equivalent, and listing
        // every translation of every page turns the map into noise. System pages are
        // dropped for a stronger reason — an error screen is not somewhere a reader
        // should be sent, and listing /404 as a destination is actively misleading.
        var pages = await db.PageInfos
            .Where(p => !p.IsDeleted
                && p.IsActive
                && p.PageStatus == PageStatus.Published
                && p.Language.IsDefault
                && !SystemPageSlugs.All.Contains(p.Slug))
            .OrderBy(p => p.FullSlug)
            .Take(MaxPages)
            .Select(p => new
            {
                p.FullSlug,
                p.SeoMeta.Title,
                Summary = p.SeoMeta.MetaDescription,
            })
            .ToListAsync(cancellationToken);

        StringBuilder builder = new();
        builder.Append("# ").AppendLine(siteName);

        if (!string.IsNullOrWhiteSpace(tagline))
            builder.AppendLine().Append("> ").AppendLine(tagline.Trim());

        if (!string.IsNullOrWhiteSpace(description))
            builder.AppendLine().AppendLine(description.Trim());

        builder.AppendLine().AppendLine("## Sayfalar").AppendLine();

        foreach (var page in pages)
        {
            string title = Fallback(page.Title, page.FullSlug);
            string url = $"{baseUrl}/{page.FullSlug.TrimStart('/')}";

            builder.Append("- [").Append(title).Append("](").Append(url).Append(')');

            if (!string.IsNullOrWhiteSpace(page.Summary))
                builder.Append(": ").Append(page.Summary.Trim());

            builder.AppendLine();
        }

        return Result.Success(builder.ToString());
    }

    private static string Fallback(string? value, string whenEmpty) =>
        string.IsNullOrWhiteSpace(value) ? whenEmpty : value.Trim();
}
