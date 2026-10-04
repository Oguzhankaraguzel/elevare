namespace Domain.Entities.PublicSiteCodeSnippets;

/// <summary>
/// Read-only projection of the CMS's <c>SiteCodeSnippets</c> table — the third-party
/// tags injected into every public page.
/// <para>
/// <c>Placement</c> and <c>Kind</c> are kept as <see cref="int"/> rather than
/// mirrored enums: the layout only needs to group and order by them, and duplicating
/// the enum here would create a second definition that can silently drift from the
/// CMS's. The numeric values are pinned by the schema contract tests.
/// </para>
/// </summary>
public sealed class PublicSiteCodeSnippet
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public int Placement { get; set; }
    public string? Content { get; set; }
    public bool IsEnabled { get; set; }
    public int SortOrder { get; set; }
    public bool IsDeleted { get; set; }
}
