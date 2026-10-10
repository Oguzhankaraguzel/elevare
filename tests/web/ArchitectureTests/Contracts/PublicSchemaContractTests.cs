using Domain.Entities.PublicAnalytics;
using Domain.Entities.PublicForms;
using Domain.Entities.PublicLanguages;
using Domain.Entities.PublicLogs;
using Domain.Entities.PublicPages;
using Domain.Entities.PublicRedirects;
using Domain.Entities.PublicSitemaps;
using Domain.Entities.PublicSiteCodeSnippets;
using Domain.Entities.PublicSiteSettings;
using Domain.Entities.PublicTags;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Persistence.Data;
using Shouldly;

namespace ArchitectureTests.Contracts;

/// <summary>
/// Pins the table/column names this Web app READS from the CMS-owned database.
/// <para>
/// The mirror of this contract lives in tests/cms/ArchitectureTests
/// (PublicSchemaContractTests): the CMS side asserts its model actually produces
/// these names; this side asserts the Web mapping expects exactly these names —
/// no more (a new mapped property here must be added to the shared contract in
/// BOTH test files, forcing a conscious check against the CMS schema).
/// </para>
/// </summary>
public sealed class PublicSchemaContractTests
{
    // ── The shared contract — keep in sync with tests/cms/ArchitectureTests ──
    private static readonly Dictionary<string, string[]> PublicContract = new()
    {
        ["PageInfos"] =
        [
            "Id", "Slug", "FullSlug", "LanguageId", "PageGroupId", "ParentPageId", "PageStatus", "IsDeleted", "IsActive",
            "CreateDate", "UpdateDate", "PublishedAt", "Kind",
            "SeoIsCanonical", "SeoCanonicalUrl", "SeoTitle", "SeoMetaDescription",
            "SeoMetaAuthor", "SeoNoIndex", "SeoNoFollow", "SeoStructuredData",
            "OgTitle", "OgDescription", "OgType", "OgImage", "OgUrl",
            "TwitterCard", "TwitterSite", "SeoSocialJson"
        ],
        ["PageContents"] = ["Id", "PageInfoId", "GjsHtml", "GjsCss", "PreviewGjsHtml", "PreviewGjsCss", "IsDeleted"],
        ["PageTemplates"] = ["Id", "Name", "Type", "IsLinked", "GjsHtml", "GjsCss", "IsDeleted"],
        ["SiteSettings"] = ["Id", "Key", "Value", "IsDeleted"],
        ["SiteCodeSnippets"] = ["Id", "Name", "Placement", "Content", "IsEnabled", "SortOrder", "IsDeleted"],
        ["Languages"] = ["Id", "TwoLetterCode", "NameInNative", "IsDefault", "IsActive", "IsPublished", "IsDeleted"],
        ["SitemapCaches"] = ["Id", "CacheKey", "XmlContent"],
        ["Redirects"] = ["Id", "OldPath", "NewPath", "SourcePageId", "IsTemporary", "IsDeleted"],
        ["Tags"] = ["Id", "Name", "Slug", "IsDeleted"],
        ["PageInfoTags"] = ["PageInfoId", "TagId"],
        ["PageInfoSiteCodeExclusions"] = ["PageInfoId", "SiteCodeSnippetId"],
        // The tables the Web app WRITES to (anonymous page-view/click telemetry + form submissions + logs).
        ["PageViewHits"] = ["Id", "Path", "Title", "VisitorId", "ViewedAtUtc", "DurationSeconds"],
        ["PageClickHits"] = ["Id", "Path", "ElementLabel", "VisitorId", "ClickedAtUtc"],
        ["FormSubmissions"] = ["Id", "PageInfoId", "FormName", "FieldsJson", "SubmittedAtUtc"],
        ["AppLogs"] = ["Id", "Level", "Message", "Exception", "Source", "Path", "UserAgent", "CreatedAtUtc"],
    };

    private static readonly Dictionary<string, Type> EntityByTable = new()
    {
        ["PageInfos"] = typeof(PublicPage),
        ["PageContents"] = typeof(PublicPageContent),
        ["PageTemplates"] = typeof(PublicPageTemplate),
        ["SiteSettings"] = typeof(PublicSiteSetting),
        ["SiteCodeSnippets"] = typeof(PublicSiteCodeSnippet),
        ["Languages"] = typeof(PublicLanguage),
        ["SitemapCaches"] = typeof(PublicSitemapCache),
        ["Redirects"] = typeof(PublicRedirect),
        ["Tags"] = typeof(PublicTag),
        ["PageInfoTags"] = typeof(PublicPageInfoTag),
        ["PageInfoSiteCodeExclusions"] = typeof(PublicPageInfoSiteCodeExclusion),
    };

    private static readonly Dictionary<string, Type> AnalyticsEntityByTable = new()
    {
        ["PageViewHits"] = typeof(PublicPageViewHit),
        ["PageClickHits"] = typeof(PublicPageClickHit),
        ["FormSubmissions"] = typeof(PublicFormSubmission),
        ["AppLogs"] = typeof(PublicAppLog),
    };

    [Fact]
    public void Analytics_write_mapping_expects_exactly_the_contracted_columns()
    {
        DbContextOptions<AnalyticsDbContext> options = new DbContextOptionsBuilder<AnalyticsDbContext>()
            .UseNpgsql("Host=model-only;Database=model-only")
            .Options;

        using AnalyticsDbContext context = new(options);

        foreach ((string tableName, Type entityClrType) in AnalyticsEntityByTable)
        {
            IEntityType entityType = context.Model.FindEntityType(entityClrType).ShouldNotBeNull();
            entityType.GetTableName().ShouldBe(tableName);

            var table = StoreObjectIdentifier.Table(tableName);
            var mappedColumns = entityType.GetProperties()
                .Select(property => property.GetColumnName(table))
                .OfType<string>()
                .ToHashSet(StringComparer.Ordinal);

            HashSet<string> contractedColumns = new(PublicContract[tableName], StringComparer.Ordinal);

            mappedColumns.ShouldBe(contractedColumns, ignoreOrder: true,
                $"The analytics write mapping for '{tableName}' drifted from the shared contract. " +
                "Update the contract in BOTH tests/web and tests/cms contract test files.");
        }
    }

    [Fact]
    public void Web_mapping_expects_exactly_the_contracted_tables_and_columns()
    {
        // Building the model needs a relational provider but never opens a connection.
        DbContextOptions<PublicReadDbContext> options = new DbContextOptionsBuilder<PublicReadDbContext>()
            .UseNpgsql("Host=model-only;Database=model-only")
            .Options;

        using PublicReadDbContext context = new(options);
        IModel model = context.Model;

        foreach ((string tableName, Type entityClrType) in EntityByTable)
        {
            IEntityType entityType = model.FindEntityType(entityClrType).ShouldNotBeNull();
            entityType.GetTableName().ShouldBe(tableName);

            var table = StoreObjectIdentifier.Table(tableName);
            var mappedColumns = entityType.GetProperties()
                .Select(property => property.GetColumnName(table))
                .OfType<string>()
                .ToHashSet(StringComparer.Ordinal);

            HashSet<string> contractedColumns = new(PublicContract[tableName], StringComparer.Ordinal);

            mappedColumns.ShouldBe(contractedColumns, ignoreOrder: true,
                $"The Web mapping for '{tableName}' drifted from the shared contract. " +
                "Update the contract in BOTH tests/web and tests/cms contract test files " +
                "so the CMS side re-verifies the column really exists.");
        }
    }
}
