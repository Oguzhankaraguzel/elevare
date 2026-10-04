using Application.Abstraction.Services.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Persistence.Data;
using Shouldly;

namespace Cms.Tests.Contracts;

/// <summary>
/// Pins the table/column names the PUBLIC WEB app reads from this database.
/// <para>
/// The Web project (src/web) opens a read-only EF context over the same physical
/// database and maps these exact names. If a CMS refactor renames one of them,
/// the Web site would fail at runtime with a SqlException — this test turns that
/// into a build-time failure instead.
/// </para>
/// <para>
/// The mirror of this contract lives in tests/web/ArchitectureTests
/// (PublicSchemaContractTests): the Web side asserts it expects exactly these
/// names; this side asserts the CMS model actually produces them.
/// </para>
/// </summary>
public sealed class PublicSchemaContractTests
{
    // ── The shared contract — keep in sync with tests/web/ArchitectureTests ──
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
        ["Redirects"] = ["Id", "OldPath", "NewPath", "SourcePageId", "IsDeleted"],
        ["Tags"] = ["Id", "Name", "Slug", "IsDeleted"],
        ["PageInfoTags"] = ["PageInfoId", "TagId"],
        ["PageInfoSiteCodeExclusions"] = ["PageInfoId", "SiteCodeSnippetId"],
        // The tables the Web app WRITES to (anonymous page-view/click telemetry + form submissions + logs).
        ["PageViewHits"] = ["Id", "Path", "Title", "VisitorId", "ViewedAtUtc", "DurationSeconds"],
        ["PageClickHits"] = ["Id", "Path", "ElementLabel", "VisitorId", "ClickedAtUtc"],
        ["FormSubmissions"] = ["Id", "PageInfoId", "FormName", "FieldsJson", "SubmittedAtUtc"],
        ["AppLogs"] = ["Id", "Level", "Message", "Exception", "Source", "Path", "UserAgent", "CreatedAtUtc"],
    };

    [Fact]
    public void Cms_model_produces_every_column_the_public_web_app_reads()
    {
        // Building the model needs a relational provider but never opens a connection.
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=model-only;Database=model-only")
            .Options;

        using ApplicationDbContext context = new(options, new ModelOnlyUserContext());
        IModel model = context.Model;

        foreach ((string tableName, string[] expectedColumns) in PublicContract)
        {
            var table = StoreObjectIdentifier.Table(tableName);

            var actualColumns = model.GetEntityTypes()
                .Where(entityType => entityType.GetTableName() == tableName)
                .SelectMany(entityType => entityType.GetProperties())
                .Select(property => property.GetColumnName(table))
                .OfType<string>()
                .ToHashSet(StringComparer.Ordinal);

            actualColumns.ShouldNotBeEmpty($"Table '{tableName}' is not mapped by the CMS model at all.");

            foreach (string expected in expectedColumns)
            {
                actualColumns.ShouldContain(expected,
                    $"Column '{tableName}.{expected}' is part of the public web read contract " +
                    "but the CMS model no longer maps it. Either restore the name or update " +
                    "the Web mapping AND both contract tests together.");
            }
        }
    }

    private sealed class ModelOnlyUserContext : IUserContext
    {
        public Guid UserId => Guid.Empty;
        public bool IsAdminOrAbove => false;
        public bool CanAuthorCustomCode => false;
        public bool HasPermission(string permissionKey) => false;
        public bool IsInRole(string roleName) => false;
    }
}
