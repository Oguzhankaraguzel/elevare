namespace Domain.Entities.PublicSiteSettings;

/// <summary>Read-only projection of the CMS's <c>SiteSettings</c> table.</summary>
public sealed class PublicSiteSetting
{
    public int Id { get; set; }
    public string Key { get; set; } = null!;
    public string? Value { get; set; }
    public bool IsDeleted { get; set; }
}
