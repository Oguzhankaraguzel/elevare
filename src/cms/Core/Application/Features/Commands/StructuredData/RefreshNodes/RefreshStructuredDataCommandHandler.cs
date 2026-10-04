using System.Text.Json.Nodes;
using Application.Abstraction.Data;
using Domain.Entities.ContentBulkEdits;
using Domain.Entities.Languages;
using Domain.Entities.PageInfos;
using Domain.Entities.StructuredData;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Commands.StructuredData.RefreshNodes;

/// <summary>
/// Re-derives the nodes the CMS owns and writes them back over every page that
/// already has structured data.
/// <para>
/// A draft copies the organisation's details into the page rather than referencing
/// them, so a new phone number or social profile in Site Settings reaches nothing
/// already built. This is how that gets paid: Organization, WebSite and
/// BreadcrumbList are rebuilt from current data and replaced by <c>@type</c>, while
/// every other node — the Article an editor filled in, the custom fields they
/// added, the FAQ — is left exactly as it was.
/// </para>
/// <para>
/// The run is recorded as a <see cref="ContentBulkEdit"/> alongside find/replace
/// runs, so it shows up in the same history and can be reverted the same way. A
/// bulk write across every page needs an undo, and this is the one the CMS already
/// has rather than a second one built beside it.
/// </para>
/// </summary>
internal sealed class RefreshStructuredDataCommandHandler(ICmsApplicationDbContext db)
    : ICommandHandler<RefreshStructuredDataCommand, RefreshStructuredDataResult>
{
    public async Task<Result<RefreshStructuredDataResult>> Handle(
        RefreshStructuredDataCommand request,
        CancellationToken cancellationToken)
    {
        Dictionary<string, string?> settings = await db.SiteSettings
            .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);

        string? baseUrl = SchemaNodeFactory
            .Value(settings, SchemaNodeFactory.PublicSiteBaseUrlKey)?.TrimEnd('/');

        if (string.IsNullOrWhiteSpace(baseUrl))
            return Result.Failure<RefreshStructuredDataResult>(
                StructuredDataErrors.PublicSiteBaseUrlMissing);

        // The default language counts even when it is not flagged published: it is
        // the language the site is actually served in, and a contact point that
        // omits it would claim visitors cannot be helped in it.
        List<Language> languages = await db.Languages
            .Where(l => l.IsPublished || l.IsDefault)
            .OrderBy(l => l.DisplayOrder)
            .ToListAsync(cancellationToken);

        // The logo's size, so the Organization node carries it without anyone typing it.
        string? logo = SchemaNodeFactory.Value(settings, "Appearance.LogoUrl");
        string logoPath = logo is null ? "" : SchemaNodeFactory.MediaKey(logo);
        Dictionary<string, MediaInfo> logoMedia = logo is null
            ? []
            : await db.MediaFiles
                .AsNoTracking()
                .Where(m => m.FilePath == logo || m.FilePath == logoPath)
                .Take(1)
                .ToDictionaryAsync(m => m.FilePath, m => new MediaInfo(m.Width, m.Height, m.MimeType, m.AltText), cancellationToken);

        // Tracked on purpose — these get written back.
        List<PageInfo> pages = await db.PageInfos
            .Include(p => p.Language)
            .Where(p => p.SeoMeta.StructuredData != null && p.SeoMeta.StructuredData != "")
            .ToListAsync(cancellationToken);

        var bulkEdit = new ContentBulkEdit { Kind = ContentBulkEditKind.StructuredDataRefresh };

        int unchanged = 0;
        List<string> skipped = [];

        foreach (PageInfo page in pages)
        {
            string? before = page.SeoMeta.StructuredData;

            if (!SchemaGraph.TryParse(before, out SchemaGraph graph))
            {
                skipped.Add(page.FullSlug);
                continue;
            }

            graph.Upsert(SchemaNodeFactory.BuildOrganization(settings, baseUrl, languages, logoMedia));
            graph.Upsert(await SchemaNodeFactory
                .BuildWebSiteAsync(db, settings, baseUrl, languages, cancellationToken));

            JsonObject? breadcrumb = await SchemaNodeFactory
                .BuildBreadcrumbAsync(db, page, baseUrl, settings, cancellationToken);

            // A page moved to the top level no longer has a trail; leaving the old
            // one would advertise a parent it no longer has.
            if (breadcrumb is not null)
            {
                graph.Upsert(breadcrumb);
            }
            else
            {
                int index = graph.IndexOfType(SchemaCatalog.BreadcrumbList.Type);
                if (index >= 0) graph.RemoveAt(index);
            }

            string after = graph.Serialize();
            if (string.Equals(after, before, StringComparison.Ordinal))
            {
                unchanged++;
                continue;
            }

            bulkEdit.Items.Add(new ContentBulkEditItem
            {
                ContentBulkEdit = bulkEdit,
                PageInfoId = page.Id,
                PageTitleSnapshot = page.SeoMeta.Title,
                PageFullSlugSnapshot = page.FullSlug,
                MatchCount = 1,
                OldStructuredData = before,
            });

            page.SeoMeta.StructuredData = after;
        }

        int updated = bulkEdit.Items.Count;
        if (updated > 0)
        {
            bulkEdit.AffectedPageCount = updated;
            db.ContentBulkEdits.Add(bulkEdit);
            await db.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(new RefreshStructuredDataResult(updated, unchanged, skipped));
    }

}
